using HexEditor.Core;
using HexEditor.UI;

namespace HexEditor.Tests;

[TestClass]
public sealed class RowViewportStateTests
{
    [TestMethod]
    public void SameImageAndNewRowWidthKeepTheFirstVisibleAddress()
    {
        var image = MemoryImage.FromPatternRange(0, 999, [0xA5]);
        var viewport = new RowViewportState();
        viewport.SetVisibleRowCount(10);
        viewport.ReplaceRows(new HexImageViewModel(image, bytesPerRow: 8).Rows);
        viewport.MoveTo(20);

        viewport.ReplaceRows(new HexImageViewModel(image, bytesPerRow: 16).Rows);

        Assert.AreEqual(10, viewport.FirstRowIndex);
        Assert.AreEqual(20, viewport.EndExclusive);
    }

    [TestMethod]
    public void ResizingAndReflowingAtTheEndKeepTheLastRowVisible()
    {
        var image = MemoryImage.FromPatternRange(0, 999, [0xA5]);
        var viewport = new RowViewportState();
        viewport.SetVisibleRowCount(10);
        viewport.ReplaceRows(new HexImageViewModel(image, bytesPerRow: 8).Rows);
        viewport.MoveTo(int.MaxValue);

        viewport.SetVisibleRowCount(15);
        Assert.AreEqual(110, viewport.FirstRowIndex);
        Assert.AreEqual(125, viewport.EndExclusive);

        var reflowed = new HexImageViewModel(image, bytesPerRow: 16).Rows;
        viewport.ReplaceRows(reflowed);
        Assert.AreEqual(reflowed.Count - 15, viewport.FirstRowIndex);
        Assert.AreEqual(reflowed.Count, viewport.EndExclusive);
    }

    [TestMethod]
    public void DifferentImageStartsAtItsFirstRow()
    {
        var viewport = new RowViewportState();
        viewport.SetVisibleRowCount(5);
        viewport.ReplaceRows(new HexImageViewModel(MemoryImage.FromPatternRange(0, 999, [0xA5]), 8).Rows);
        viewport.MoveTo(int.MaxValue);

        viewport.ReplaceRows(new HexImageViewModel(MemoryImage.FromPatternRange(0x1000, 0x1FFF, [0x5A]), 8).Rows);

        Assert.AreEqual(0, viewport.FirstRowIndex);
        Assert.AreEqual(5, viewport.EndExclusive);
    }

    [TestMethod]
    public void EmptyAndShortSourcesClampTheVisibleWindow()
    {
        var viewport = new RowViewportState();
        viewport.SetVisibleRowCount(10);
        viewport.ReplaceRows(new HexImageViewModel(MemoryImage.Empty, 8).Rows);
        viewport.MoveTo(100);
        Assert.AreEqual(0, viewport.FirstRowIndex);
        Assert.AreEqual(0, viewport.EndExclusive);

        viewport.ReplaceRows(new HexImageViewModel(MemoryImage.FromPatternRange(0, 7, [0xA5]), 8).Rows);
        viewport.MoveTo(100);
        Assert.AreEqual(0, viewport.MaximumStart);
        Assert.AreEqual(1, viewport.EndExclusive);
    }

    [TestMethod]
    public void GapMarkerUsesProportionalPositionWhenRowWidthChanges()
    {
        var image = MemoryImage.FromSegments([
            new MemorySegment(0x1000, Enumerable.Repeat((byte)0xA5, 64).ToArray()),
            new MemorySegment(0x2000, [0x5A])]);
        var viewport = new RowViewportState();
        viewport.SetVisibleRowCount(1);
        viewport.ReplaceRows(new HexImageViewModel(image, bytesPerRow: 8).Rows);
        viewport.MoveTo(8);

        viewport.ReplaceRows(new HexImageViewModel(image, bytesPerRow: 16).Rows);

        Assert.AreEqual(4, viewport.FirstRowIndex);
    }

    [TestMethod]
    public void ExpandingAHiddenGapKeepsTheViewportAtTheEnd()
    {
        var image = MemoryImage.FromSegments([
            new MemorySegment(0x1000, [0xA5]),
            new MemorySegment(0x2000, [0x5A])]);
        var model = new HexImageViewModel(image, bytesPerRow: 16);
        var viewport = new RowViewportState();
        viewport.SetVisibleRowCount(1);
        viewport.ReplaceRows(model.Rows);
        viewport.MoveTo(int.MaxValue);

        Assert.IsTrue(model.TryNavigateToAddress(0x1FF0, out _));
        viewport.SetVisibleRowCount(1);

        Assert.AreEqual(model.Rows.Count - 1, viewport.FirstRowIndex);
        Assert.AreEqual(model.Rows.Count, viewport.EndExclusive);
    }
}
