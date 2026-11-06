using HexEditor.Core;
using HexEditor.Formats;
using HexEditor.UI;

namespace HexEditor.Tests;

[TestClass]
public sealed class SystemClipboardPasteTests
{
    [TestMethod]
    public void HexTextPastesExactlyItsByteCountAndPreservesSelectedTail()
    {
        var original = MemoryImage.FromSegments([new MemorySegment(0x1000, [0x10, 0x11, 0x12, 0x13])]);
        var editing = new ImageEditingSession();
        editing.Reset(original);
        var bytes = SelectionClipboardText.ParseHexBytes("AA 0xbb");

        Assert.AreEqual(2L, editing.GetSystemPasteOverlapCount(0x1001, bytes.Length));
        editing.PasteSystemBytes(0x1001, bytes, OverlapDecision.OverwriteIncoming);

        Assert.IsTrue(editing.CurrentImage.TryGetByte(0x1001, out var first) && first == 0xAA);
        Assert.IsTrue(editing.CurrentImage.TryGetByte(0x1002, out var second) && second == 0xBB);
        Assert.IsTrue(editing.CurrentImage.TryGetByte(0x1003, out var tail) && tail == 0x13);
        Assert.IsTrue(editing.Undo());
        Assert.AreSame(original, editing.CurrentImage);
    }

    [TestMethod]
    public void InvalidClipboardTextAndAddressOverflowLeaveImageUnchanged()
    {
        Assert.Throws<FormatException>(() => SelectionClipboardText.ParseHexBytes("AA not-hex"));
        var original = MemoryImage.FromSegments([new MemorySegment(uint.MaxValue, [0x10])]);
        var editing = new ImageEditingSession();
        editing.Reset(original);
        Assert.Throws<ArgumentOutOfRangeException>(() => editing.PasteSystemBytes(uint.MaxValue, [0xAA, 0xBB],
            OverlapDecision.OverwriteIncoming));
        Assert.AreSame(original, editing.CurrentImage);
    }
}
