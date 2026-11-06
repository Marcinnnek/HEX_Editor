using HexEditor.Core;
using HexEditor.UI;

namespace HexEditor.Tests;

[TestClass]
public sealed class SelectedRangeZeroTests
{
    [TestMethod]
    public void ZeroingSelectionPreservesAddressGapsAndUndoesAsOneEdit()
    {
        var image = MemoryImage.FromSegments([
            new MemorySegment(0x1000, [0x11, 0x12]),
            new MemorySegment(0x1004, [0x14])]);
        var editing = new ImageEditingSession();
        editing.Reset(image);

        editing.ZeroOccupiedRange(0x1001, 0x1004);

        Assert.IsTrue(editing.CurrentImage.TryGetByte(0x1000, out var untouched) && untouched == 0x11);
        Assert.IsTrue(editing.CurrentImage.TryGetByte(0x1001, out var first) && first == 0);
        Assert.IsFalse(editing.CurrentImage.TryGetByte(0x1002, out _));
        Assert.IsFalse(editing.CurrentImage.TryGetByte(0x1003, out _));
        Assert.IsTrue(editing.CurrentImage.TryGetByte(0x1004, out var last) && last == 0);
        Assert.IsTrue(editing.Undo());
        Assert.AreSame(image, editing.CurrentImage);
    }
}
