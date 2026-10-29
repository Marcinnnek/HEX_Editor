using HexEditor.Core;
using HexEditor.UI;

namespace HexEditor.Tests;

[TestClass]
public sealed class CompareViewModelTests
{
    [TestMethod]
    public void Comparison_AlignsSparseBytesByAddressAndClassifiesEachSide()
    {
        var fileA = MemoryImage.FromSegments([new MemorySegment(0x1001, [0x11, 0x22]), new MemorySegment(0x1010, [0x44])]);
        var fileB = MemoryImage.FromSegments([new MemorySegment(0x1001, [0x11, 0x33]), new MemorySegment(0x1004, [0x55])]);
        var comparison = new CompareViewModel(fileA, fileB, bytesPerRow: 8);

        var row = comparison.Rows[0];

        Assert.AreEqual("00001000", row.Address);
        Assert.AreEqual(CompareByteState.Equal, row.States[1]);
        Assert.AreEqual(CompareByteState.Different, row.States[2]);
        Assert.AreEqual(CompareByteState.OnlyB, row.States[4]);
        Assert.AreEqual("11 22", row.FileA.HexData.Trim());
        Assert.IsTrue(row.FileB.HexData.Contains("11 33    55", StringComparison.Ordinal));
        Assert.AreEqual(CompareByteState.OnlyA, comparison.Rows[2].States[0]);
    }

    [TestMethod]
    public void ComparisonStatus_ReportsEqualityAndDifferenceCountsAndRefreshes()
    {
        var comparison = new CompareViewModel(
            MemoryImage.FromSegments([new MemorySegment(0x1000, [0x10, 0x20]), new MemorySegment(0x1010, [0x30])]),
            MemoryImage.FromSegments([new MemorySegment(0x1000, [0x10, 0x21]), new MemorySegment(0x1020, [0x40])]));

        Assert.AreEqual("3 differences (1 different, 1 only in File A, 1 only in File B)", comparison.ComparisonStatus);

        comparison.Refresh(comparison.FileA, MemoryImage.FromSegments([new MemorySegment(0x1000, [0x10, 0x20]), new MemorySegment(0x1010, [0x30])]));

        Assert.AreEqual("No differences", comparison.ComparisonStatus);
    }

    [TestMethod]
    public void Comparison_SharesNavigationAndSettingsAndCanShowOnlyDifferencesOrRange()
    {
        var fileA = MemoryImage.FromSegments([new MemorySegment(0x1000, [0x10, 0x20, 0x30]), new MemorySegment(0x90000000, [0x40])]);
        var fileB = MemoryImage.FromSegments([new MemorySegment(0x1000, [0x10, 0x21, 0x30]), new MemorySegment(0x90000000, [0x40])]);
        var comparison = new CompareViewModel(fileA, fileB);

        Assert.IsTrue(comparison.TryNavigateToAddress(0x1001, out var index));
        Assert.AreEqual("00001000", comparison.Rows[index].Address);
        Assert.IsTrue(comparison.TryFindNextDifference(0x1000, out var next));
        Assert.AreEqual(0x1001u, next);
        Assert.IsFalse(comparison.TryFindNextDifference(next + 1, out _));
        Assert.IsTrue(comparison.TryFindPreviousDifference(0x1002, out var previous));
        Assert.AreEqual(0x1001u, previous);

        comparison.BytesPerRow = 8;
        comparison.ShowAscii = false;
        comparison.ViewMode = CompareViewMode.DifferencesOnly;
        Assert.HasCount(1, comparison.Rows);
        Assert.IsNull(comparison.Rows[0].FileA.Ascii);
        Assert.AreEqual("00001000", comparison.Rows[0].Address);

        comparison.SetAddressRange(0x1001, 0x1001);
        comparison.ViewMode = CompareViewMode.AddressRange;
        Assert.HasCount(1, comparison.Rows);
        Assert.AreEqual(CompareByteState.Different, comparison.Rows[0].States[1]);

        comparison.Refresh(MemoryImage.FromSegments([new MemorySegment(0x1001, [0x21])]), fileB);
        Assert.AreEqual(CompareByteState.Equal, comparison.Rows[0].States[1]);
    }

    [TestMethod]
    public void MissingBytes_AreNavigableDifferencesAndAppearInDifferencesOnly()
    {
        var fileA = MemoryImage.FromSegments([new MemorySegment(0x2000, [0x10, 0x20])]);
        var fileB = MemoryImage.FromSegments([new MemorySegment(0x2000, [0x10, 0x21, 0x30])]);
        var comparison = new CompareViewModel(fileA, fileB, bytesPerRow: 8);

        Assert.IsTrue(comparison.TryFindNextDifference(0x2001, out var firstDifference));
        Assert.AreEqual(0x2001u, firstDifference);
        Assert.IsTrue(comparison.TryFindNextDifference(firstDifference + 1, out var onlyB));
        Assert.AreEqual(0x2002u, onlyB);
        Assert.IsTrue(comparison.TryFindPreviousDifference(0x2002, out var previous));
        Assert.AreEqual(0x2002u, previous);

        comparison.ViewMode = CompareViewMode.DifferencesOnly;
        Assert.HasCount(1, comparison.Rows);
        Assert.AreEqual(CompareByteState.OnlyB, comparison.Rows[0].States[2]);
    }

    [TestMethod]
    public void SparseRegions_CollapseLargeGapsAndRangeViewDoesNotExpandThem()
    {
        var fileA = MemoryImage.FromSegments([new MemorySegment(0x1000, [0x11]), new MemorySegment(0x90000000, [0x22])]);
        var comparison = new CompareViewModel(fileA, MemoryImage.Empty, bytesPerRow: 16);

        Assert.HasCount(3, comparison.Rows);
        Assert.IsTrue(comparison.Rows[1].IsGapMarker);
        Assert.IsTrue(comparison.TryNavigateToAddress(0x50000000, out var gapIndex));
        Assert.AreEqual(1, gapIndex);

        comparison.SetAddressRange(0x1000, 0x90000000);
        comparison.ViewMode = CompareViewMode.AddressRange;
        Assert.HasCount(3, comparison.Rows);
        comparison.ViewMode = CompareViewMode.DifferencesOnly;
        Assert.HasCount(2, comparison.Rows);
        Assert.IsFalse(comparison.Rows.Any(row => row.IsGapMarker));
    }
}
