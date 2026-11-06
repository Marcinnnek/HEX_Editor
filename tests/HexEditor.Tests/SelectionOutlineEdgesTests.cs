using HexEditor.UI;

namespace HexEditor.Tests;

[TestClass]
public sealed class SelectionOutlineEdgesTests
{
    [TestMethod]
    public void AdjacentSelectedBytesHaveOnlyExteriorEdgesAcrossRows()
    {
        var above = Row(0x1000, 0x1001);
        var below = Row(0x1002, 0x1003);
        Assert.AreEqual(new SelectionOutlineEdges(true, false, false, true),
            SelectionOutlineEdges.ForCell(above, null, below, 0, 2, 0x1000, 0x1003));
        Assert.AreEqual(new SelectionOutlineEdges(true, true, false, false),
            SelectionOutlineEdges.ForCell(above, null, below, 1, 2, 0x1000, 0x1003));
        Assert.AreEqual(new SelectionOutlineEdges(false, false, true, true),
            SelectionOutlineEdges.ForCell(below, above, null, 0, 2, 0x1000, 0x1003));
        Assert.AreEqual(new SelectionOutlineEdges(false, true, true, false),
            SelectionOutlineEdges.ForCell(below, above, null, 1, 2, 0x1000, 0x1003));
    }

    [TestMethod]
    public void AddressGapRetainsSeparateOutlines()
    {
        var first = Row(0x1000, 0x1001);
        var other = Row(0x2000, 0x2001);
        Assert.AreEqual(new SelectionOutlineEdges(true, true, true, false),
            SelectionOutlineEdges.ForCell(first, null, other, 1, 2, 0x1000, 0x2001));
    }

    private static HexViewRow Row(params uint[] addresses) => new("", "", null, false, null)
    {
        Cells = addresses.Select(address => new HexByteCell("00", ".", address, ByteHighlightState.None)).ToArray()
    };
}
