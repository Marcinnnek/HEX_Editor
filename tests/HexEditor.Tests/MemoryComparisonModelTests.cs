using HexEditor.Core;
using HexEditor.Compare;
using System.Diagnostics;

namespace HexEditor.Tests;

[TestClass]
public sealed class MemoryComparisonModelTests
{
    [TestMethod]
    public void ComparisonPanels_UseSharedHexAsciiAndMissingByteText()
    {
        var left = MemoryImage.FromSegments([new MemorySegment(0x1000, [0x41, 0x00, 0x7F])]);
        var right = MemoryImage.Empty;
        var comparison = new MemoryComparisonModel(left, right, bytesPerRow: 8);

        var row = comparison.Rows[0];

        Assert.AreEqual("41 00 7F" + new string(' ', 15), row.FileA.HexData);
        Assert.AreEqual("A..     ", row.FileA.Ascii);
        Assert.AreEqual(new string(' ', 23), row.FileB.HexData);
        Assert.AreEqual("        ", row.FileB.Ascii);
        Assert.AreEqual(CompareByteState.OnlyA, row.FileA.Cells[0].State);
        Assert.AreEqual("", row.FileB.Cells[0].Hex);
        Assert.AreEqual(" ", row.FileB.Cells[0].Ascii);
    }

    [TestMethod]
    public void ComparisonModel_AlignsSparseBytesAndFindsOnlyPresentDifferences()
    {
        var left = MemoryImage.FromSegments([
            new MemorySegment(0x1001, [0x11, 0x22]),
            new MemorySegment(0x1010, [0x44])]);
        var right = MemoryImage.FromSegments([
            new MemorySegment(0x1001, [0x11, 0x33]),
            new MemorySegment(0x1004, [0x55])]);
        var comparison = new MemoryComparisonModel(left, right, bytesPerRow: 8);

        Assert.AreEqual(CompareByteState.Equal, comparison.Rows[0].States[1]);
        Assert.AreEqual(CompareByteState.Different, comparison.Rows[0].States[2]);
        Assert.AreEqual(CompareByteState.OnlyB, comparison.Rows[0].States[4]);
        Assert.AreEqual(CompareByteState.OnlyA, comparison.Rows[2].States[0]);
        Assert.AreEqual("3 differences (1 different, 1 only in File A, 1 only in File B)", comparison.ComparisonStatus);
        Assert.IsTrue(comparison.TryFindNextDifference(0x1001, out var address));
        Assert.AreEqual(0x1002u, address);
    }

    [TestMethod]
    public void ComparisonModel_UsesArbitraryWidthsForGlobalRowsAndDifferenceNavigation()
    {
        foreach (var width in new[] { 1, 3, 24, 80, 128 })
        {
            var left = MemoryImage.FromSegments([
                new MemorySegment(0x1001, [0x10, 0x11, 0x12, 0x13, 0x14, 0x15]),
                new MemorySegment(0x90000003, [0x20])]);
            var right = MemoryImage.FromSegments([
                new MemorySegment(0x1001, [0x10, 0x11, 0x99, 0x13, 0x14, 0x15]),
                new MemorySegment(0x90000003, [0x21])]);
            var comparison = new MemoryComparisonModel(left, right, bytesPerRow: width);

            Assert.IsTrue(comparison.TryNavigateToAddress(0x1003, out var rowIndex), $"width {width}");
            var expectedStart = (0x1003 / width) * width;
            Assert.AreEqual(expectedStart.ToString("X8"), comparison.Rows[rowIndex].Address, $"width {width}");
            Assert.AreEqual(width, comparison.Rows[rowIndex].FileA.Cells.Count, $"File A width {width}");
            Assert.AreEqual(width, comparison.Rows[rowIndex].FileB.Cells.Count, $"File B width {width}");
            Assert.AreEqual(CompareByteState.Different,
                comparison.Rows[rowIndex].States[0x1003 - expectedStart], $"width {width}");

            comparison.ViewMode = CompareViewMode.DifferencesOnly;
            Assert.IsTrue(comparison.TryNavigateToAddress(0x1003, out var differenceRow), $"difference row width {width}");
            var differenceAddress = comparison.Rows[differenceRow].Address;
            Assert.AreEqual(((0x1003 / width) * width).ToString("X8"), differenceAddress, $"difference row width {width}");
            Assert.AreEqual(width, comparison.Rows[differenceRow].FileA.Cells.Count, $"File A difference width {width}");
            Assert.AreEqual(width, comparison.Rows[differenceRow].FileB.Cells.Count, $"File B difference width {width}");
            Assert.IsTrue(comparison.TryFindNextDifferenceFromAddress(0x1001, null, out var next), $"next difference width {width}");
            Assert.AreEqual(0x1003u, next, $"next difference width {width}");
        }
    }

