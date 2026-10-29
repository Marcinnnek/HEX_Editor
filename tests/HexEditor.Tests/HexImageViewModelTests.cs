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
        Assert.IsNull(model.CurrentAddress);
        Assert.AreEqual(0, model.SelectionLength);

        model.SetSelection(0x1234, 0x123F);
        Assert.AreEqual(0x1234u, model.CurrentAddress);
        Assert.AreEqual(12L, model.SelectionLength);
        Assert.AreEqual("00001234", model.CurrentAddressText);

        model.ClearSelection();
        Assert.IsNull(model.CurrentAddress);
        Assert.AreEqual(0, model.SelectionLength);
        Assert.AreEqual("—", model.CurrentAddressText);
    }

    [TestMethod]
    public void EditorStatus_ReportsSourceFormatAndModifiedState()
    {
        var model = new MainViewModel();
        model.LoadImage(MemoryImage.FromSegments([new MemorySegment(0x10, [0xAA])]), "input.hex", MemoryImageExportFormat.IntelHex);

        Assert.AreEqual("Intel HEX", model.ImageFormatText);
        Assert.AreEqual("Saved", model.ModifiedStateText);
        model.EditByte(0x10, 0xBB, OverlapDecision.OverwriteIncoming);
        Assert.AreEqual("Modified", model.ModifiedStateText);
        Assert.IsTrue(model.Undo());
        Assert.AreEqual("Saved", model.ModifiedStateText);
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

        model.FillRange(0x1001, 0x1006, [0xAA, 0xBB]);
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
        model.CalculateChecksum(ChecksumRequest.Additive(0x1000, 0x1001, 16, MissingAddressTreatment.Reject));

        model.WriteChecksumResult(0x2000);

        Assert.IsTrue(model.IsModified);
        CollectionAssert.AreEqual(new byte[] { 0x00, 0x03 }, model.CurrentImage.Segments[^1].Data.ToArray());
        Assert.IsFalse(model.ChecksumResult!.IsStale, "Writing outside the input range must preserve the result.");
        Assert.IsTrue(model.Undo());
        Assert.IsFalse(model.IsModified);
        Assert.IsFalse(model.CurrentImage.TryGetByte(0x2000, out _));
        Assert.IsTrue(model.Redo());
        Assert.IsTrue(model.CurrentImage.TryGetByte(0x2001, out _));

        model.EditByte(0x1000, 0xFF, OverlapDecision.OverwriteIncoming);
        Assert.IsTrue(model.ChecksumResult.IsStale);
        Assert.Throws<InvalidOperationException>(() => model.WriteChecksumResult(0x3000));
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
        Assert.IsTrue(view.Rows[gapRow].IsGapMarker);
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
}
