using HexEditor.Core;
using HexEditor.UI;

namespace HexEditor.Tests;

[TestClass]
public sealed class OrderedMemoryMergeTests
{
    [TestMethod]
    public void LaterFilesWinAtOverlappingAddressesAndTheWholeMergeUndoesOnce()
    {
        var editor = MemoryImage.FromSegments([new MemorySegment(0x1000, [0x10, 0x11])]);
        var first = MemoryImage.FromSegments([new MemorySegment(0x1001, [0x21, 0x22])]);
        var last = MemoryImage.FromSegments([new MemorySegment(0x1001, [0x31])]);
        var editing = new ImageEditingSession();
        editing.Reset(editor);

        var preview = editing.PreviewOrderedMerge([first, last]);
        Assert.AreEqual(1UL, preview.Stages[0].OverlappingByteCount);
        Assert.AreEqual(1UL, preview.Stages[1].OverlappingByteCount);
        editing.ApplyOrderedMerge(preview);

        Assert.IsTrue(editing.CurrentImage.TryGetByte(0x1000, out var baseByte) && baseByte == 0x10);
        Assert.IsTrue(editing.CurrentImage.TryGetByte(0x1001, out var winningByte) && winningByte == 0x31);
        Assert.IsTrue(editing.CurrentImage.TryGetByte(0x1002, out var appendedByte) && appendedByte == 0x22);
        Assert.IsTrue(editing.Undo());
        Assert.AreSame(editor, editing.CurrentImage);
        Assert.IsFalse(editing.CanUndo);
    }

    [TestMethod]
    public void EmptyEditorCanMergeFilesAndUndoBackToEmpty()
    {
        var first = MemoryImage.FromSegments([new MemorySegment(0x1000, [0x10])]);
        var second = MemoryImage.FromSegments([new MemorySegment(0x1000, [0x20])]);
        var editing = new ImageEditingSession();
        var preview = editing.PreviewOrderedMerge([first, second]);

        editing.ApplyOrderedMerge(preview);

        Assert.IsTrue(editing.CurrentImage.TryGetByte(0x1000, out var result) && result == 0x20);
        Assert.IsTrue(editing.Undo());
        Assert.AreSame(MemoryImage.Empty, editing.CurrentImage);
    }
}
