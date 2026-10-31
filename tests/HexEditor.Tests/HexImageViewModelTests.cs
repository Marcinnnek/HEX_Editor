using System.Text;
using HexEditor.Core;
using HexEditor.Formats;
using HexEditor.UI;
using HexEditor.Tools;

namespace HexEditor.Tests;

[TestClass]
public sealed class HexImageViewModelTests
{
    [TestMethod]
    public void SelectionStatus_TracksInclusiveAddressRangeAndClearsToZeroSelection()
    {
        var model = new MainViewModel();
        Assert.IsNull(model.EditorStatus.CurrentAddress);
        Assert.AreEqual(0, model.EditorStatus.SelectionLength);

        model.EditorStatus.SetSelection(0x1234, 0x123F);
        Assert.AreEqual(0x1234u, model.EditorStatus.CurrentAddress);
        Assert.AreEqual(12L, model.EditorStatus.SelectionLength);
        Assert.AreEqual("00001234", model.EditorStatus.CurrentAddressText);

        model.EditorStatus.ClearSelection();
        Assert.IsNull(model.EditorStatus.CurrentAddress);
        Assert.AreEqual(0, model.EditorStatus.SelectionLength);
        Assert.AreEqual("—", model.EditorStatus.CurrentAddressText);
    }

    [TestMethod]
    public void HexRows_ExposeEachAbsoluteByteAddressForSingleOrShiftRangeSelection()
    {
        var view = new HexImageViewModel(MemoryImage.FromSegments([new MemorySegment(0x1234, [0xAA, 0xBB])]), bytesPerRow: 8);

        var row = view.Rows[0];

        Assert.HasCount(8, row.Cells);
        Assert.AreEqual(0x1230u, row.Cells[0].Address);
        Assert.AreEqual(0x1234u, row.Cells[4].Address);
        Assert.AreEqual("AA", row.Cells[4].Hex);
        Assert.AreEqual("BB", row.Cells[5].Hex);
        Assert.AreEqual(0x1237u, row.Cells[7].Address);
    }

    [DataRow(1)]
    [DataRow(3)]
    [DataRow(8)]
    [DataRow(15)]
    [DataRow(16)]
    [DataRow(17)]
    [DataRow(24)]
    [DataRow(32)]
    [DataRow(80)]
    [DataRow(128)]
    [TestMethod]
    public void Editor_RendersCustomWidthsOnGlobalRowsAcrossSparseAndFinalAddresses(int bytesPerRow)
    {
        var image = MemoryImage.FromSegments([
            new MemorySegment(0x00000002, [0xA1, 0xA2]),
            new MemorySegment(0xFFFFFFFF, [0xFE])]);
        var view = new HexImageViewModel(image, bytesPerRow);

        Assert.IsTrue(view.TryNavigateToAddress(0x00000002, out var firstIndex));
        var first = view.Rows[firstIndex];
        Assert.AreEqual((2 / bytesPerRow * bytesPerRow).ToString("X8"), first.Address);
        Assert.AreEqual(bytesPerRow, first.Cells.Count);
        Assert.AreEqual("A1", first.Cells[2 % bytesPerRow].Hex);
        Assert.IsTrue(first.Cells.Take(2 % bytesPerRow).All(cell => cell.Hex.Length == 0));

        Assert.IsTrue(view.TryNavigateToAddress(0xFFFFFFFF, out var lastIndex));
        var last = view.Rows[lastIndex];
        var finalRegionStart = 0xFF000000u;
        var lastRowStart = (uint)((ulong)finalRegionStart +
            (((ulong)uint.MaxValue - finalRegionStart) / (uint)bytesPerRow * (uint)bytesPerRow));
        Assert.AreEqual(lastRowStart.ToString("X8"), last.Address);
        var lastOffset = (int)(uint.MaxValue - lastRowStart);
        Assert.AreEqual("FE", last.Cells[lastOffset].Hex);
        Assert.AreEqual(uint.MaxValue, last.Cells[lastOffset].Address);
        Assert.IsTrue(last.Cells.Skip(lastOffset + 1).All(cell => cell.Address is null));
    }

    [TestMethod]
    public void Editor_CombinesSparseBlocksWithinTheirSharedGlobalRow()
    {
        var image = MemoryImage.FromSegments([
            new MemorySegment(0xA0000002, [0xA1]),
            new MemorySegment(0xA0000008, [0xB2])]);
        var view = new HexImageViewModel(image, bytesPerRow: 17);

        Assert.IsTrue(view.TryNavigateToAddress(0xA0000002, out var firstIndex));
        Assert.IsTrue(view.TryNavigateToAddress(0xA0000008, out var secondIndex));
        Assert.AreEqual(firstIndex, secondIndex);
        var row = view.Rows[firstIndex];
        Assert.AreEqual("A0000000", row.Address);
        Assert.AreEqual("A1", row.Cells[2].Hex);
        Assert.IsTrue(row.Cells.Take(2).All(cell => cell.Hex.Length == 0));
        Assert.IsTrue(row.Cells.Skip(3).Take(5).All(cell => cell.Hex.Length == 0));
        Assert.AreEqual("B2", row.Cells[8].Hex);
    }

