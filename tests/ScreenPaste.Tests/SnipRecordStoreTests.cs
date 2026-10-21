using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using ScreenPaste.Capture;
using ScreenPaste.Native;
using Xunit;

namespace ScreenPaste.Tests;

/// <summary>
/// , / . replay the snip history: the last successful snips, newest first, held in memory up
/// to a record count and a byte budget, oldest dropped first. The records here are plain
/// strings with a made-up size; the store never looks inside them.
/// </summary>
public sealed class SnipRecordStoreTests
{
    private const long MB = 1024 * 1024;

    private static SnipRecordStore<string> Store(int capacity = 8, long budget = 512 * MB) => new(capacity, budget);

    private static SnipRecordStore<string> With(params string[] oldestFirst)
    {
        var store = Store();
        foreach (var r in oldestFirst) store.Add(r, 10 * MB);
        return store;
    }

    // ---------------------------------------------------------------- adding, eviction ---

    [Fact]
    public void Add_PutsTheNewestFirst_AndCountsItsBytes()
    {
        var store = Store();

        store.Add("a", 10 * MB);
        store.Add("b", 20 * MB);

        Assert.Equal(new[] { "b", "a" }, store.Items);
        Assert.Equal(30 * MB, store.TotalBytes);
    }

    [Fact]
    public void Add_PastTheRecordCount_DropsTheOldest()
    {
        var store = Store(capacity: 3);

        foreach (var r in new[] { "a", "b", "c", "d" }) store.Add(r, MB);

        Assert.Equal(new[] { "d", "c", "b" }, store.Items);
        Assert.Equal(3 * MB, store.TotalBytes);
    }

    [Fact]
    public void Add_PastTheByteBudget_DropsTheOldestUntilItFits()
    {
        var store = Store(budget: 100 * MB);
        store.Add("a", 40 * MB);
        store.Add("b", 30 * MB);
        store.Add("c", 20 * MB);

        store.Add("d", 60 * MB);   // 150 MB: a and b have to go

        Assert.Equal(new[] { "d", "c" }, store.Items);
        Assert.Equal(80 * MB, store.TotalBytes);
    }

    [Fact]
    public void Add_ExactlyTheBudget_Fits()
    {
        var store = Store(budget: 100 * MB);

        Assert.True(store.Add("a", 100 * MB));
        Assert.Equal(1, store.Count);
    }

    [Fact]
    public void Add_ARecordLargerThanTheWholeBudget_IsTurnedAway_LeavingTheOthers()
    {
        var store = Store(budget: 100 * MB);
        store.Add("a", 40 * MB);

        bool added = store.Add("huge", 101 * MB);

        Assert.False(added);
        Assert.Equal(new[] { "a" }, store.Items);
        Assert.Equal(40 * MB, store.TotalBytes);
    }

    [Fact]
    public void Add_WithNoRoomAtAll_KeepsNothing()
    {
        var store = Store(capacity: 0);

        Assert.False(store.Add("a", MB));
        Assert.Equal(0, store.Count);
    }

    [Fact]
    public void Add_ANegativeSize_IsTurnedAway()
    {
        var store = Store();

        Assert.False(store.Add("a", -1));
        Assert.Equal(0, store.Count);
    }

    [Theory]
    [InlineData(8, 100, 100, true)]    // exactly the budget
    [InlineData(8, 100, 0, true)]
    [InlineData(8, 100, 101, false)]   // larger than the whole budget
    [InlineData(8, 100, -1, false)]
    [InlineData(0, 100, 1, false)]     // history off
    [InlineData(8, 0, 1, false)]       // no budget
    public void Accepts_TellsBeforehandWhetherAddWouldKeepARecordOfThatSize(
        int capacity, long budget, long bytes, bool expected)
    {
        var store = Store(capacity, budget);

        Assert.Equal(expected, store.Accepts(bytes));
        Assert.Equal(expected, store.Add("a", bytes));
    }

    [Fact]
    public void Accepts_DoesNotDependOnWhatIsAlreadyHeld()
    {
        // The oldest records make room, so a full store still takes one within the budget.
        var store = Store(capacity: 2, budget: 30);
        store.Add("a", 20);
        store.Add("b", 10);

        Assert.True(store.Accepts(30));
        Assert.Equal(new[] { "b", "a" }, store.Items);
        Assert.Equal(-1, store.Position);
    }

    // ---------------------------------------------------------------- , and . ---

    [Fact]
    public void Store_StartsOnTheLiveScreen_WithNoRecordShown()
    {
        var store = With("a", "b");

        Assert.True(store.IsLive);
        Assert.Equal(-1, store.Position);
        Assert.Null(store.Current);
    }

