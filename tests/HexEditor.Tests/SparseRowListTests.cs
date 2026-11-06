using HexEditor.Compare;

namespace HexEditor.Tests;

[TestClass]
public sealed class SparseRowListTests
{
    [TestMethod]
    public void OneEmptyRowAcrossRegionBoundaryRemainsVisible()
    {
        var rows = CreateRows([(0xA0FFFFFF, 0xA0FFFFFF), (0xA1000011, 0xA1000011)], 17);

        Assert.HasCount(3, rows);
        Assert.AreEqual(0xA0FFFFFFUL, rows[0].Position.RowAddress);
        Assert.AreEqual(0xA1000000UL, rows[1].Position.RowAddress);
        Assert.IsFalse(rows[1].Position.IsGap);
        Assert.AreEqual(0xA1000011UL, rows[2].Position.RowAddress);
        Assert.IsTrue(rows.TryGetRowIndex(0xA1000010, out var emptyRow));
        Assert.AreEqual(1, emptyRow);
    }

    [TestMethod]
    public void LongGapMarkerExpandsTheLastMissingRowWithoutMaterializingTheGap()
    {
        var rows = CreateRows([(0x1000, 0x1000), (0x2000, 0x2000)], 16);

        Assert.HasCount(3, rows);
        Assert.IsTrue(rows[1].Position.IsGap);
        Assert.AreEqual(0x1010UL, rows[1].Position.GapStartAddress);
        Assert.AreEqual(0x1FF0UL, rows[1].Position.GapEndAddress);

        Assert.IsTrue(rows.TryGetRowIndex(0x1FFF, out var targetRow));
        Assert.HasCount(4, rows);
        Assert.AreEqual(0x1FF0UL, rows[targetRow].Position.RowAddress);
        Assert.IsFalse(rows[targetRow].Position.IsGap);
        Assert.IsTrue(rows.TryGetRowIndex(0x2000, out var occupiedRow));
        Assert.AreEqual(3, occupiedRow);
    }

    [TestMethod]
    public void CompactLayoutOmitsEmptyRowsAndGapMarkers()
    {
        var rows = CreateRows([(0xA0FFFFFF, 0xA0FFFFFF), (0xA1000011, 0xA1000011)],
            17, SparseRowLayout.Compact);

        Assert.HasCount(2, rows);
        Assert.IsFalse(rows.Any(row => row.Position.IsGap));
        Assert.IsFalse(rows.TryGetRowIndex(0xA1000000, out _));
    }

    [TestMethod]
    public void FullAddressRangeHasExactlyOneRowPerEightBytes()
    {
        var rows = CreateRows([(0, uint.MaxValue)], 8);
        var duplicateRows = CreateRows([(0, uint.MaxValue), (0, uint.MaxValue)], 8);

        Assert.AreEqual(536870912, rows.Count);
        Assert.AreEqual(rows.Count, duplicateRows.Count);
    }

    private static SparseRowList<TestRow> CreateRows(
        IEnumerable<(ulong StartAddress, ulong EndAddress)> ranges, int width,
        SparseRowLayout layout = SparseRowLayout.WithGaps) =>
        new(ranges, width, position => new TestRow(position), layout);

    private sealed record TestRow(SparseRowPosition Position);
}
