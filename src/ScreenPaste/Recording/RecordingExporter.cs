using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;
using System.Windows;

namespace ScreenPaste.Recording;

/// <summary>A region of the recording to blur or pixelate, in video-pixel coordinates.</summary>
public readonly record struct BlurRegion(Int32Rect Rect, bool Mosaic, double Strength);

/// <summary>
/// Re-encodes a trimmed span of the intermediate recording into the final format via
/// ffmpeg — optionally blurring regions and compositing a static annotation overlay —
/// reporting determinate progress parsed from its -progress output.
/// </summary>
public sealed class RecordingExporter
{
    private readonly StringBuilder _stderrTail = new();

    /// <summary>The tail of ffmpeg's stderr — useful for diagnosing an export failure.</summary>
    public string Diagnostics => _stderrTail.ToString();

    /// <summary>Progress in [0, 1]. Raised on a background thread.</summary>
    public event Action<double>? Progress;

    public async Task<bool> ExportAsync(string ffmpegPath, string inputPath, string outputPath,
        RecordingFormat format, double startSeconds, double durationSeconds, int fps,
        string? overlayPngPath, IReadOnlyList<BlurRegion> blurRegions,
        CancellationToken ct)
    {
        var inv = CultureInfo.InvariantCulture;
        var psi = new ProcessStartInfo
        {
            FileName = ffmpegPath,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,   // -progress key=value stream
            RedirectStandardError = true,
        };

        psi.ArgumentList.Add("-y");
        if (startSeconds > 0.0005)
        {
            // -ss before -i: fast keyframe seek, then frame-accurate decode-forward.
            psi.ArgumentList.Add("-ss");
            psi.ArgumentList.Add(startSeconds.ToString("0.###", inv));
        }
        psi.ArgumentList.Add("-i");
        psi.ArgumentList.Add(inputPath);
        if (overlayPngPath != null)
        {
            // Still image; overlay's default repeatlast keeps it visible for the whole clip.
            psi.ArgumentList.Add("-i");
            psi.ArgumentList.Add(overlayPngPath);
        }
        psi.ArgumentList.Add("-t");
        psi.ArgumentList.Add(durationSeconds.ToString("0.###", inv));
        psi.ArgumentList.Add("-an");

        if (overlayPngPath == null && blurRegions.Count == 0)
        {
            foreach (var a in FFmpegEncoder.OutputArgs(format, fps)) psi.ArgumentList.Add(a);
        }
        else
        {
            psi.ArgumentList.Add("-filter_complex");
            psi.ArgumentList.Add(BuildFilterGraph(format, overlayPngPath != null, blurRegions, out var finalLabel));
            psi.ArgumentList.Add("-map");
            psi.ArgumentList.Add(finalLabel);
            foreach (var a in FFmpegEncoder.CodecArgs(format, fps)) psi.ArgumentList.Add(a);
        }

        psi.ArgumentList.Add("-progress");
        psi.ArgumentList.Add("pipe:1");
        psi.ArgumentList.Add("-nostats");
        psi.ArgumentList.Add(outputPath);

        using var proc = new Process { StartInfo = psi };
        proc.ErrorDataReceived += (_, e) =>
        {
            if (e.Data == null) return;
            _stderrTail.AppendLine(e.Data);
            if (_stderrTail.Length > 4096) _stderrTail.Remove(0, _stderrTail.Length - 4096);
        };
        proc.OutputDataReceived += (_, e) => ReportProgress(e.Data, durationSeconds);

        proc.Start();
        proc.BeginErrorReadLine();
        proc.BeginOutputReadLine();

        using var kill = ct.Register(() => { try { if (!proc.HasExited) proc.Kill(); } catch { /* ignore */ } });
        await proc.WaitForExitAsync(CancellationToken.None);   // killed via the registration

        if (ct.IsCancellationRequested || proc.ExitCode != 0 || !File.Exists(outputPath))
        {
            try { File.Delete(outputPath); } catch { /* partial file may not exist */ }
            return false;
        }
        Progress?.Invoke(1.0);
        return true;
    }

    /// <summary>
    /// Chain: [0:v] → per-region split/crop/blur/overlay → annotation PNG overlay →
    /// format-specific tail (palette for GIF, pixel format for MP4).
    /// </summary>
    private static string BuildFilterGraph(RecordingFormat format, bool hasOverlay,
        IReadOnlyList<BlurRegion> blurs, out string finalLabel)
    {
        var inv = CultureInfo.InvariantCulture;
        var chains = new List<string>();
        string cur = "[0:v]";

        for (int i = 0; i < blurs.Count; i++)
        {
            var (r, mosaic, strength) = blurs[i];
            string effect = mosaic
                ? $"pixelize=w={Math.Max(2, (int)strength)}:h={Math.Max(2, (int)strength)}"
                : $"gblur=sigma={strength.ToString("0.#", inv)}";
            chains.Add($"{cur}split=2[bg{i}][fg{i}]");
            chains.Add($"[fg{i}]crop={r.Width}:{r.Height}:{r.X}:{r.Y},{effect}[bl{i}]");
            chains.Add($"[bg{i}][bl{i}]overlay=x={r.X}:y={r.Y}[v{i}]");
            cur = $"[v{i}]";
        }

        if (hasOverlay)
        {
            chains.Add($"{cur}[1:v]overlay=0:0[va]");
            cur = "[va]";
        }

        if (FFmpegEncoder.FilterFragment(format) is { } frag)
        {
            chains.Add($"{cur}{frag}[vout]");
            finalLabel = "[vout]";
        }
        else
        {
            finalLabel = cur;
        }

        return string.Join(";", chains);
    }

    private void ReportProgress(string? line, double duration)
    {
        if (line == null || duration <= 0) return;

        double t;
        if (line.StartsWith("out_time_us=", StringComparison.Ordinal) &&
            long.TryParse(line.AsSpan("out_time_us=".Length), out var us))
            t = us / 1_000_000.0;
        else if (line.StartsWith("out_time=", StringComparison.Ordinal) &&
                 TimeSpan.TryParse(line.AsSpan("out_time=".Length), CultureInfo.InvariantCulture, out var ts))
            t = ts.TotalSeconds;
        else if (line.StartsWith("progress=end", StringComparison.Ordinal))
            t = duration;
        else
            return;

        Progress?.Invoke(Math.Clamp(t / duration, 0, 1));
    }
}