    [Fact]
    public void MoveOlder_FromTheLiveScreen_ShowsTheLastSnip_ThenOlderOnes()
    {
        var store = With("a", "b", "c");

        Assert.True(store.MoveOlder());
        Assert.Equal("c", store.Current);
        Assert.True(store.MoveOlder());
        Assert.Equal("b", store.Current);
    }

    [Fact]
    public void MoveOlder_StopsAtTheOldest()
    {
        var store = With("a", "b");
        store.MoveOlder();
        store.MoveOlder();

        Assert.False(store.MoveOlder());
        Assert.Equal("a", store.Current);
    }

    [Fact]
    public void MoveNewer_StepsBack_AndPastTheNewestReturnsToTheLiveScreen()
    {
        var store = With("a", "b", "c");
        store.MoveOlder();
        store.MoveOlder();   // b

        Assert.True(store.MoveNewer());
        Assert.Equal("c", store.Current);
        Assert.True(store.MoveNewer());
        Assert.True(store.IsLive);
        Assert.Null(store.Current);
    }

    [Fact]
    public void MoveNewer_OnTheLiveScreen_GoesNowhere()
    {
        var store = With("a");

        Assert.False(store.MoveNewer());
        Assert.True(store.IsLive);
    }

    [Fact]
    public void MoveOlder_WithNoRecords_GoesNowhere()
    {
        var store = Store();

        Assert.False(store.MoveOlder());
        Assert.True(store.IsLive);
    }

    [Fact]
    public void Add_ReturnsToTheLiveScreen()
    {
        var store = With("a", "b");
        store.MoveOlder();

        store.Add("c", MB);

        Assert.True(store.IsLive);
    }

    [Fact]
    public void Reset_ReturnsToTheLiveScreen_KeepingTheRecords()
    {
        var store = With("a", "b");
        store.MoveOlder();

        store.Reset();

        Assert.True(store.IsLive);
        Assert.Equal(2, store.Count);
    }

    [Fact]
    public void Clear_DropsEveryRecordAndItsBytes()
    {
        var store = With("a", "b");
        store.MoveOlder();

        store.Clear();

        Assert.Equal(0, store.Count);
        Assert.Equal(0, store.TotalBytes);
        Assert.True(store.IsLive);
    }

    [Fact]
    public void RemoveAll_DropsTheMatchingRecords_AndTheirBytes()
    {
        var store = Store();
        store.Add("keep-1", 10 * MB);
        store.Add("drop", 30 * MB);
        store.Add("keep-2", 20 * MB);
        store.MoveOlder();

        int removed = store.RemoveAll(r => r == "drop");

        Assert.Equal(1, removed);
        Assert.Equal(new[] { "keep-2", "keep-1" }, store.Items);
        Assert.Equal(30 * MB, store.TotalBytes);
        Assert.True(store.IsLive);
    }

    // ---------------------------------------------------------------- limits ---

    [Theory]
    [InlineData(8, 8)]
    [InlineData(3, 3)]
    [InlineData(20, 8)]   // never more than Snipaste's 8
    [InlineData(0, 0)]    // 0 turns the history off
    [InlineData(-4, 0)]
    public void Limits_RecordCount_IsTheSettingCappedAtEight(int setting, int expected)
    {
        Assert.Equal(expected, SnipHistoryLimits.Capacity(setting));
    }

    [Theory]
    [InlineData(512, 512)]
    [InlineData(100, 100)]
    [InlineData(4096, 512)]
    [InlineData(-1, 0)]
    public void Limits_Budget_IsTheSettingInMegabytes_CappedAt512(int setting, long expectedMb)
    {
        Assert.Equal(expectedMb * MB, SnipHistoryLimits.BudgetBytes(setting));
    }

    [Fact]
    public void RecordBytes_CountTheScreenshotAndTheFlattenedArea()
    {
        StaRunner.Run(() =>
        {
            // Arrange
            static BitmapSource Blank(int w, int h) =>
                BitmapSource.Create(w, h, 96, 96, PixelFormats.Pbgra32, null, new byte[w * h * 4], w * 4);
            var record = new SnipRecord(Blank(40, 30), new VirtualScreen(0, 0, 40, 30),
                new Int32Rect(5, 5, 10, 8), Blank(10, 8), Cursor: null);

            // Act + Assert
            Assert.Equal(40 * 30 * 4 + 10 * 8 * 4, record.Bytes);
        });
    }

    [Fact]
    public void RecordSize_IsFourBytesAPixel_WithoutOverflowingOnHugeScreens()
    {
        Assert.Equal(1920L * 1080 * 4, SnipRecord.BytesOf(1920, 1080));
        Assert.Equal(40000L * 40000 * 4, SnipRecord.BytesOf(40000, 40000));
        Assert.Equal(0, SnipRecord.BytesOf(0, 1080));
    }
}