    [TestMethod]
    public void Editor_UsesOneRowGridAcrossSourceRecordLikeSegmentStarts()
    {
        var image = MemoryImage.FromSegments([
            new MemorySegment(0x0002, [0xA1]),
            new MemorySegment(0x0012, [0xA2]),
            new MemorySegment(0x0022, [0xA3])]);
        var view = new HexImageViewModel(image, bytesPerRow: 17);

        Assert.IsTrue(view.TryNavigateToAddress(0x0002, out var firstIndex));
        Assert.AreEqual("00000000", view.Rows[firstIndex].Address);
        Assert.AreEqual("A1", view.Rows[firstIndex].Cells[2].Hex);
        Assert.IsTrue(view.TryNavigateToAddress(0x0012, out var secondIndex));
        Assert.AreEqual("00000011", view.Rows[secondIndex].Address);
        Assert.AreEqual("A2", view.Rows[secondIndex].Cells[1].Hex);
        Assert.IsTrue(view.TryNavigateToAddress(0x0022, out var thirdIndex));
        Assert.AreEqual("00000022", view.Rows[thirdIndex].Address);
        Assert.AreEqual("A3", view.Rows[thirdIndex].Cells[0].Hex);
    }

    [TestMethod]
    public void Editor_ResetsGridOriginAtEach16MiBRegionBoundary()
    {
        var image = MemoryImage.FromSegments([
            new MemorySegment(0xA0001002, [0xC3]),
            new MemorySegment(0xA0FFFFFF, [0xA1]),
            new MemorySegment(0xA1000002, [0xB2])]);
        var view = new HexImageViewModel(image, bytesPerRow: 17);

        Assert.IsTrue(view.TryNavigateToAddress(0xA0FFFFFF, out var beforeBoundaryIndex));
        Assert.IsTrue(view.TryNavigateToAddress(0xA1000002, out var afterBoundaryIndex));
        Assert.AreEqual("A0FFFFFF", view.Rows[beforeBoundaryIndex].Address);
        Assert.AreEqual("A1000000", view.Rows[afterBoundaryIndex].Address);
        Assert.AreEqual("A1", view.Rows[beforeBoundaryIndex].Cells[0].Hex);
        Assert.IsTrue(view.Rows[beforeBoundaryIndex].Cells.Skip(1).All(cell => cell.Address is null));
        Assert.AreEqual("B2", view.Rows[afterBoundaryIndex].Cells[2].Hex);

        Assert.IsTrue(view.TryNavigateToAddress(0xA0001002, out var interiorRegionIndex));
        Assert.AreEqual("A0001001", view.Rows[interiorRegionIndex].Address);
        Assert.AreEqual("C3", view.Rows[interiorRegionIndex].Cells[1].Hex);
    }

    [TestMethod]
    public void Editor_WidthChangePreservesCurrentAddressAndSelection()
    {
        var model = new MainViewModel(MemoryImage.FromSegments([new MemorySegment(0x1000, [0x10, 0x20, 0x30]) ]));
        model.EditorStatus.SetSelection(0x1001, 0x1002);

        model.ActiveBytesPerRow = 3;

        Assert.AreEqual(3, model.ActiveBytesPerRow);
        Assert.AreEqual(0x1001u, model.EditorStatus.CurrentAddress);
        Assert.AreEqual(2, model.EditorStatus.SelectionLength);
        Assert.IsTrue(model.TryNavigateToAddress(0x1001, out var rowIndex));
        Assert.AreEqual((0x1001 / 3 * 3).ToString("X8"), model.ImageView.Rows[rowIndex].Address);
    }

