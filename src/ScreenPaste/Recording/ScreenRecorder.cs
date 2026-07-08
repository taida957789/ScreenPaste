using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Windows;
using PixelFormat = System.Drawing.Imaging.PixelFormat;
using Size = System.Drawing.Size;

namespace ScreenPaste.Recording;

/// <summary>Thrown when no bundled or system ffmpeg executable can be located.</summary>
public sealed class FFmpegNotFoundException : Exception { }

/// <summary>
/// Captures a fixed screen region on a background thread at a target frame rate and
/// pipes each frame to <see cref="FFmpegEncoder"/>. Encoder-agnostic capture pipeline —
/// the chosen <see cref="RecordingFormat"/> only affects the ffmpeg output stage.
/// </summary>
public sealed class ScreenRecorder
{
    private readonly int _w, _h, _fps;
    private volatile int _x, _y;              // capture origin; movable mid-recording
    private readonly RecordingFormat? _format;
    private readonly bool _captureCursor;

    private FFmpegEncoder? _encoder;
    private Bitmap? _bmp;
    private Graphics? _g;
    private byte[] _buffer = Array.Empty<byte>();
    private Thread? _thread;
    private volatile bool _stop;

    public string OutputPath { get; }

    /// <summary>Final format, or null when recording an intermediate MP4 for the editor.</summary>
    public RecordingFormat? Format => _format;
    public int Fps => _fps;
    public int FrameWidth => _w;
    public int FrameHeight => _h;

    /// <summary>
    /// Move the capture origin mid-recording (the frame SIZE is fixed by the encoder
    /// stream). The capture loop picks the new origin up on its next frame.
    /// </summary>
    public void MoveTo(int x, int y)
    {
        _x = x;
        _y = y;
    }

    /// <param name="format">Final output format, or null to record a near-lossless
    /// intermediate MP4 that the post-recording editor trims and re-encodes.</param>
    public ScreenRecorder(Int32Rect screenRegion, int fps, RecordingFormat? format,
                          string outputPath, bool captureCursor)
    {
        _x = screenRegion.X;
        _y = screenRegion.Y;
        // Force even dimensions — required by H.264 (mp4) and harmless for gif/webp.
        _w = Math.Max(2, screenRegion.Width) & ~1;
        _h = Math.Max(2, screenRegion.Height) & ~1;
        _fps = Math.Clamp(fps, 2, 30);
        _format = format;
        _captureCursor = captureCursor;
        OutputPath = outputPath;
    }

    /// <summary>Locate ffmpeg, start the encoder, and begin capturing. Throws if ffmpeg is missing.</summary>
    public void Start()
    {
        var ffmpeg = FFmpegLocator.Find() ?? throw new FFmpegNotFoundException();

        _encoder = new FFmpegEncoder(ffmpeg, _w, _h, _fps, _format, OutputPath);

        _bmp = new Bitmap(_w, _h, PixelFormat.Format32bppArgb);
        _g = Graphics.FromImage(_bmp);
        _buffer = new byte[_w * _h * 4];

        _thread = new Thread(CaptureLoop) { IsBackground = true, Name = "ScreenRecorder" };
        _thread.Start();
    }

    private void CaptureLoop()
    {
        long interval = Stopwatch.Frequency / _fps;
        var sw = Stopwatch.StartNew();
        long next = 0;

        while (!_stop)
        {
            CaptureFrame();

            next += interval;
            long remaining = next - sw.ElapsedTicks;
            if (remaining > 0)
            {
                int ms = (int)(remaining * 1000 / Stopwatch.Frequency);
                if (ms > 0) Thread.Sleep(ms);
            }
            else
            {
                // Falling behind — rebase so we don't burst to catch up.
                next = sw.ElapsedTicks;
            }
        }
    }

    private void CaptureFrame()
    {
        if (_g == null || _bmp == null || _encoder == null) return;

        // Snapshot the origin once so the frame and its cursor overlay agree even if
        // the region is being dragged mid-frame.
        int x = _x, y = _y;
        _g.CopyFromScreen(x, y, 0, 0, new Size(_w, _h), CopyPixelOperation.SourceCopy);
        if (_captureCursor) CursorCapture.Draw(_g, x, y);

        var rect = new Rectangle(0, 0, _w, _h);
        BitmapData data = _bmp.LockBits(rect, ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
        try
        {
            int rowBytes = _w * 4;
            if (data.Stride == rowBytes)
            {
                Marshal.Copy(data.Scan0, _buffer, 0, _buffer.Length);
            }
            else
            {
                for (int row = 0; row < _h; row++)
                    Marshal.Copy(data.Scan0 + row * data.Stride, _buffer, row * rowBytes, rowBytes);
            }
        }
        finally
        {
            _bmp.UnlockBits(data);
        }

        _encoder.WriteFrame(_buffer, _buffer.Length);
    }

    /// <summary>Stop capturing, flush ffmpeg, and return whether the file was written.</summary>
    public async Task<bool> StopAsync()
    {
        _stop = true;
        _thread?.Join(3000);

        bool ok = _encoder != null && await _encoder.FinishAsync();

        _g?.Dispose();
        _bmp?.Dispose();
        _encoder?.Dispose();
        _g = null;
        _bmp = null;
        _encoder = null;

        return ok;
    }
}