    [TestMethod]
    public void ComparisonModel_CustomWidthShowsPartialFinalRowWithoutOverflow()
    {
        var left = MemoryImage.FromPatternRange(uint.MaxValue, uint.MaxValue, [0xA5]);
        var right = MemoryImage.Empty;
        var comparison = new MemoryComparisonModel(left, right, bytesPerRow: 128);

        Assert.IsTrue(comparison.TryNavigateToAddress(uint.MaxValue, out var rowIndex));
        var row = comparison.Rows[rowIndex];
        Assert.AreEqual("FFFFFF80", row.Address);
        Assert.AreEqual(128, row.FileA.Cells.Count);
        Assert.AreEqual(uint.MaxValue, row.FileA.Cells[127].Address);
        Assert.AreEqual(string.Empty, row.FileA.Cells[126].Hex);
        Assert.AreEqual(CompareByteState.OnlyA, row.FileA.Cells[127].State);
    }

    [TestMethod]
    public void ComparisonModel_RefreshUpdatesDifferenceStatusAndRows()
    {
        var left = MemoryImage.FromSegments([new MemorySegment(0x2000, [0x10])]);
        var right = MemoryImage.FromSegments([new MemorySegment(0x2000, [0x20])]);
        var comparison = new MemoryComparisonModel(left, right);

        comparison.Refresh(left, left);

        Assert.AreEqual("No differences", comparison.ComparisonStatus);
        Assert.AreEqual(CompareByteState.Equal, comparison.Rows[0].States[0]);
    }

    [TestMethod]
    public void ComparisonModel_RangeImagesShowPanelsAndNavigateAtBothAddressBoundaries()
    {
        var left = MemoryImage.FromRanges([
            MemoryImage.FromPatternRange(0, 1, new byte[] { 0x11, 0x22 }).Ranges[0],
            MemoryImage.FromPatternRange(uint.MaxValue - 1, uint.MaxValue, new byte[] { 0x33, 0x44 }).Ranges[0]]);
        var right = MemoryImage.FromRanges([
            MemoryImage.FromPatternRange(0, 1, new byte[] { 0x11, 0x23 }).Ranges[0],
            MemoryImage.FromPatternRange(uint.MaxValue - 1, uint.MaxValue, new byte[] { 0x33, 0x45 }).Ranges[0]]);

        var comparison = new MemoryComparisonModel(left, right, bytesPerRow: 8);

        Assert.AreEqual("2 differences (2 different, 0 only in File A, 0 only in File B)", comparison.ComparisonStatus);
        Assert.IsTrue(comparison.TryFindNextDifference(0, out var first));
        Assert.AreEqual(1u, first);
        Assert.IsTrue(comparison.TryFindNextDifference(uint.MaxValue - 2, out var last));
        Assert.AreEqual(uint.MaxValue, last);
        Assert.IsTrue(comparison.TryNavigateToAddress(uint.MaxValue, out var rowIndex));
        Assert.AreEqual("FFFFFFF8", comparison.Rows[rowIndex].Address);
        Assert.AreEqual("44", comparison.Rows[rowIndex].FileA.Cells[7].Hex);
    }

    [TestMethod]
    public void ComparisonModel_RangeAddressPresentOnOneSideIsCountedAndRendered()
    {
        var left = MemoryImage.FromPatternRange(uint.MaxValue, uint.MaxValue, new byte[] { 0xA5 });
        var comparison = new MemoryComparisonModel(left, MemoryImage.Empty, bytesPerRow: 8);

        Assert.AreEqual("1 differences (0 different, 1 only in File A, 0 only in File B)", comparison.ComparisonStatus);
        Assert.AreEqual(CompareByteState.OnlyA, comparison.Rows[0].FileA.Cells[7].State);
        Assert.AreEqual("A5", comparison.Rows[0].FileA.Cells[7].Hex);
    }

