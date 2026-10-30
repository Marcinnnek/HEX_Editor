using HexEditor.Core;
using HexEditor.Formats;
using HexEditor.UI;

namespace HexEditor.Tests;

[TestClass]
public sealed class ImageEditingLimitTests
{
    private const uint MaximumOccupiedBytes = 100_000_000;

    [TestMethod]
    public void Byte_edit_accepts_exact_limit_and_rejects_one_more_atomically()
    {
        var workspace = CreateWorkspace(MaximumOccupiedBytes - 1);
        var original = workspace.Document.CurrentImage;

        workspace.EditByte(uint.MaxValue, 0xA5);
        Assert.AreEqual((ulong)MaximumOccupiedBytes, workspace.Document.CurrentImage.OccupiedByteCount);
        Assert.IsTrue(workspace.Document.CurrentImage.TryGetByte(uint.MaxValue, out var value));
        Assert.AreEqual((byte)0xA5, value);
        Assert.IsTrue(workspace.Document.IsModified);
        Assert.IsTrue(workspace.Document.CanUndo);

        var atLimit = workspace.Document.CurrentImage;
        Assert.Throws<InvalidOperationException>(() => workspace.Document.Editing.EditByte(uint.MaxValue - 1, 0x5A));

        Assert.AreSame(atLimit, workspace.Document.CurrentImage);
        Assert.AreNotSame(original, atLimit);
        Assert.IsTrue(workspace.Document.IsModified);
        Assert.IsTrue(workspace.Document.CanUndo);
        Assert.IsFalse(workspace.Document.CanRedo);
    }

    [TestMethod]
    public void Rejected_byte_edit_preserves_redo_history_and_clean_state()
    {
        var workspace = CreateWorkspace(MaximumOccupiedBytes);
        workspace.EditByte(0, 0x7B, OverlapDecision.OverwriteIncoming);
        Assert.IsTrue(workspace.Undo());
        var original = workspace.Document.CurrentImage;
        Assert.IsFalse(workspace.Document.IsModified);
        Assert.IsFalse(workspace.Document.CanUndo);
        Assert.IsTrue(workspace.Document.CanRedo);

        Assert.Throws<InvalidOperationException>(() => workspace.Document.Editing.EditByte(uint.MaxValue, 0x11));

        Assert.AreSame(original, workspace.Document.CurrentImage);
        Assert.IsFalse(workspace.Document.IsModified);
        Assert.IsFalse(workspace.Document.CanUndo);
        Assert.IsTrue(workspace.Document.CanRedo);
    }

    [TestMethod]
    public void Insert_overwrite_is_allowed_at_limit_but_new_address_is_rejected()
    {
        var workspace = CreateWorkspace(MaximumOccupiedBytes);
        var editing = workspace.Document.Editing;

        editing.InsertBytes(0, [0x7B], OverlapDecision.OverwriteIncoming);
        Assert.AreEqual((ulong)MaximumOccupiedBytes, editing.CurrentImage.OccupiedByteCount);
        Assert.IsTrue(editing.CurrentImage.TryGetByte(0, out var overwritten));
        Assert.AreEqual((byte)0x7B, overwritten);

        var atLimit = editing.CurrentImage;
        var exception = Assert.Throws<InvalidOperationException>(() => editing.InsertBytes(uint.MaxValue, [0x11]));
        Assert.AreSame(atLimit, editing.CurrentImage);
        StringAssert.Contains(exception.Message, "100,000,000");
    }

    [TestMethod]
    public void Paste_that_would_exceed_limit_leaves_image_and_history_unchanged()
    {
        var workspace = CreateWorkspace(MaximumOccupiedBytes);
        var editing = workspace.Document.Editing;
        editing.CopyRange(0, 0);
        Assert.IsFalse(editing.CanUndo);
        Assert.IsFalse(editing.CanRedo);
        var beforePaste = editing.CurrentImage;

        Assert.Throws<InvalidOperationException>(() => editing.PasteAt(uint.MaxValue));

        Assert.AreSame(beforePaste, editing.CurrentImage);
        Assert.IsFalse(editing.CanUndo);
        Assert.IsFalse(editing.CanRedo);
        Assert.AreEqual((ulong)MaximumOccupiedBytes, editing.CurrentImage.OccupiedByteCount);
    }

    [TestMethod]
    public void Fill_keep_existing_rejects_only_when_newly_occupied_bytes_exceed_limit()
    {
        var workspace = CreateWorkspace(MaximumOccupiedBytes);
        var editing = workspace.Document.Editing;
        var original = editing.CurrentImage;

        Assert.Throws<InvalidOperationException>(() => editing.FillRange(MaximumOccupiedBytes - 1, MaximumOccupiedBytes, [0x12], FillPolicy.KeepExisting));
        Assert.AreSame(original, editing.CurrentImage);
        Assert.IsFalse(editing.CanUndo);

        editing.FillRange(0, MaximumOccupiedBytes - 1, [0x12], FillPolicy.OverwriteExisting);
        Assert.AreEqual((ulong)MaximumOccupiedBytes, editing.CurrentImage.OccupiedByteCount);
        Assert.IsTrue(editing.CurrentImage.TryGetByte(1, out var value));
        Assert.AreEqual((byte)0x12, value);
    }

    [TestMethod]
    public async Task Asynchronous_fill_rejects_overflow_before_committing()
    {
        var workspace = CreateWorkspace(MaximumOccupiedBytes);
        var editing = workspace.Document.Editing;
        var original = editing.CurrentImage;

        await Assert.ThrowsExactlyAsync<InvalidOperationException>(() =>
            editing.FillRangeAsync(MaximumOccupiedBytes, MaximumOccupiedBytes, [0x33], FillPolicy.KeepExisting,
                CancellationToken.None, null));

        Assert.AreSame(original, editing.CurrentImage);
        Assert.IsFalse(editing.CanUndo);
        Assert.IsFalse(editing.CanRedo);
    }

    private static EditorDocumentWorkspace CreateWorkspace(uint occupiedBytes)
    {
        var workspace = new EditorDocumentWorkspace();
        workspace.OpenImage(MemoryImage.FromRepeatedRange(0, occupiedBytes - 1, 0xCC),
            null, MemoryImageExportFormat.IntelHex);
        return workspace;
    }
}
