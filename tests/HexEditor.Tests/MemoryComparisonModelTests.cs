using HexEditor.Core;
using HexEditor.Compare;

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
    public void ComparisonModel_RefreshUpdatesDifferenceStatusAndRows()
    {
        var left = MemoryImage.FromSegments([new MemorySegment(0x2000, [0x10])]);
        var right = MemoryImage.FromSegments([new MemorySegment(0x2000, [0x20])]);
        var comparison = new MemoryComparisonModel(left, right);

        comparison.Refresh(left, left);

        Assert.AreEqual("No differences", comparison.ComparisonStatus);
        Assert.AreEqual(CompareByteState.Equal, comparison.Rows[0].States[0]);
    }
}