    [TestMethod]
    public void ComparisonModel_IdenticalFullAddressRangesOpenWithoutScanningEveryByte()
    {
        var image = MemoryImage.FromPatternRange(0, uint.MaxValue, new byte[] { 0xA5, 0x5A });

        var comparison = new MemoryComparisonModel(image, image, bytesPerRow: 8);

        Assert.AreEqual("No differences", comparison.ComparisonStatus);
        Assert.AreEqual(536870912, comparison.Rows.Count);
        Assert.IsTrue(comparison.TryNavigateToAddress(uint.MaxValue, out var rowIndex));
        Assert.AreEqual("FFFFFFF8", comparison.Rows[rowIndex].Address);
        Assert.AreEqual("A5 5A A5 5A A5 5A A5 5A", comparison.Rows[rowIndex].FileA.HexData);
    }

    [TestMethod]
    public void ComparisonModel_FullRepeatedRangeAgainstEmptyCountsDifferencesWithoutScanningEveryAddress()
    {
        var image = MemoryImage.FromRepeatedRange(0, uint.MaxValue, 0xA5);

        var timer = Stopwatch.StartNew();
        var comparison = new MemoryComparisonModel(image, MemoryImage.Empty, bytesPerRow: 8);
        timer.Stop();

        Assert.IsTrue(timer.Elapsed < TimeSpan.FromSeconds(2), $"Summarizing a repeated full range took {timer.Elapsed}.");
        Assert.AreEqual($"{4_294_967_296L:N0} differences (0 different, {4_294_967_296L:N0} only in File A, 0 only in File B)", comparison.ComparisonStatus);
    }

    [TestMethod]
    public void ComparisonModel_DifferentFullRepeatedRangesCountEveryDifferenceWithoutScanningEveryAddress()
    {
        var left = MemoryImage.FromPatternRange(0, uint.MaxValue, [0xA5, 0x5A]);
        var right = MemoryImage.FromPatternRange(0, uint.MaxValue, [0xA5, 0x6A]);

        var timer = Stopwatch.StartNew();
        var comparison = new MemoryComparisonModel(left, right, bytesPerRow: 8);
        timer.Stop();

        Assert.IsTrue(timer.Elapsed < TimeSpan.FromSeconds(2), $"Summarizing differing repeated full ranges took {timer.Elapsed}.");
        Assert.AreEqual($"{2_147_483_648L:N0} differences ({2_147_483_648L:N0} different, 0 only in File A, 0 only in File B)", comparison.ComparisonStatus);
    }

    [TestMethod]
    public void ComparisonModel_TenMegabyteOpenAndVirtualRowAccessStayBounded()
    {
        const int imageSize = 10 * 1024 * 1024;
        var baseData = new byte[imageSize];
        var referenceData = new byte[imageSize];
        referenceData[^1] = 1;
        var baseImage = MemoryImage.FromSegments([new MemorySegment(0x1000, baseData)]);
        var referenceImage = MemoryImage.FromSegments([new MemorySegment(0x1000, referenceData)]);

        var timer = Stopwatch.StartNew();
        var comparison = new MemoryComparisonModel(baseImage, referenceImage, bytesPerRow: 16);
        timer.Stop();

        Assert.IsTrue(timer.Elapsed < TimeSpan.FromSeconds(15),
            $"Opening a 10 MB comparison took {timer.Elapsed}.");
        Assert.AreEqual("1 differences (1 different, 0 only in File A, 0 only in File B)", comparison.ComparisonStatus);
        Assert.AreEqual(655360, comparison.Rows.Count);

        // Simulate repeated DataGrid virtualization requests over distant viewport positions.
        for (var index = 0; index < comparison.Rows.Count; index += 4096)
            Assert.IsFalse(comparison.Rows[index].IsGapMarker);
    }

    [TestMethod]
    public void ComparisonModel_LongSummaryReportsProgressAndHonorsCancellation()
    {
        var data = new byte[256 * 1024];
        var image = MemoryImage.FromSegments([new MemorySegment(0x1000, data)]);
        using var cancellation = new CancellationTokenSource();
        var progress = new CancellingComparisonProgress(cancellation);

        Assert.Throws<OperationCanceledException>(() =>
            new MemoryComparisonModel(image, image, cancellationToken: cancellation.Token, progress: progress));
        Assert.IsTrue(progress.Reports > 0);
    }

    private sealed class CancellingComparisonProgress(CancellationTokenSource cancellation) : IProgress<ComparisonProgress>
    {
        public int Reports { get; private set; }
        public void Report(ComparisonProgress value)
        {
            Reports++;
            cancellation.Cancel();
        }
    }
}
