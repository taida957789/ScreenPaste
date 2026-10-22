using System.Diagnostics;
using System.Globalization;
using System.IO;
using ScreenPaste.Settings;
using Xunit;

namespace ScreenPaste.Tests;

/// <summary>
/// The update download as the update window shows it: how far it has got, how fast it goes
/// (over the last few seconds, so it follows the connection), and the sizes in words.
/// </summary>
public sealed class UpdateDownloadTests
{
    private static readonly string[] Units = { "B", "KB", "MB", "GB" };

    private static TimeSpan S(double seconds) => TimeSpan.FromSeconds(seconds);

    /// <summary>Takes each report as it is made, where <see cref="Progress{T}"/> would post it.</summary>
    private sealed class Reports : IProgress<DownloadProgress>
    {
        public List<DownloadProgress> All { get; } = new();
        public void Report(DownloadProgress value) => All.Add(value);
    }

    [Fact]
    public void Fraction_IsHowMuchOfTheTotalHasArrived()
    {
        Assert.Equal(0.25, new DownloadProgress(25, 100, 0).Fraction);
        Assert.Equal(1, new DownloadProgress(120, 100, 0).Fraction);   // never past the end
    }

    [Fact]
    public void Fraction_IsUnknown_WithoutATotal()
    {
        Assert.Null(new DownloadProgress(25, null, 0).Fraction);
        Assert.Null(new DownloadProgress(25, 0, 0).Fraction);
    }

    [Theory]
    [InlineData(29, 100, 29)]      // 0.29 * 100 in doubles is 28.999…
    [InlineData(999, 1000, 99)]    // rounded down: 100 only once it is all there
    [InlineData(1000, 1000, 100)]
    [InlineData(1200, 1000, 100)]
    public void Percent_IsTheWholePercentThatHasArrived(long received, long total, int expected)
    {
        Assert.Equal(expected, new DownloadProgress(received, total, 0).Percent);
    }

    [Fact]
    public void Percent_IsUnknown_WithoutATotal()
    {
        Assert.Null(new DownloadProgress(25, null, 0).Percent);
    }

    [Fact]
    public void Speed_IsTheBytesPerSecondSinceTheFirstSample()
    {
        var meter = new DownloadSpeedMeter();
        Assert.Equal(0, meter.Sample(S(0), 0));
        Assert.Equal(100, meter.Sample(S(1), 100));
        Assert.Equal(100, meter.Sample(S(2), 200));
    }

    [Fact]
    public void Speed_FollowsTheConnection_OnceAWindowHasPassed()
    {
        var meter = new DownloadSpeedMeter();
        for (var s = 0; s <= 3; s++) meter.Sample(S(s), s * 100);   // 100 B/s

        meter.Sample(S(4), 1300);                                     // then 1000 B/s
        meter.Sample(S(5), 2300);

        Assert.Equal(1000, meter.Sample(S(6), 3300));
    }

    [Fact]
    public void Speed_Falls_WhileNothingArrives()
    {
        var meter = new DownloadSpeedMeter();
        meter.Sample(S(0), 0);
        meter.Sample(S(1), 1000);

        Assert.Equal(500, meter.Sample(S(2), 1000));
    }

    [Fact]
    public void Speed_IsZero_WhenNoTimeHasPassed()
    {
        var meter = new DownloadSpeedMeter();
        meter.Sample(S(1), 500);
        Assert.Equal(0, meter.Sample(S(1), 900));
    }

    [Fact]
    public async Task Copy_CopiesEverything_AndReportsTheStartAndTheEnd()
    {
        var data = Enumerable.Range(0, 300_000).Select(i => (byte)i).ToArray();
        var target = new MemoryStream();
        var reports = new Reports();
        var now = TimeSpan.Zero;

        await UpdateDownload.CopyAsync(new MemoryStream(data), target, data.Length, reports, () => now += S(1));

        Assert.Equal(data, target.ToArray());
        Assert.Equal(new DownloadProgress(0, data.Length, 0), reports.All[0]);
        Assert.Equal(data.Length, reports.All[^1].Received);
        Assert.Equal(data.Length, reports.All[^1].Total);
        Assert.True(reports.All[^1].BytesPerSecond > 0);
        Assert.Equal(reports.All.Select(r => r.Received).Order(), reports.All.Select(r => r.Received));
    }