    [TestMethod]
    [DataRow(0)]
    [DataRow(129)]
    public void Editor_RejectsRowWidthsOutsideOneThrough128(int bytesPerRow)
    {
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new HexImageViewModel(MemoryImage.Empty, bytesPerRow));
    }

    [TestMethod]
    public void EditedByteHighlightTracksCurrentOccupiedValuesAgainstLastSavedImage()
    {
        var workspace = new EditorDocumentWorkspace();
        workspace.OpenImage(MemoryImage.FromSegments([new MemorySegment(0x10, [0x10, 0x20])]), null, null);
        var document = workspace.Document;
        workspace.EditByte(0x10, 0x11, OverlapDecision.OverwriteIncoming);
        workspace.EditByte(0x12, 0x30, OverlapDecision.OverwriteIncoming);

        Assert.IsTrue(document.IsEditedByte(0x10));
        Assert.IsTrue(document.IsEditedByte(0x12));
        Assert.IsFalse(document.IsEditedByte(0x11));
        Assert.IsFalse(document.IsEditedByte(0x13));
        Assert.IsTrue(workspace.Undo());
        Assert.IsFalse(document.IsEditedByte(0x12));
        Assert.IsTrue(document.IsEditedByte(0x10));
        Assert.IsTrue(workspace.Undo());
        Assert.IsFalse(document.IsEditedByte(0x10));

        workspace.EditByte(0x10, 0x33, OverlapDecision.OverwriteIncoming);
        var editedCell = new HexImageViewModel(document.CurrentImage, bytesPerRow: 8, document: document)
            .Rows.Single(row => !row.IsGapMarker).Cells.Single(cell => cell.Address == 0x10);
        Assert.IsTrue(editedCell.IsEdited);
        Assert.IsTrue(workspace.Undo());
        Assert.IsFalse(document.IsEditedByte(0x10));

        workspace.EditByte(0x10, 0x44, OverlapDecision.OverwriteIncoming);
        var savedPath = Path.Combine(Path.GetTempPath(), $"hex-editor-edited-{Guid.NewGuid():N}.hex");
        try
        {
            workspace.SaveAs(savedPath, MemoryImageExportFormat.IntelHex, overwriteExisting: false);
            Assert.IsFalse(document.IsEditedByte(0x10));
        }
        finally { if (File.Exists(savedPath)) File.Delete(savedPath); }
    }

    [TestMethod]
    public void Editor_RendersSparseBytesAtBoth32BitEndsAndKeepsEditsUndoable()
    {
        var workspace = new EditorDocumentWorkspace();
        var image = MemoryImage.FromSegments([
            new MemorySegment(0x00000000, [0xA5]),
            new MemorySegment(0xFFFFFFFF, [0xA5])]);
        workspace.OpenImage(image, null, null);
        var view = new HexImageViewModel(workspace.Document.CurrentImage, bytesPerRow: 8);

        Assert.AreEqual(2UL, workspace.Document.CurrentImage.OccupiedByteCount);
        Assert.IsTrue(view.TryNavigateToAddress(0x00000000, out var firstRowIndex));
        Assert.AreEqual("00000000", view.Rows[firstRowIndex].Address);
        Assert.AreEqual("A5", view.Rows[firstRowIndex].Cells[0].Hex);
        Assert.IsTrue(view.TryNavigateToAddress(0xFFFFFFFF, out var finalRowIndex));
        Assert.AreEqual("FFFFFFF8", view.Rows[finalRowIndex].Address);
        Assert.AreEqual("A5", view.Rows[finalRowIndex].Cells[7].Hex);

        workspace.EditByte(0xFFFFFFFF, 0x5A, OverlapDecision.OverwriteIncoming);
        Assert.AreEqual(2UL, workspace.Document.CurrentImage.OccupiedByteCount);
        Assert.IsTrue(workspace.Document.CurrentImage.TryGetByte(0x00000000, out var first));
        Assert.AreEqual((byte)0xA5, first);
        Assert.IsTrue(workspace.Document.CurrentImage.TryGetByte(0xFFFFFFFF, out var last));
        Assert.AreEqual((byte)0x5A, last);
        Assert.IsTrue(workspace.Undo());
        Assert.AreSame(image, workspace.Document.CurrentImage);
    }

    [TestMethod]
    public void EditorAndComparison_ShareSparseLayoutAndCompareCanNavigateIntoGaps()
    {
        var image = MemoryImage.FromSegments([
            new MemorySegment(0x1001, [0xAA, 0xBB]),
            new MemorySegment(0x1020, [0xCC])]);
        var editor = new HexImageViewModel(image, bytesPerRow: 8);
        var comparison = new CompareViewModel(image, image, bytesPerRow: 8);

        Assert.HasCount(editor.Rows.Count, comparison.Rows);
        for (var i = 0; i < editor.Rows.Count; i++)
        {
            Assert.AreEqual(editor.Rows[i].IsGapMarker, comparison.Rows[i].IsGapMarker);
            Assert.AreEqual(editor.Rows[i].Address, comparison.Rows[i].Address);
        }

        foreach (var address in new uint[] { 0x1001, 0x1002, 0x1020 })
        {
            Assert.IsTrue(editor.TryNavigateToAddress(address, out var editorIndex));
            Assert.IsTrue(comparison.TryNavigateToAddress(address, out var comparisonIndex));
            Assert.AreEqual(editorIndex, comparisonIndex);
        }

        Assert.IsTrue(comparison.TryNavigateToAddress(0x1010, out var gapIndex));
        Assert.IsFalse(comparison.Rows[gapIndex].IsGapMarker);
        Assert.AreEqual("00001010", comparison.Rows[gapIndex].Address);
        Assert.IsLessThanOrEqualTo(comparison.Rows.Count, 5);
    }

    [TestMethod]
    public void NavigatingIntoCollapsedGap_ReindexesRowsAndKeepsFollowingSegmentAddressable()
    {
        var image = MemoryImage.FromSegments([
            new MemorySegment(0x1000, [0xAA]),
            new MemorySegment(0x90000000, [0xBB])]);
        var editor = new HexImageViewModel(image, bytesPerRow: 16);

        Assert.HasCount(3, editor.Rows);
        Assert.IsTrue(editor.TryNavigateToAddress(0x50000000, out var expandedRowIndex));

        Assert.AreEqual("50000000", editor.Rows[expandedRowIndex].Address);
        Assert.HasCount(5, editor.Rows);
        Assert.IsTrue(editor.TryNavigateToAddress(0x90000000, out var finalSegmentRowIndex));
        Assert.AreEqual("90000000", editor.Rows[finalSegmentRowIndex].Address);
        Assert.AreEqual(4, finalSegmentRowIndex);
    }

    [TestMethod]
    public void EditorStatus_ReportsSourceFormatAndModifiedState()
    {
        var model = new MainViewModel();
        model.LoadImage(MemoryImage.FromSegments([new MemorySegment(0x10, [0xAA])]), "input.hex", MemoryImageExportFormat.IntelHex);

        Assert.AreEqual("Intel HEX", model.EditorStatus.ImageFormatText);
        Assert.AreEqual("Saved", model.EditorStatus.ModifiedStateText);
        model.EditByte(0x10, 0xBB, OverlapDecision.OverwriteIncoming);
        Assert.AreEqual("Modified", model.EditorStatus.ModifiedStateText);
        Assert.IsTrue(model.Undo());
        Assert.AreEqual("Saved", model.EditorStatus.ModifiedStateText);
    }

    [TestMethod]
    public void Edits_PreserveSparseOffsetsAndSupportUndoRedo()
    {
        var model = new MainViewModel();
        var image = MemoryImage.FromSegments([
            new MemorySegment(0x1000, [0x10, 0x20]),
            new MemorySegment(0x1004, [0x40])]);
        model.LoadImage(image, "input.hex", MemoryImageExportFormat.IntelHex);

        model.CopyRange(0x1000, 0x1004);
        model.DeleteRange(0x1000, 0x1004);
        Assert.IsEmpty(model.CurrentImage.Segments);
        model.PasteAt(0x2000, OverlapDecision.OverwriteIncoming);

        Assert.IsTrue(model.CurrentImage.TryGetByte(0x2000, out var first));
        Assert.AreEqual((byte)0x10, first);
        Assert.IsTrue(model.CurrentImage.TryGetByte(0x2001, out var second));
        Assert.AreEqual((byte)0x20, second);
        Assert.IsFalse(model.CurrentImage.TryGetByte(0x2002, out _));
        Assert.IsTrue(model.CurrentImage.TryGetByte(0x2004, out var last));
        Assert.AreEqual((byte)0x40, last);
        Assert.IsTrue(model.IsModified);

        Assert.IsTrue(model.Undo());
        Assert.IsEmpty(model.CurrentImage.Segments);
        Assert.IsTrue(model.Redo());
        Assert.IsTrue(model.CurrentImage.TryGetByte(0x2004, out _));
    }

    [TestMethod]
    public void Insert_RequiresAnExplicitDecisionForOccupiedAddresses()
    {
        var model = new MainViewModel();
        model.LoadImage(MemoryImage.FromSegments([new MemorySegment(0x10, [0xAA])]), null, null);

        var preview = model.PreviewInsert(0x10, [0xBB, 0xCC]);
        Assert.AreEqual(1, preview.OverlapCount);
        Assert.IsNull(preview.Apply(null));
        Assert.IsTrue(preview.Apply(OverlapDecision.KeepExisting)!.TryGetByte(0x10, out var kept));
        Assert.AreEqual((byte)0xAA, kept);
        Assert.IsTrue(preview.Apply(OverlapDecision.OverwriteIncoming)!.TryGetByte(0x10, out var replaced));
        Assert.AreEqual((byte)0xBB, replaced);
        Assert.IsTrue(preview.Apply(OverlapDecision.OverwriteIncoming)!.TryGetByte(0x11, out var added));
        Assert.AreEqual((byte)0xCC, added);
        Assert.AreEqual(1, model.PreviewInsert(0x10, [0xAA]).OverlapCount, "An identical occupied value still requires an explicit decision.");
    }

    [TestMethod]
    public void MergePreview_CombinesNonOverlappingBytesAndSummarizesEveryOverlap()
    {
        var destination = MemoryImage.FromSegments([new MemorySegment(0x1000, [0xA1, 0x00, 0xF0])]);
        var source = MemoryImage.FromSegments([new MemorySegment(0x1001, [0x00, 0xF1, 0x80])]);
        var model = new MainViewModel();
        model.LoadImage(destination, null, null);

        var preview = model.PreviewMerge(source);

        Assert.HasCount(1, preview.Overlaps);
        Assert.AreEqual(0x1001u, preview.Overlaps[0].StartAddress);
        Assert.AreEqual(0x1002u, preview.Overlaps[0].EndAddress);
        Assert.AreEqual(2L, preview.Overlaps[0].TotalByteCount);
        Assert.AreEqual(1L, preview.Overlaps[0].IdenticalByteCount);
        Assert.AreEqual(1L, preview.Overlaps[0].DifferentByteCount);

        var keepDestination = preview.Resolve(MemoryImageMergeChoice.KeepDestination)!;
        Assert.IsTrue(keepDestination.TryGetByte(0x1000, out var before));
        Assert.AreEqual((byte)0xA1, before);
        Assert.IsTrue(keepDestination.TryGetByte(0x1001, out var identical));
        Assert.AreEqual((byte)0x00, identical);
        Assert.IsTrue(keepDestination.TryGetByte(0x1002, out var conflicting));
        Assert.AreEqual((byte)0xF0, conflicting);
        Assert.IsTrue(keepDestination.TryGetByte(0x1003, out var after));
        Assert.AreEqual((byte)0x80, after);

        var keepSource = preview.Resolve(MemoryImageMergeChoice.KeepSource)!;
        Assert.IsTrue(keepSource.TryGetByte(0x1002, out var sourceValue));
        Assert.AreEqual((byte)0xF1, sourceValue);
    }

    [TestMethod]
    public void MergePreview_AppliesDefaultChoiceAndAddressRangeOverrides()
    {
        var destination = MemoryImage.FromSegments([new MemorySegment(0x2000, [0x10, 0x20, 0x30, 0x40])]);
        var source = MemoryImage.FromSegments([new MemorySegment(0x2000, [0xA0, 0xB0, 0xC0, 0xD0])]);
        var preview = new MemoryImageMergePreview(destination, source);

        var merged = preview.Resolve(
            MemoryImageMergeChoice.KeepDestination,
            [new MemoryImageMergeRangeOverride(0x2001, 0x2002, MemoryImageMergeChoice.KeepSource)])!;

        CollectionAssert.AreEqual(new byte[] { 0x10, 0xB0, 0xC0, 0x40 }, merged.Segments.Single().Data.ToArray());
    }

    [TestMethod]
    public void ApplyingMerge_CanCancelAndCreatesOneUndoableEditThatRefreshesSegments()
    {
        var destination = MemoryImage.FromSegments([
            new MemorySegment(0x10, [0x11]),
            new MemorySegment(0x20, [0x22])]);
        var source = MemoryImage.FromSegments([
            new MemorySegment(0x10, [0xAA]),
            new MemorySegment(0x30, [0x33])]);
        var model = new MainViewModel();
        model.LoadImage(destination, null, null);
        var preview = model.PreviewMerge(source);

        model.ApplyMerge(preview, null);
        Assert.AreSame(destination, model.CurrentImage);
        Assert.IsFalse(model.CanUndo);
        Assert.HasCount(2, model.Segments);

        model.ApplyMerge(preview, MemoryImageMergeChoice.KeepSource);
        Assert.IsTrue(model.IsModified);
        Assert.IsTrue(model.CanUndo);
        Assert.HasCount(3, model.Segments);
        Assert.AreEqual("00000010", model.Segments[0].StartAddress);
        Assert.AreEqual("00000010", model.Segments[0].EndAddress);
        Assert.IsTrue(model.CurrentImage.TryGetByte(0x10, out var mergedByte));
        Assert.AreEqual((byte)0xAA, mergedByte);

        Assert.IsTrue(model.Undo());
        Assert.AreSame(destination, model.CurrentImage);
        Assert.HasCount(2, model.Segments);
        Assert.IsTrue(model.Redo());
        Assert.HasCount(3, model.Segments);
        Assert.IsTrue(model.CurrentImage.TryGetByte(0x30, out var addedByte));
        Assert.AreEqual((byte)0x33, addedByte);
    }

    [TestMethod]
    public void ApplyingMerge_RejectsPreviewWhenDestinationChangedAfterReview()
    {
        var model = new MainViewModel();
        model.LoadImage(MemoryImage.FromSegments([new MemorySegment(0x10, [0x11])]), null, null);
        var preview = model.PreviewMerge(MemoryImage.FromSegments([new MemorySegment(0x10, [0x22])]));
        model.EditByte(0x10, 0x33, OverlapDecision.OverwriteIncoming);

        Assert.Throws<InvalidOperationException>(() => model.ApplyMerge(preview, MemoryImageMergeChoice.KeepSource));
        Assert.IsTrue(model.CurrentImage.TryGetByte(0x10, out var retained));
        Assert.AreEqual((byte)0x33, retained);
    }

    [TestMethod]
    public void FillAndDelete_ChangeOccupancyWithoutShiftingLaterAddresses()
    {
        var model = new MainViewModel();
        model.LoadImage(MemoryImage.FromSegments([new MemorySegment(0x1000, [0x11, 0x22, 0x33, 0x44, 0x55])]), null, null);

        model.FillRange(0x1001, 0x1006, [0xAA, 0xBB], FillPolicy.OverwriteExisting);
        CollectionAssert.AreEqual(new byte[] { 0x11, 0xAA, 0xBB, 0xAA, 0xBB, 0xAA, 0xBB },
            model.CurrentImage.Segments.Single().Data.ToArray());
        model.DeleteRange(0x1002, 0x1004);

        Assert.HasCount(2, model.CurrentImage.Segments);
        Assert.AreEqual(0x1000u, model.CurrentImage.Segments[0].StartAddress);
        Assert.AreEqual(0x1001u, model.CurrentImage.Segments[0].EndAddress);
        Assert.AreEqual(0x1005u, model.CurrentImage.Segments[1].StartAddress);
        Assert.IsTrue(model.CurrentImage.TryGetByte(0x1005, out var later));
        Assert.AreEqual((byte)0xAA, later);
    }

    [TestMethod]
    public void FillRange_KeepsOccupiedBytesAndAnchorsPatternAtRangeStart()
    {
        var model = new MainViewModel();
        model.LoadImage(MemoryImage.FromSegments([new MemorySegment(0x10, [0xA0, 0xA1, 0xA2, 0xA3])]), null, null);

        model.FillRange(0x0F, 0x14, [0x10, 0x20]);

        for (uint address = 0x0F; address <= 0x14; address++)
        {
            Assert.IsTrue(model.CurrentImage.TryGetByte(address, out var value));
            var expected = address is >= 0x10 and <= 0x13
                ? (byte)(0xA0 + address - 0x10)
                : (address % 2 == 1 ? (byte)0x10 : (byte)0x20);
            Assert.AreEqual(expected, value, $"Unexpected fill at {address:X8}.");
        }
        Assert.IsTrue(model.Undo());
        Assert.IsFalse(model.CurrentImage.TryGetByte(0x0F, out _));
    }

    [TestMethod]
    public async Task FillRangeAsync_FillsRequestedRangeWithinLimitAsOneUndoablePatternBackedChange()
    {
        var original = MemoryImage.FromSegments([
            new MemorySegment(0, [0xE0]),
            new MemorySegment(0x80000000, [0xE1]),
            new MemorySegment(uint.MaxValue, [0xE2])]);
        var model = new MainViewModel();
        model.LoadImage(original, null, null);
        var progress = new RecordingFillProgress();

        await model.FillRangeAsync(0, 0x000FFFFF, [0x11, 0x22, 0x33],
            FillPolicy.OverwriteExisting, CancellationToken.None, progress);

        Assert.AreEqual(1_048_578UL, model.CurrentImage.OccupiedByteCount);
        AssertByte(model.CurrentImage, 0, 0x11);
        AssertByte(model.CurrentImage, 0x00080000, 0x33);
        AssertByte(model.CurrentImage, 0x000FFFFF, 0x11);
        AssertByte(model.CurrentImage, 0x80000000, 0xE1);
        AssertByte(model.CurrentImage, uint.MaxValue, 0xE2);
        Assert.IsTrue(model.CanUndo);
        Assert.IsTrue(model.Undo());
        Assert.AreSame(original, model.CurrentImage);
        Assert.IsTrue(model.Redo());
        AssertByte(model.CurrentImage, 0x000FFFFF, 0x11);
        Assert.IsGreaterThan(0, progress.Updates.Count);
    }

    [TestMethod]
    public async Task FillRangeAsync_CancellationLeavesTheImageAndUndoHistoryUnchanged()
    {
        var original = MemoryImage.FromSegments([new MemorySegment(0x10, [0xA5])]);
        var model = new MainViewModel();
        model.LoadImage(original, null, null);
        using var cancellation = new CancellationTokenSource();
        var progress = new CancellingFillProgress(cancellation);

        await Assert.ThrowsExactlyAsync<OperationCanceledException>(async () =>
            await model.FillRangeAsync(0, 1_000_000, [0x11], FillPolicy.OverwriteExisting,
                cancellation.Token, progress));

        Assert.AreSame(original, model.CurrentImage);
        Assert.IsFalse(model.CanUndo);
    }

    private static void AssertByte(MemoryImage image, uint address, byte expected)
    {
        Assert.IsTrue(image.TryGetByte(address, out var actual), $"Address {address:X8} should be occupied.");
        Assert.AreEqual(expected, actual, $"Unexpected value at address {address:X8}.");
    }

    private sealed class RecordingFillProgress : IProgress<MemoryImageFileProgress>
    {
        public List<MemoryImageFileProgress> Updates { get; } = [];
        public void Report(MemoryImageFileProgress value) => Updates.Add(value);
    }

    private sealed class CancellingFillProgress(CancellationTokenSource cancellation) : IProgress<MemoryImageFileProgress>
    {
        public void Report(MemoryImageFileProgress value) => cancellation.Cancel();
    }

    [TestMethod]
    public void FillAllRegion_UsesLowestAndHighestOccupiedAddressesAndIsNullForEmptyImage()
    {
        var model = new MainViewModel();
        Assert.IsNull(model.GetFillAllRegion());
        model.LoadImage(MemoryImage.FromSegments([
            new MemorySegment(0x10, [0xAA]), new MemorySegment(0x20, [0xBB])]), null, null);

        Assert.AreEqual((0x10u, 0x20u), model.GetFillAllRegion());
    }

    [TestMethod]
    public void RangeEdits_CancelOverlapWithoutMutationAndFillAsOneUndoableChange()
    {
        var original = MemoryImage.FromSegments([new MemorySegment(0x1000, [0x11, 0x22])]);
        var model = new MainViewModel();
        model.LoadImage(original, null, null);

        var overlap = model.PreviewInsert(0x1000, [0x11]);
        Assert.AreEqual(1, overlap.OverlapCount, "Identical occupied bytes still require an explicit decision.");
        model.InsertBytes(0x1000, [0x11], OverlapDecision.Cancel);
        Assert.AreSame(original, model.CurrentImage);
        Assert.IsFalse(model.CanUndo);

        model.FillRange(0x1002, 0x1004, [0xA5, 0x5A]);
        Assert.IsTrue(model.CanUndo);
        Assert.AreEqual(0x1004u, model.CurrentImage.Segments.Single().EndAddress);
        Assert.IsTrue(model.Undo());
        Assert.AreSame(original, model.CurrentImage);
        Assert.IsFalse(model.CanUndo);
    }

    [TestMethod]
    public void SaveAs_ChangesAssociationOnlyAfterSuccessfulAtomicSave()
    {
        var model = new MainViewModel();
        model.LoadImage(MemoryImage.FromSegments([new MemorySegment(0x20, [0xA5])]), null, null);
        model.EditByte(0x20, 0x5A, OverlapDecision.OverwriteIncoming);
        var path = Path.Combine(Path.GetTempPath(), $"hex-editor-{Guid.NewGuid():N}.hex");
        File.WriteAllText(path, "keep existing");
        try
        {
            Assert.Throws<IOException>(() => model.SaveAs(path, MemoryImageExportFormat.IntelHex, overwriteExisting: false));
            Assert.IsTrue(model.IsModified);
            Assert.IsNull(model.SourcePath);
            Assert.AreEqual("keep existing", File.ReadAllText(path));

            model.SaveAs(path, MemoryImageExportFormat.IntelHex, overwriteExisting: true);
            Assert.IsFalse(model.IsModified);
            Assert.AreEqual(Path.GetFullPath(path), model.SourcePath);
            Assert.AreEqual(MemoryImageExportFormat.IntelHex, model.SourceFormat);
        }
        finally { File.Delete(path); }
    }

    [TestMethod]
    public void ChecksumWrite_IsUndoableAndCoveredEditsMarkDisplayedResultStale()
    {
        var model = new MainViewModel();
        model.LoadImage(MemoryImage.FromSegments([new MemorySegment(0x1000, [0x01, 0x02])]), null, null);
        model.Checksum.Calculate(model.CurrentImage, ChecksumRequest.Additive(0x1000, 0x1001, 16, MissingAddressTreatment.Reject));

        model.WriteChecksumResult(0x2000);

        Assert.IsTrue(model.IsModified);
        CollectionAssert.AreEqual(new byte[] { 0x00, 0x03 }, model.CurrentImage.Segments[^1].Data.ToArray());
        Assert.IsFalse(model.Checksum.Result!.IsStale, "Writing outside the input range must preserve the result.");
        Assert.IsTrue(model.Undo());
        Assert.IsFalse(model.IsModified);
        Assert.IsFalse(model.CurrentImage.TryGetByte(0x2000, out _));
        Assert.IsTrue(model.Redo());
        Assert.IsTrue(model.CurrentImage.TryGetByte(0x2001, out _));

        model.EditByte(0x1000, 0xFF, OverlapDecision.OverwriteIncoming);
        Assert.IsTrue(model.Checksum.Result!.IsStale);
        var imageBeforeRejectedWrite = model.CurrentImage;
        Assert.Throws<InvalidOperationException>(() => model.WriteChecksumResult(0x3000));
        Assert.AreSame(imageBeforeRejectedWrite, model.CurrentImage, "A stale checksum must not change the image.");
        Assert.IsFalse(model.CurrentImage.TryGetByte(0x3000, out _));
    }

    [TestMethod]
    public void Row_UsesAbsoluteAlignedAddressAndDistinguishesHolesFromStoredBytes()
    {
        var image = MemoryImage.FromSegments(
        [
            new MemorySegment(0x1001, [0x00]),
            new MemorySegment(0x1003, [0x20, 0x7E, 0x7F, 0xFF])
        ]);
        var view = new HexImageViewModel(image, bytesPerRow: 8);

        var row = view.Rows[0];

        Assert.AreEqual("00001000", row.Address);
        Assert.AreEqual("   00    20 7E 7F FF   ", row.HexData);
        Assert.AreEqual(" .  ~.. ", row.Ascii);
        Assert.IsFalse(row.IsGapMarker);
    }

    [TestMethod]
    public void Rows_KeepOneEmptyRowButCollapseLongAddressGap()
    {
        var image = MemoryImage.FromSegments(
        [
            new MemorySegment(0x100, [0x41]),
            new MemorySegment(0x110, [0x42]),
            new MemorySegment(0x200, [0x43])
        ]);
        var view = new HexImageViewModel(image, bytesPerRow: 8);

        Assert.HasCount(5, view.Rows);
        Assert.AreEqual("00000100", view.Rows[0].Address);
        Assert.AreEqual("00000108", view.Rows[1].Address);
        Assert.AreEqual("00000110", view.Rows[2].Address);
        Assert.IsTrue(view.Rows[3].IsGapMarker);
        StringAssert.Contains(view.Rows[3].GapDescription!, "00000118");
        StringAssert.Contains(view.Rows[3].GapDescription!, "000001F8");
        StringAssert.Contains(view.Rows[3].GapDescription!, "...");
        Assert.IsFalse(view.Rows[3].GapDescription!.Contains("â", StringComparison.Ordinal));
        Assert.AreEqual("00000200", view.Rows[4].Address);
    }

    [TestMethod]
    public void RowWidthAndAsciiCanBeChangedWithoutExpandingAddressGaps()
    {
        var image = MemoryImage.FromSegments(
        [
            new MemorySegment(0xA0000000, [0x41, 0x42]),
            new MemorySegment(0xFFFFFFFE, [0x00, 0xFF])
        ]);
        var view = new HexImageViewModel(image);

        view.BytesPerRow = 32;
        view.ShowAscii = false;

        Assert.HasCount(3, view.Rows);
        Assert.AreEqual("A0000000", view.Rows[0].Address);
        Assert.IsNull(view.Rows[0].Ascii);
        Assert.IsTrue(view.Rows[1].IsGapMarker);
        Assert.AreEqual("FFFFFFE0", view.Rows[2].Address);
        Assert.IsTrue(view.Rows[2].HexData.EndsWith("00 FF", StringComparison.Ordinal));
    }

    [TestMethod]
    public void Navigation_FindsAbsoluteAddressAndSegmentStartWithoutWalkingRows()
    {
        var image = MemoryImage.FromSegments(
        [
            new MemorySegment(0x1000, [0x11]),
            new MemorySegment(0xA0000000, [0x22])
        ]);
        var view = new HexImageViewModel(image, bytesPerRow: 16);

        Assert.IsTrue(view.TryNavigateToAddress(0xA0000000, out var addressRow));
        Assert.AreEqual("A0000000", view.Rows[addressRow].Address);
        Assert.IsTrue(view.TryNavigateToSegment(1, out var segmentRow));
        Assert.AreEqual(addressRow, segmentRow);
        Assert.IsTrue(view.TryNavigateToAddress(0x5000, out var gapRow));
        Assert.IsFalse(view.Rows[gapRow].IsGapMarker);
        Assert.AreEqual("00005000", view.Rows[gapRow].Address);
    }

    [TestMethod]
    public void Navigation_ToSecondBlockDoesNotExpandThePrecedingGap()
    {
        var image = MemoryImage.FromSegments(
        [
            new MemorySegment(0x1000, [0x11]),
            new MemorySegment(0x2000, [0x22])
        ]);
        var view = new HexImageViewModel(image, bytesPerRow: 11);
        var originalRowCount = view.Rows.Count;

        Assert.IsTrue(view.TryNavigateToSegment(0, out var firstRowIndex));
        Assert.AreEqual("00000FFC", view.Rows[firstRowIndex].Address);
        Assert.IsTrue(view.TryNavigateToSegment(1, out var rowIndex));

        Assert.AreEqual("00001FF8", view.Rows[rowIndex].Address);
        Assert.HasCount(originalRowCount, view.Rows);
    }

    [TestMethod]
    public void Navigation_AtEndOfGapKeepsNextBlockReachable()
    {
        var image = MemoryImage.FromSegments(
        [
            new MemorySegment(0x1000, [0x11]),
            new MemorySegment(0x2000, [0x22])
        ]);
        var view = new HexImageViewModel(image, bytesPerRow: 11);

        Assert.IsTrue(view.TryNavigateToAddress(0x1FFF, out var gapRow));
        Assert.IsFalse(view.Rows[gapRow].IsGapMarker);
        Assert.IsTrue(view.TryNavigateToSegment(1, out var blockRow));
        Assert.AreEqual("00001FF8", view.Rows[blockRow].Address);
    }

    [TestMethod]
    public void Navigation_IntoCollapsedLargeGapCreatesOnlyTargetRow()
    {
        var image = MemoryImage.FromSegments(
        [
            new MemorySegment(0x1000, [0x11]),
            new MemorySegment(0xF0000000, [0x22])
        ]);
        var view = new HexImageViewModel(image, bytesPerRow: 16);
        Assert.HasCount(3, view.Rows);

        Assert.IsTrue(view.TryNavigateToAddress(0x50000003, out var targetRow));

        Assert.HasCount(5, view.Rows);
        Assert.AreEqual("50000000", view.Rows[targetRow].Address);
        Assert.IsFalse(view.Rows[targetRow].IsGapMarker);
        Assert.IsTrue(view.Rows[1].IsGapMarker);
        Assert.IsTrue(view.Rows[3].IsGapMarker);
    }

    [TestMethod]
    public void Rows_AreIndexedLazilyForLargeOccupiedSegments()
    {
        var image = MemoryImage.FromSegments([new MemorySegment(0, new byte[10 * 1024 * 1024])]);
        var view = new HexImageViewModel(image, bytesPerRow: 32);

        Assert.HasCount(327680, view.Rows);
        Assert.AreEqual("00000000", view.Rows[0].Address);
        Assert.AreEqual("009FFFE0", view.Rows[^1].Address);
    }

    [TestMethod]
    public void MainViewModel_ExportsOnlySelectedSegmentAndCanReimportIt()
    {
        const string sourceRecords = ":020010001011CD\n:0100200020BF\n:00000001FF\n";
        var fileService = new MemoryImageFileService();
        var viewModel = new MainViewModel();
        using var source = new MemoryStream(Encoding.ASCII.GetBytes(sourceRecords));
        viewModel.ApplyImport(
            fileService.PrepareImport(source),
            OverlapDecision.OverwriteIncoming);
        using var exported = new MemoryStream();

        viewModel.ExportSegments([1], MemoryImageExportFormat.SRecord, 8, exported);
        exported.Position = 0;
        var imported = fileService.PrepareImport(exported).Apply(OverlapDecision.OverwriteIncoming)!.Image;

        Assert.HasCount(1, imported.Segments);
        Assert.AreEqual(0x20u, imported.Segments[0].StartAddress);
        CollectionAssert.AreEqual(new byte[] { 0x20 }, imported.Segments[0].Data.ToArray());
    }

    [TestMethod]
    public void SaveAs_RetainsExecutionAddressWhenSavingInSourceOrOtherFormat()
    {
        const string records = "S0061234484558CE\nS1050010AABB85\nS9031234B6\n";
        var service = new MemoryImageFileService();
        using var input = new MemoryStream(Encoding.ASCII.GetBytes(records));
        var preview = service.PrepareImport(input);
        var model = new MainViewModel();
        model.ApplyImport(preview, OverlapDecision.OverwriteIncoming);
        var srecPath = Path.Combine(Path.GetTempPath(), $"hex-editor-{Guid.NewGuid():N}.srec");
        var intelPath = Path.Combine(Path.GetTempPath(), $"hex-editor-{Guid.NewGuid():N}.hex");
        try
        {
            model.SaveAs(srecPath, MemoryImageExportFormat.SRecord, overwriteExisting: false);
            Assert.AreEqual(0x1234u, model.SourceFormatMetadata!.SRecord!.StartAddress);
            using var srec = File.OpenRead(srecPath);
            var importedSrec = service.PrepareImport(srec).Apply(OverlapDecision.OverwriteIncoming)!;
            Assert.AreEqual(0x1234u, importedSrec.FormatMetadata!.SRecord!.StartAddress);
            Assert.AreEqual((ushort)0x1234, importedSrec.FormatMetadata.SRecord.HeaderAddress);
            CollectionAssert.AreEqual(new byte[] { 0x48, 0x45, 0x58 }, importedSrec.FormatMetadata.SRecord.HeaderData!);

            model.SaveAs(intelPath, MemoryImageExportFormat.IntelHex, overwriteExisting: false);
            Assert.AreEqual(0x1234u, model.SourceFormatMetadata!.IntelHex!.StartLinearAddress);
            using var intel = File.OpenRead(intelPath);
            var importedIntel = service.PrepareImport(intel).Apply(OverlapDecision.OverwriteIncoming)!;
            Assert.AreEqual(0x1234u, importedIntel.FormatMetadata!.IntelHex!.StartLinearAddress);
            Assert.IsFalse(model.IsModified);
        }
        finally
        {
            File.Delete(srecPath);
            File.Delete(intelPath);
        }
    }
}
