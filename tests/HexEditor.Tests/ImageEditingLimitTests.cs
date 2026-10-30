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

    [TestMethod]
    public void Merge_accepts_distinct_bytes_that_reach_the_exact_limit()
    {
        var destination = MemoryImage.FromRepeatedRange(0, MaximumOccupiedBytes - 2, 0xCC);
        var source = MemoryImage.FromSegments([new MemorySegment(MaximumOccupiedBytes - 1, [0x5A])]);
        var workspace = new EditorDocumentWorkspace();
        workspace.OpenImage(destination, null, null);

        workspace.Document.Editing.ApplyMerge(workspace.Document.Editing.PreviewMerge(source),
            MemoryImageMergeChoice.KeepDestination);

        Assert.AreEqual((ulong)MaximumOccupiedBytes, workspace.Document.CurrentImage.OccupiedByteCount);
        Assert.IsTrue(workspace.Document.CurrentImage.TryGetByte(0, out var first));
        Assert.AreEqual((byte)0xCC, first);
        Assert.IsTrue(workspace.Document.CurrentImage.TryGetByte(MaximumOccupiedBytes - 1, out var last));
        Assert.AreEqual((byte)0x5A, last);
        Assert.IsTrue(workspace.Document.CanUndo);
    }

    [TestMethod]
    public void Merge_counts_overlapping_occupied_addresses_once_and_uses_the_selected_value()
    {
        var destination = MemoryImage.FromSegments([new MemorySegment(0x10, [0x11, 0x22])]);
        var source = MemoryImage.FromSegments([new MemorySegment(0x11, [0xA2, 0x33])]);
        var workspace = new EditorDocumentWorkspace();
        workspace.OpenImage(destination, null, null);

        workspace.Document.Editing.ApplyMerge(workspace.Document.Editing.PreviewMerge(source),
            MemoryImageMergeChoice.KeepSource);

        Assert.AreEqual(3UL, workspace.Document.CurrentImage.OccupiedByteCount);
        Assert.IsTrue(workspace.Document.CurrentImage.TryGetByte(0x11, out var overlap));
        Assert.AreEqual((byte)0xA2, overlap);
        Assert.IsTrue(workspace.Document.CurrentImage.TryGetByte(0x12, out var added));
        Assert.AreEqual((byte)0x33, added);
    }

    [TestMethod]
    public void Rejected_merge_preserves_clean_state_and_redo_history()
    {
        var workspace = CreateWorkspace(MaximumOccupiedBytes);
        var editing = workspace.Document.Editing;
        editing.EditByte(0, 0x7B, OverlapDecision.OverwriteIncoming);
        Assert.IsTrue(workspace.Undo());
        var original = editing.CurrentImage;
        var preview = editing.PreviewMerge(MemoryImage.FromSegments([new MemorySegment(uint.MaxValue, [0x11])]));

        Assert.Throws<InvalidOperationException>(() => editing.ApplyMerge(preview, MemoryImageMergeChoice.KeepDestination));

        Assert.AreSame(original, editing.CurrentImage);
        Assert.IsFalse(workspace.Document.IsModified);
        Assert.IsFalse(editing.CanUndo);
        Assert.IsTrue(editing.CanRedo);
    }

    [TestMethod]
    public void Relocation_can_place_the_last_occupied_byte_at_the_highest_address_at_the_limit()
    {
        var image = MemoryImage.FromRanges([
            MemoryImage.FromRepeatedRange(0, MaximumOccupiedBytes - 2, 0xCC).Ranges[0],
            MemoryImage.FromSegments([new MemorySegment(0x20000000, [0x5A])]).Ranges[0]]);
        var workspace = new EditorDocumentWorkspace();
        workspace.OpenImage(image, null, null);

        var preview = workspace.Document.Editing.PrepareRelocation(1, uint.MaxValue);
        Assert.IsTrue(workspace.Document.Editing.ApplyRelocation(preview, RelocationOverlapDecision.Overwrite));

        Assert.AreEqual((ulong)MaximumOccupiedBytes, workspace.Document.CurrentImage.OccupiedByteCount);
        Assert.IsFalse(workspace.Document.CurrentImage.TryGetByte(0x20000000, out _));
        Assert.IsTrue(workspace.Document.CurrentImage.TryGetByte(uint.MaxValue, out var last));
        Assert.AreEqual((byte)0x5A, last);
        Assert.IsTrue(workspace.Undo());
        Assert.AreSame(image, workspace.Document.CurrentImage);
        Assert.IsTrue(workspace.Redo());
        Assert.IsTrue(workspace.Document.CurrentImage.TryGetByte(uint.MaxValue, out _));
    }

    [TestMethod]
    public void Rejected_over_limit_relocation_preserves_image_and_redo_history()
    {
        var image = MemoryImage.FromRanges([
            MemoryImage.FromRepeatedRange(0, MaximumOccupiedBytes, 0xCC).Ranges[0],
            MemoryImage.FromSegments([new MemorySegment(0x20000000, [0x5A])]).Ranges[0]]);
        var workspace = new EditorDocumentWorkspace();
        workspace.OpenImage(image, null, null);
        var editing = workspace.Document.Editing;
        var preview = editing.PrepareRelocation(1, 0x30000000);

        Assert.Throws<InvalidOperationException>(() => editing.ApplyRelocation(preview, RelocationOverlapDecision.Overwrite));

        Assert.AreSame(image, editing.CurrentImage);
        Assert.IsFalse(workspace.Document.IsModified);
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