    [Fact]
    public async Task Copy_ReportsAFewTimesASecond_NotEveryRead()
    {
        var data = new byte[UpdateDownload.BufferSize * 20];
        var reports = new Reports();
        var now = TimeSpan.Zero;

        // 20 reads, 10 ms apart: one report each 200 ms, and the end.
        await UpdateDownload.CopyAsync(new MemoryStream(data), new MemoryStream(), data.Length, reports,
            () => now += TimeSpan.FromMilliseconds(10));

        Assert.InRange(reports.All.Count, 2, 4);
        Assert.Equal(data.Length, reports.All[^1].Received);
    }

    [Fact]
    public async Task Copy_ReportsTheEnd_EvenWhenTheClockDoesNotMove()
    {
        var reports = new Reports();

        await UpdateDownload.CopyAsync(new MemoryStream(new byte[1000]), new MemoryStream(), null, reports, () => TimeSpan.Zero);

        Assert.Equal(new DownloadProgress(1000, null, 0), reports.All[^1]);
    }

    [Fact]
    public async Task Copy_GivesUp_WhenNothingArrivesForTooLong_ReportingTheSpeedFallingMeanwhile()
    {
        var reports = new Reports();
        var clock = Stopwatch.StartNew();

        await Assert.ThrowsAsync<TimeoutException>(() => UpdateDownload.CopyAsync(
            new StallingStream(1000), new MemoryStream(), 10_000, reports, () => clock.Elapsed, TimeSpan.FromSeconds(1)));

        var stalled = reports.All.Skip(1).ToList();   // after the start, all while stalled
        Assert.True(stalled.Count >= 2, $"{stalled.Count} reports while stalled");
        Assert.All(stalled, r => Assert.Equal(1000, r.Received));
        Assert.True(stalled[^1].BytesPerSecond < stalled[0].BytesPerSecond);
    }

    /// <summary>Gives <paramref name="first"/> bytes, then nothing until the read is cancelled.</summary>
    private sealed class StallingStream(int first) : Stream
    {
        private bool _gave;

        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            if (!_gave) { _gave = true; return Math.Min(first, buffer.Length); }
            await Task.Delay(Timeout.Infinite, cancellationToken);
            return 0;
        }

        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
        public override void Flush() { }
        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }

    [Fact]
    public async Task Copy_OfNothing_ReportsJustTheStart()
    {
        var reports = new Reports();

        await UpdateDownload.CopyAsync(new MemoryStream(), new MemoryStream(), 0, reports, () => TimeSpan.Zero);

        Assert.Equal(new[] { new DownloadProgress(0, 0, 0) }, reports.All);
    }

    [Theory]
    [InlineData(0, "0 B")]
    [InlineData(512, "512 B")]
    [InlineData(1536, "1.5 KB")]
    [InlineData(150 * 1024, "150 KB")]
    [InlineData(45.6 * 1024 * 1024, "45.6 MB")]
    [InlineData(2.5 * 1024 * 1024 * 1024, "2.5 GB")]
    [InlineData(3000.0 * 1024 * 1024 * 1024, "3000 GB")]   // there is no larger unit
    [InlineData(1023.97, "1.0 KB")]                          // as shown it would be 1024 B
    [InlineData(1023.6 * 1024, "1.0 MB")]
    [InlineData(99.97 * 1024 * 1024, "100 MB")]              // whole from 100, as shown
    public void Size_UsesTheLargestUnitThatKeepsItAtLeastOne(double bytes, string expected)
    {
        Assert.Equal(expected, UpdateDownload.Size(bytes, Units, CultureInfo.InvariantCulture));
    }

    [Fact]
    public void Size_UsesTheGivenUnitsAndNumberFormat()
    {
        Assert.Equal("1,5 Ko", UpdateDownload.Size(1536, new[] { "o", "Ko", "Mo", "Go" }, CultureInfo.GetCultureInfo("fr-FR")));
    }

    [Fact]
    public void Detail_ShowsWhatHasArrived_OfTheTotal_AndTheSpeed()
    {
        var progress = new DownloadProgress(12_900_000, 47_800_000, 1_250_000);

        Assert.Equal("12.3 MB / 45.6 MB · 1.2 MB/s", UpdateDownload.Detail(progress, Units, CultureInfo.InvariantCulture));
    }

    [Fact]
    public void Detail_LeavesOutTheTotal_WhenItIsNotKnown()
    {
        var progress = new DownloadProgress(12_900_000, null, 800);

        Assert.Equal("12.3 MB · 800 B/s", UpdateDownload.Detail(progress, Units, CultureInfo.InvariantCulture));
    }
}
