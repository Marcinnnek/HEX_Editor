using HexEditor.UI;

namespace HexEditor.Tests;

[TestClass]
public sealed class ByteOutlineEdgesTests
{
    [TestMethod]
    public void AdjacentSelectedBytesHaveOnlyExteriorEdgesAcrossRows()
    {
        var above = Row(0x1000, 0x1001);
        var below = Row(0x1002, 0x1003);
        Assert.AreEqual(new ByteOutlineEdges(true, false, false, true),
            ByteOutlineEdges.ForSelection(above, null, below, 0, 2, 0x1000, 0x1003));
        Assert.AreEqual(new ByteOutlineEdges(true, true, false, false),
            ByteOutlineEdges.ForSelection(above, null, below, 1, 2, 0x1000, 0x1003));
        Assert.AreEqual(new ByteOutlineEdges(false, false, true, true),
            ByteOutlineEdges.ForSelection(below, above, null, 0, 2, 0x1000, 0x1003));
        Assert.AreEqual(new ByteOutlineEdges(false, true, true, false),
            ByteOutlineEdges.ForSelection(below, above, null, 1, 2, 0x1000, 0x1003));
    }

    [TestMethod]
    public void AddressGapRetainsSeparateOutlines()
    {
        var first = Row(0x1000, 0x1001);
        var other = Row(0x2000, 0x2001);
        Assert.AreEqual(new ByteOutlineEdges(true, true, true, false),
            ByteOutlineEdges.ForSelection(first, null, other, 1, 2, 0x1000, 0x2001));
    }

    [TestMethod]
    public void AdjacentEditedBytesHaveOnlyExteriorEdgesAcrossRows()
    {
        var above = EditedRow((0x1000, true), (0x1001, true));
        var below = EditedRow((0x1002, true), (0x1003, true));
        Assert.AreEqual(new ByteOutlineEdges(true, false, false, true),
            ByteOutlineEdges.ForEdited(above, null, below, 0, 2));
        Assert.AreEqual(new ByteOutlineEdges(true, true, false, false),
            ByteOutlineEdges.ForEdited(above, null, below, 1, 2));
        Assert.AreEqual(new ByteOutlineEdges(false, false, true, true),
            ByteOutlineEdges.ForEdited(below, above, null, 0, 2));
        Assert.AreEqual(new ByteOutlineEdges(false, true, true, false),
            ByteOutlineEdges.ForEdited(below, above, null, 1, 2));
    }

    [TestMethod]
    public void UneditedBytesAndAddressGapsSplitEditedOutlines()
    {
        var first = EditedRow((0x1000, true), (0x1001, false), (0x1002, true));
        var distant = EditedRow((0x2000, true), (0x2001, true), (0x2002, true));
        Assert.AreEqual(new ByteOutlineEdges(true, true, true, true),
            ByteOutlineEdges.ForEdited(first, null, distant, 0, 3));
        Assert.AreEqual(default, ByteOutlineEdges.ForEdited(first, null, distant, 1, 3));
        Assert.AreEqual(new ByteOutlineEdges(true, true, true, true),
            ByteOutlineEdges.ForEdited(first, null, distant, 2, 3));

        var skippedAddress = EditedRow((0x3000, true), (0x3002, true));
        Assert.AreEqual(new ByteOutlineEdges(true, true, true, true),
            ByteOutlineEdges.ForEdited(skippedAddress, null, null, 0, 2));
        Assert.AreEqual(new ByteOutlineEdges(true, true, true, true),
            ByteOutlineEdges.ForEdited(skippedAddress, null, null, 1, 2));
    }

    private static HexViewRow Row(params uint[] addresses) => new("", "", null, false, null)
    {
        Cells = addresses.Select(address => new HexByteCell("00", ".", address, ByteHighlightState.None)).ToArray()
    };

    private static HexViewRow EditedRow(params (uint Address, bool IsEdited)[] cells) => new("", "", null, false, null)
    {
        Cells = cells.Select(cell => new HexByteCell("00", ".", cell.Address, ByteHighlightState.None,
            IsEdited: cell.IsEdited)).ToArray()
    };
}
