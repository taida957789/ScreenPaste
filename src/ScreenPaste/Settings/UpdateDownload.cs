using System.IO;

namespace ScreenPaste.Settings;

/// <summary>How far the update download has got: bytes so far, of <see cref="Total"/> when the
/// server said, at <see cref="BytesPerSecond"/> lately.</summary>
public readonly record struct DownloadProgress(long Received, long? Total, double BytesPerSecond)
{
    /// <summary>0..1 of the total, or null when the size is not known.</summary>
    public double? Fraction => Total is > 0 and var total ? Math.Min(1, (double)Received / total) : null;

    /// <summary>The whole percent that has arrived, rounded down so it is 100 only once it is
    /// all there; null when the size is not known.</summary>
    public int? Percent => Total is > 0 and var total ? (int)Math.Min(100, Received * 100 / total) : null;
}

/// <summary>
/// Download speed over the last few seconds rather than since the start, so it follows a
/// connection that speeds up or slows down without jumping at every read.
/// </summary>
internal sealed class DownloadSpeedMeter
{
    private static readonly TimeSpan Window = TimeSpan.FromSeconds(3);
    private readonly List<(TimeSpan At, long Received)> _samples = new();

    /// <summary>Note that <paramref name="received"/> bytes had arrived at <paramref name="at"/>;
    /// returns the bytes per second over the window up to then (0 while no time has passed).</summary>
    public double Sample(TimeSpan at, long received)
    {
        _samples.Add((at, received));
        // Keep the newest sample that is a window old, so the speed spans the whole window.
        var drop = 0;
        while (drop + 1 < _samples.Count && at - _samples[drop + 1].At >= Window) drop++;
        _samples.RemoveRange(0, drop);

        var (from, start) = _samples[0];
        var seconds = (at - from).TotalSeconds;
        return seconds > 0 ? (received - start) / seconds : 0;
    }
}

/// <summary>Copying the installer while saying how it goes, and that in words.</summary>
internal static class UpdateDownload
{
    internal const int BufferSize = 81920;
    private static readonly TimeSpan ReportInterval = TimeSpan.FromMilliseconds(200);
    private static readonly TimeSpan IdleTimeout = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Copy <paramref name="source"/> to <paramref name="target"/>, reporting the start, a few
    /// times a second after it (also while nothing arrives, so the speed falls) and the end.
    /// <paramref name="clock"/> is the time since some start. Throws <see cref="TimeoutException"/>
    /// when nothing arrives for <paramref name="idleTimeout"/> (30 s unless given): past the
    /// response headers no other timeout holds, so a dropped connection would wait forever.
    /// </summary>
    public static async Task CopyAsync(Stream source, Stream target, long? total,
        IProgress<DownloadProgress>? progress, Func<TimeSpan> clock, TimeSpan? idleTimeout = null)
    {
        var meter = new DownloadSpeedMeter();
        var lastReport = clock();
        meter.Sample(lastReport, 0);
        progress?.Report(new DownloadProgress(0, total, 0));

        long received = 0, reported = 0;
        void ReportIfDue(TimeSpan now, bool always = false)
        {
            if (!always && now - lastReport < ReportInterval) return;
            lastReport = now;
            reported = received;
            progress?.Report(new DownloadProgress(received, total, meter.Sample(now, received)));
        }

        using var idle = new CancellationTokenSource();
        var buffer = new byte[BufferSize];
        Task? tick = null;   // one timer at a time, kept across reads until it fires
        while (true)
        {
            idle.CancelAfter(idleTimeout ?? IdleTimeout);
            var read = source.ReadAsync(buffer, idle.Token).AsTask();
            while (!read.IsCompleted)
            {
                tick ??= Task.Delay(ReportInterval);
                if (await Task.WhenAny(read, tick).ConfigureAwait(false) != tick) break;
                tick = null;
                ReportIfDue(clock());
            }

            int count;
            try { count = await read.ConfigureAwait(false); }
            catch (OperationCanceledException) when (idle.IsCancellationRequested)
            {
                throw new TimeoutException($"Nothing arrived for {idleTimeout ?? IdleTimeout}.");
            }
            if (count == 0) break;

            await target.WriteAsync(buffer.AsMemory(0, count)).ConfigureAwait(false);
            received += count;
            ReportIfDue(clock());
        }
        if (reported != received) ReportIfDue(clock(), always: true);
    }

    /// <summary>
    /// <paramref name="bytes"/> in the largest of <paramref name="units"/> (bytes, then each
    /// 1024 times the last) that keeps it at least 1 as shown: "512 B", "1.5 KB", "150 KB".
    /// Numbers are written as <paramref name="culture"/> writes them, by default the Windows
    /// regional format, as elsewhere in the app.
    /// </summary>
    public static string Size(double bytes, IReadOnlyList<string> units, IFormatProvider? culture = null)
    {
        var unit = 0;
        while (Math.Round(bytes) >= 1024 && unit < units.Count - 1) { bytes /= 1024; unit++; }
        var whole = unit == 0 || Math.Round(bytes, 1) >= 100;
        return string.Format(culture, whole ? "{0:0} {1}" : "{0:0.0} {1}", bytes, units[unit]);
    }

    /// <summary>"12.3 MB / 45.6 MB · 1.2 MB/s", or without the total when it is not known.</summary>
    public static string Detail(DownloadProgress progress, IReadOnlyList<string> units, IFormatProvider? culture = null)
    {
        var received = Size(progress.Received, units, culture);
        var amount = progress.Fraction is null ? received : received + " / " + Size(progress.Total!.Value, units, culture);
        return amount + " · " + Size(progress.BytesPerSecond, units, culture) + "/s";
    }
}
