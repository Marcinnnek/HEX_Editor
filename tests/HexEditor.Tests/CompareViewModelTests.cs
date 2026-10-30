using HexEditor.Core;
using HexEditor.Compare;
using HexEditor.Formats;
using HexEditor.UI;
using System.Collections.Specialized;

namespace HexEditor.Tests;

[TestClass]
public sealed class CompareViewModelTests
{
    [TestMethod]
    public void SelectingOccupiedCompareBytesKeepsBothSparsePanelsStable()
    {
        var comparison = new CompareViewModel(
            MemoryImage.FromSegments([new MemorySegment(0x1000, [0x10, 0x11]), new MemorySegment(0x90000000, [0x20])]),
            MemoryImage.FromSegments([new MemorySegment(0x1000, [0x10, 0x12]), new MemorySegment(0x90000000, [0x21])]),
            bytesPerRow: 8);
        var baseRows = comparison.FileARows;
        var referenceRows = comparison.FileBRows;
        var baseResets = 0;
        var referenceResets = 0;
        ((INotifyCollectionChanged)baseRows).CollectionChanged += (_, _) => baseResets++;
        ((INotifyCollectionChanged)referenceRows).CollectionChanged += (_, _) => referenceResets++;

        foreach (var address in new uint[] { 0x1000, 0x1001, 0x90000000, 0x1001 })
        {
            Assert.IsTrue(comparison.TryNavigateToAddress(address, out var rowIndex));
            Assert.AreEqual(comparison.FileARows[rowIndex].Address, comparison.FileBRows[rowIndex].Address);
        }

        Assert.AreSame(baseRows, comparison.FileARows);
        Assert.AreSame(referenceRows, comparison.FileBRows);
        Assert.AreEqual(0, baseResets);
        Assert.AreEqual(0, referenceResets);
        Assert.IsTrue(comparison.TryFindNextDifferenceFromAddress(0x1000, null, out var next));
        Assert.AreEqual(0x1001u, next);
    }

    [TestMethod]
    public async Task ActiveDocumentCommands_RouteUndoRedoAndSaveAsToCompareBase()
    {
        var viewModel = new MainViewModel();
        var baseImage = MemoryImage.FromSegments([new MemorySegment(0x1000, [0x10])]);
        var baseOwner = new MainViewModel();
        baseOwner.LoadImage(baseImage, null, MemoryImageExportFormat.IntelHex);
        var baseDocument = baseOwner.Workspace.Document;
        var compare = new CompareWorkspaceTabViewModel(baseImage,
            MemoryImage.FromSegments([new MemorySegment(0x1000, [0x20])]),
            "base.hex", "reference.hex", baseDocument: baseDocument);
        viewModel.Workspace.RegisterComparisonView(compare);
        viewModel.ActivateComparison(compare);
        var path = Path.Combine(Path.GetTempPath(), $"hex-editor-active-base-{Guid.NewGuid():N}.hex");

        try
        {
            baseDocument.Editing.EditByte(0x1000, 0x11, OverlapDecision.OverwriteIncoming);
            Assert.IsTrue(viewModel.ActiveCanUndo);
            Assert.IsTrue(viewModel.Undo());
            Assert.IsTrue(baseDocument.CurrentImage.TryGetByte(0x1000, out var undone));
            Assert.AreEqual((byte)0x10, undone);
            Assert.IsTrue(viewModel.Redo());

            await viewModel.SaveAsAsync(path, MemoryImageExportFormat.IntelHex, false,
                CancellationToken.None, null);

            Assert.AreEqual(Path.GetFullPath(path), baseDocument.SourcePath);
            Assert.IsFalse(baseDocument.IsModified);
            Assert.IsNull(viewModel.Workspace.Document.SourcePath);
            Assert.IsTrue(viewModel.ActiveCanUndo);
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }

    [TestMethod]
    public void ActiveTabViewSettings_RestoreIndependentlyAndSynchronizePrimaryControls()
    {
        var viewModel = new MainViewModel();
        var first = new CompareViewModel(
            MemoryImage.FromSegments([new MemorySegment(0x1000, [0x10])]),
            MemoryImage.FromSegments([new MemorySegment(0x1000, [0x11])]), 8, false);
        var second = new CompareViewModel(
            MemoryImage.FromSegments([new MemorySegment(0x2000, [0x20])]),
            MemoryImage.FromSegments([new MemorySegment(0x2000, [0x21])]), 32, true);

        viewModel.ActivateComparison(first);
        Assert.AreEqual(8, viewModel.ActiveBytesPerRow);
        Assert.IsFalse(viewModel.ActiveShowAscii);
        viewModel.ActiveBytesPerRow = 16;
        viewModel.ActiveShowAscii = true;
        Assert.AreEqual(16, first.BytesPerRow);
        Assert.IsTrue(first.ShowAscii);

        viewModel.ActivateComparison(second);
        Assert.AreEqual(32, viewModel.ActiveBytesPerRow);
        Assert.IsTrue(viewModel.ActiveShowAscii);
        second.BytesPerRow = 8;
        second.ShowAscii = false;
        Assert.AreEqual(8, viewModel.ActiveBytesPerRow);
        Assert.IsFalse(viewModel.ActiveShowAscii);

        viewModel.ActivateEditor();
        Assert.AreEqual(16, viewModel.ActiveBytesPerRow);
        Assert.IsTrue(viewModel.ActiveShowAscii);
    }

    [TestMethod]
    public void CompareNavigation_UsesOneAbsoluteAddressForBothSparsePanels()
    {
        var workspace = new CompareWorkspaceTabViewModel(
            MemoryImage.FromSegments([new MemorySegment(0x1001, [0x10]), new MemorySegment(0x1010, [0x20])]),
            MemoryImage.FromSegments([new MemorySegment(0x1001, [0x11]), new MemorySegment(0x1020, [0x30])]),
            "base.hex", "reference.hex");
        Assert.IsTrue(workspace.Comparison.TryNavigateToAddress(0x1010, out var rowIndex));
        Assert.AreEqual("00001010", workspace.Comparison.FileARows[rowIndex].Address);
        Assert.AreEqual(workspace.Comparison.FileARows[rowIndex].Address, workspace.Comparison.FileBRows[rowIndex].Address);
        Assert.IsTrue(workspace.Comparison.FileBRows[rowIndex].Cells.All(cell => cell.Address is null || cell.Address >= 0x1010));
    }

    [TestMethod]
    public void Comparison_AlignsSparseBytesByAddressAndClassifiesEachSide()
    {
        var fileA = MemoryImage.FromSegments([new MemorySegment(0x1001, [0x11, 0x22]), new MemorySegment(0x1010, [0x44])]);
        var fileB = MemoryImage.FromSegments([new MemorySegment(0x1001, [0x11, 0x33]), new MemorySegment(0x1004, [0x55])]);
        var comparison = new CompareViewModel(fileA, fileB, bytesPerRow: 8);

        var row = comparison.Rows[0];

        Assert.AreEqual("00001000", row.Address);
        Assert.AreEqual(CompareByteState.Equal, row.States[1]);
        Assert.AreEqual(CompareByteState.Different, row.States[2]);
        Assert.AreEqual(CompareByteState.OnlyB, row.States[4]);
        Assert.AreEqual("11 22", row.FileA.HexData.Trim());
        Assert.IsTrue(row.FileB.HexData.Contains("11 33    55", StringComparison.Ordinal));
        Assert.AreEqual(CompareByteState.OnlyA, comparison.Rows[2].States[0]);
    }

    [TestMethod]
    public void WorkspaceTab_ListsBlocksForBothImagesAndRefreshesThemAfterAnEdit()
    {
        var left = MemoryImage.FromSegments([new MemorySegment(0x1000, [0x10]), new MemorySegment(0x2000, [0x20])]);
        var right = MemoryImage.FromSegments([new MemorySegment(0x3000, [0x30])]);
        var workspace = new CompareWorkspaceTabViewModel(left, right, "left.hex", "right.s19");

        Assert.AreEqual("left.hex", workspace.NameA);
        Assert.AreEqual("right.s19", workspace.NameB);
        Assert.HasCount(2, workspace.BlocksA);
        Assert.AreEqual(0x2000u, workspace.BlocksA[1].StartAddress);
        Assert.HasCount(1, workspace.BlocksB);
        Assert.AreEqual(0x3000u, workspace.BlocksB[0].StartAddress);

        var editedLeft = MemoryImage.FromSegments([new MemorySegment(0x1000, [0x10]), new MemorySegment(0x4000, [0x40])]);
        workspace.RefreshImage(left, editedLeft);

        Assert.HasCount(2, workspace.BlocksA);
        Assert.AreEqual(0x4000u, workspace.BlocksA[1].StartAddress);
        Assert.AreEqual(CompareByteState.OnlyA, workspace.Comparison.Rows[^1].States[0]);
    }

    [TestMethod]
    public void WorkspaceTab_RefreshesMatchingDocumentPathWhenImagesAreSeparateInstances()
    {
        var compareImage = MemoryImage.FromSegments([new MemorySegment(0x1000, [0x10])]);
        var otherImage = MemoryImage.FromSegments([new MemorySegment(0x1000, [0x20])]);
        var workspace = new CompareWorkspaceTabViewModel(
            compareImage, otherImage, "left.hex", "right.s19",
            @"C:\Firmware\image.hex", @"C:\Firmware\other.s19");
        var editedDocument = MemoryImage.FromSegments([new MemorySegment(0x1000, [0x20]), new MemorySegment(0x2000, [0x30])]);

        workspace.RefreshDocument(@"c:\firmware\IMAGE.HEX", editedDocument);

        Assert.AreSame(editedDocument, workspace.Comparison.FileA);
        Assert.AreSame(otherImage, workspace.Comparison.FileB);
        Assert.HasCount(2, workspace.BlocksA);
        Assert.AreEqual(0x2000u, workspace.BlocksA[1].StartAddress);
        Assert.AreEqual(CompareByteState.Equal, workspace.Comparison.Rows[0].States[0]);
    }

    [TestMethod]
    public void WorkspaceTab_RefreshesBothSidesWhenTheyRepresentSameDocument()
    {
        var left = MemoryImage.FromSegments([new MemorySegment(0x1000, [0x10])]);
        var right = MemoryImage.FromSegments([new MemorySegment(0x1000, [0x20])]);
        var workspace = new CompareWorkspaceTabViewModel(
            left, right, "left.hex", "same.hex",
            @"C:\Firmware\image.hex", @"C:\Firmware\IMAGE.HEX");
        var editedDocument = MemoryImage.FromSegments([new MemorySegment(0x2000, [0x30])]);

        workspace.RefreshDocument(@"C:\Firmware\image.hex", editedDocument);

        Assert.AreSame(editedDocument, workspace.Comparison.FileA);
        Assert.AreSame(editedDocument, workspace.Comparison.FileB);
        Assert.HasCount(1, workspace.BlocksA);
        Assert.HasCount(1, workspace.BlocksB);
        Assert.AreEqual(0x2000u, workspace.BlocksA[0].StartAddress);
        Assert.AreEqual("No differences", workspace.Comparison.ComparisonStatus);
    }

    [TestMethod]
    public void MainDocumentEditsUndoAndRedo_RefreshSeparateComparisonImageInstances()
    {
        const string documentPath = @"C:\Firmware\image.hex";
        var mainImage = MemoryImage.FromSegments([new MemorySegment(0x1000, [0x10])]);
        var comparisonImage = MemoryImage.FromSegments([new MemorySegment(0x1000, [0x10])]);
        var referenceImage = MemoryImage.FromSegments([new MemorySegment(0x1000, [0x10])]);
        var viewModel = new MainViewModel();
        viewModel.LoadImage(mainImage, documentPath, MemoryImageExportFormat.IntelHex);
        var workspace = new CompareWorkspaceTabViewModel(
            comparisonImage, referenceImage, "image.hex", "other.s19",
            @"c:\firmware\IMAGE.HEX", @"C:\Firmware\other.s19");
        viewModel.ImageChanged += (_, updated) => workspace.RefreshDocument(viewModel.SourcePath, updated);

        viewModel.EditByte(0x1000, 0x20, OverlapDecision.OverwriteIncoming);
        Assert.AreEqual(CompareByteState.Different, workspace.Comparison.Rows[0].States[0]);
        Assert.IsTrue(viewModel.Undo());
        Assert.AreEqual(CompareByteState.Equal, workspace.Comparison.Rows[0].States[0]);
        Assert.IsTrue(viewModel.Redo());
        Assert.AreEqual(CompareByteState.Different, workspace.Comparison.Rows[0].States[0]);
    }

    [TestMethod]
    public void SharedPanelRows_ExposeAddressableByteCellsWithSideSpecificHighlights()
    {
        var comparison = new CompareViewModel(
            MemoryImage.FromSegments([new MemorySegment(0x1000, [0x10, 0x20]), new MemorySegment(0x1003, [0x30])]),
            MemoryImage.FromSegments([new MemorySegment(0x1000, [0x10, 0x21]), new MemorySegment(0x1004, [0x40])]),
            bytesPerRow: 8);

        var fileA = comparison.FileARows[0];
        var fileB = comparison.FileBRows[0];

        Assert.HasCount(8, fileA.Cells);
        Assert.AreEqual(0x1001u, fileA.Cells[1].Address);
        Assert.AreEqual("Different", fileA.Cells[1].Highlight);
        Assert.AreEqual("Different", fileB.Cells[1].Highlight);
        Assert.AreEqual("OnlyInPanel", fileA.Cells[3].Highlight);
        Assert.AreEqual("OnlyOther", fileB.Cells[3].Highlight);
        Assert.AreEqual("OnlyOther", fileA.Cells[4].Highlight);
        Assert.AreEqual("OnlyInPanel", fileB.Cells[4].Highlight);
        Assert.AreEqual(string.Empty, fileA.Cells[5].Highlight);
        Assert.AreEqual(0x1007u, fileA.Cells[7].Address);
    }

    [TestMethod]
    public void ComparisonPanelAtAddressSpaceEnd_DoesNotWrapCellAddresses()
    {
        var lastAddress = uint.MaxValue;
        var comparison = new CompareViewModel(
            MemoryImage.FromSegments([new MemorySegment(lastAddress, [0xA5])]),
            MemoryImage.Empty,
            bytesPerRow: 16);

        Assert.IsTrue(comparison.TryNavigateToAddress(lastAddress, out var rowIndex));
        var cells = comparison.FileARows[rowIndex].Cells;

        for (var offset = 0; offset < cells.Count; offset++)
        {
            Assert.AreEqual(0xFFFFFFF0u + (uint)offset, cells[offset].Address);
        }
        Assert.AreEqual(lastAddress, cells[^1].Address);
    }

    [TestMethod]
    public void AddressRangeMode_HidesBytesOutsidePartialBoundaryRows()
    {
        var comparison = new CompareViewModel(
            MemoryImage.FromSegments([new MemorySegment(0x1000, [0xA0, 0xA1, 0xA2, 0xA3, 0xA4, 0xA5, 0xA6, 0xA7])]),
            MemoryImage.FromSegments([new MemorySegment(0x1000, [0xB0, 0xB1, 0xB2, 0xB3, 0xB4, 0xB5, 0xB6, 0xB7])]),
            bytesPerRow: 8);

        comparison.SetAddressRange(0x1003, 0x1005);
        comparison.ViewMode = CompareViewMode.AddressRange;

        Assert.HasCount(1, comparison.Rows);
        Assert.AreEqual("00001000", comparison.Rows[0].Address);
        var row = comparison.Rows[0];
        for (var offset = 0; offset < 8; offset++)
        {
            var inRange = offset is >= 3 and <= 5;
            Assert.AreEqual(inRange ? CompareByteState.Different : CompareByteState.Equal, row.States[offset]);
            Assert.AreEqual(inRange ? (0xA0 + offset).ToString("X2") : string.Empty, row.FileA.Cells[offset].Hex);
            Assert.AreEqual(inRange ? (uint?)(0x1000 + offset) : null, row.FileA.Cells[offset].Address);
            Assert.AreEqual(inRange ? (0xB0 + offset).ToString("X2") : string.Empty, row.FileB.Cells[offset].Hex);
            Assert.AreEqual(inRange ? "." : " ", row.FileA.Cells[offset].Ascii);
            Assert.AreEqual(inRange ? "Different" : string.Empty, comparison.FileARows[0].Cells[offset].Highlight);
        }

        Assert.IsNull(row.FileA.Cells[2].Address);
        Assert.IsNull(row.FileA.Cells[6].Address);
        Assert.IsTrue(comparison.TryNavigateToAddress(0x1003, out var rowIndex));
        Assert.AreEqual(0, rowIndex);
    }

    [TestMethod]
    public void AddressRangeMode_NavigationRejectsAddressesBeyondInclusiveRangeEvenWithinVisibleRow()
    {
        var comparison = new CompareViewModel(
            MemoryImage.FromSegments([new MemorySegment(0x1000, [0xA0, 0xA1, 0xA2, 0xA3, 0xA4, 0xA5, 0xA6, 0xA7])]),
            MemoryImage.Empty,
            bytesPerRow: 8);

        comparison.SetAddressRange(0x1000, 0x1005);
        comparison.ViewMode = CompareViewMode.AddressRange;

        Assert.IsTrue(comparison.TryNavigateToAddress(0x1000, out var lowerBoundaryRow));
        Assert.AreEqual(0, lowerBoundaryRow);
        Assert.IsTrue(comparison.TryNavigateToAddress(0x1005, out var upperBoundaryRow));
        Assert.AreEqual(0, upperBoundaryRow);
        Assert.IsFalse(comparison.TryNavigateToAddress(0x1007, out _));
    }

    [TestMethod]
    public void DifferenceNavigation_InAddressRangeDoesNotReturnOutsideDifferences()
    {
        var comparison = new CompareViewModel(
            MemoryImage.FromSegments([new MemorySegment(0x0FFF, [0x10, 0x11, 0x12, 0x13, 0x14, 0x15, 0x16, 0x17])]),
            MemoryImage.FromSegments([new MemorySegment(0x0FFF, [0x20, 0x11, 0x12, 0x13, 0x14, 0x15, 0x16, 0x27])]),
            bytesPerRow: 8);

        comparison.SetAddressRange(0x1000, 0x1005);
        comparison.ViewMode = CompareViewMode.AddressRange;

        Assert.IsFalse(comparison.TryFindNextDifference(0x1000, out _), "The next difference at 0x1006 is outside the inclusive range.");
        Assert.IsFalse(comparison.TryFindPreviousDifference(0x1005, out _), "The previous difference at 0x0FFF is outside the inclusive range.");
    }

    [TestMethod]
    public void DifferenceNavigation_InAddressRangeIncludesBothRangeBoundaries()
    {
        var comparison = new CompareViewModel(
            MemoryImage.FromSegments([new MemorySegment(0x1000, [0x10, 0x11, 0x12, 0x13, 0x14, 0x15])]),
            MemoryImage.FromSegments([new MemorySegment(0x1000, [0x20, 0x21, 0x22, 0x23, 0x24, 0x25])]),
            bytesPerRow: 8);

        comparison.SetAddressRange(0x1000, 0x1005);
        comparison.ViewMode = CompareViewMode.AddressRange;

        Assert.IsTrue(comparison.TryFindNextDifference(0x1000, out var next));
        Assert.AreEqual(0x1000u, next);
        Assert.IsTrue(comparison.TryFindPreviousDifference(0x1005, out var previous));
        Assert.AreEqual(0x1005u, previous);
    }

    [TestMethod]
    public void ComparisonStatus_ReportsEqualityAndDifferenceCountsAndRefreshes()
    {
        var comparison = new CompareViewModel(
            MemoryImage.FromSegments([new MemorySegment(0x1000, [0x10, 0x20]), new MemorySegment(0x1010, [0x30])]),
            MemoryImage.FromSegments([new MemorySegment(0x1000, [0x10, 0x21]), new MemorySegment(0x1020, [0x40])]));

        Assert.AreEqual("3 differences (1 different, 1 only in File A, 1 only in File B)", comparison.ComparisonStatus);

        comparison.Refresh(comparison.FileA, MemoryImage.FromSegments([new MemorySegment(0x1000, [0x10, 0x20]), new MemorySegment(0x1010, [0x30])]));

        Assert.AreEqual("No differences", comparison.ComparisonStatus);
    }

    [TestMethod]
    public void Comparison_SharesNavigationAndSettingsAndCanShowOnlyDifferencesOrRange()
    {
        var fileA = MemoryImage.FromSegments([new MemorySegment(0x1000, [0x10, 0x20, 0x30]), new MemorySegment(0x90000000, [0x40])]);
        var fileB = MemoryImage.FromSegments([new MemorySegment(0x1000, [0x10, 0x21, 0x30]), new MemorySegment(0x90000000, [0x40])]);
        var comparison = new CompareViewModel(fileA, fileB);

        Assert.IsTrue(comparison.TryNavigateToAddress(0x1001, out var index));
        Assert.AreEqual("00001000", comparison.Rows[index].Address);
        Assert.IsTrue(comparison.TryFindNextDifference(0x1000, out var next));
        Assert.AreEqual(0x1001u, next);
        Assert.IsFalse(comparison.TryFindNextDifference(next + 1, out _));
        Assert.IsTrue(comparison.TryFindPreviousDifference(0x1002, out var previous));
        Assert.AreEqual(0x1001u, previous);

        comparison.BytesPerRow = 8;
        comparison.ShowAscii = false;
        comparison.ViewMode = CompareViewMode.DifferencesOnly;
        Assert.HasCount(1, comparison.Rows);
        Assert.IsNull(comparison.Rows[0].FileA.Ascii);
        Assert.AreEqual("00001000", comparison.Rows[0].Address);

        comparison.SetAddressRange(0x1001, 0x1001);
        comparison.ViewMode = CompareViewMode.AddressRange;
        Assert.HasCount(1, comparison.Rows);
        Assert.AreEqual(CompareByteState.Different, comparison.Rows[0].States[1]);

        comparison.Refresh(MemoryImage.FromSegments([new MemorySegment(0x1001, [0x21])]), fileB);
        Assert.AreEqual(CompareByteState.Equal, comparison.Rows[0].States[1]);
    }

    [TestMethod]
    public void MissingBytes_AreNavigableDifferencesAndAppearInDifferencesOnly()
    {
        var fileA = MemoryImage.FromSegments([new MemorySegment(0x2000, [0x10, 0x20])]);
        var fileB = MemoryImage.FromSegments([new MemorySegment(0x2000, [0x10, 0x21, 0x30])]);
        var comparison = new CompareViewModel(fileA, fileB, bytesPerRow: 8);

        Assert.IsTrue(comparison.TryFindNextDifference(0x2001, out var firstDifference));
        Assert.AreEqual(0x2001u, firstDifference);
        Assert.IsTrue(comparison.TryFindNextDifference(firstDifference + 1, out var onlyB));
        Assert.AreEqual(0x2002u, onlyB);
        Assert.IsTrue(comparison.TryFindPreviousDifference(0x2002, out var previous));
        Assert.AreEqual(0x2002u, previous);

        comparison.ViewMode = CompareViewMode.DifferencesOnly;
        Assert.HasCount(1, comparison.Rows);
        Assert.AreEqual(CompareByteState.OnlyB, comparison.Rows[0].States[2]);
    }

    [TestMethod]
    public void DifferenceNavigation_UsesAddressSpaceEndsWhenNoRowIsSelected()
    {
        var comparison = new CompareViewModel(
            MemoryImage.FromSegments([new MemorySegment(0, [0x10])]),
            MemoryImage.Empty);

        Assert.IsTrue(comparison.TryFindNextDifferenceFromSelection(null, out var next));
        Assert.AreEqual(0u, next);
        Assert.IsTrue(comparison.TryFindPreviousDifferenceFromSelection(null, out var previous));
        Assert.AreEqual(0u, previous);
    }

    [TestMethod]
    public void DifferenceNavigation_FromSelectedAddressSearchesStrictlyBeyondSelection()
    {
        var comparison = new CompareViewModel(
            MemoryImage.FromSegments([new MemorySegment(0, [0x10, 0x20, 0x30])]),
            MemoryImage.FromSegments([new MemorySegment(0, [0x11, 0x20, 0x31])]));

        Assert.IsTrue(comparison.TryFindNextDifferenceFromSelection(0, out var next));
        Assert.AreEqual(2u, next);
        Assert.IsTrue(comparison.TryFindPreviousDifferenceFromSelection(2, out var previous));
        Assert.AreEqual(0u, previous);
        Assert.IsFalse(comparison.TryFindPreviousDifferenceFromSelection(0, out _));
    }

    [TestMethod]
    public void DifferenceNavigation_FromAddressAnchorWithoutSelectedRowSearchesStrictlyInRequestedDirectionAcrossSparseFiles()
    {
        var comparison = new CompareViewModel(
            MemoryImage.FromSegments([new MemorySegment(0x1000, [0x10]), new MemorySegment(0x90000000, [0x20])]),
            MemoryImage.FromSegments([new MemorySegment(0x2000, [0x11]), new MemorySegment(0x90000000, [0x21])]));

        Assert.IsTrue(comparison.TryFindNextDifferenceFromAddress(null, 0x1000, out var next));
        Assert.AreEqual(0x2000u, next);
        Assert.IsTrue(comparison.TryFindPreviousDifferenceFromAddress(null, 0x90000000, out var previous));
        Assert.AreEqual(0x2000u, previous);
        Assert.IsFalse(comparison.TryFindNextDifferenceFromAddress(null, 0x90000000, out _));
        Assert.IsFalse(comparison.TryFindPreviousDifferenceFromAddress(null, 0x1000, out _));
    }

    [TestMethod]
    public void DifferenceNavigation_FromSelectedByteFindsNextDifferenceAcrossDifferentSparseFiles()
    {
        var comparison = new CompareViewModel(
            MemoryImage.FromSegments([new MemorySegment(0x1000, [0x10, 0x20]), new MemorySegment(0x90000000, [0x30])]),
            MemoryImage.FromSegments([new MemorySegment(0x1000, [0x10, 0x21]), new MemorySegment(0x70000000, [0x40])]));

        Assert.IsTrue(comparison.TryFindNextDifferenceFromAddress(0x1000, 0x70000000, out var next));
        Assert.AreEqual(0x1001u, next);
        Assert.IsTrue(comparison.TryFindPreviousDifferenceFromAddress(0x90000000, 0, out var previous));
        Assert.AreEqual(0x70000000u, previous);
    }

    [TestMethod]
    public void DifferenceNavigation_NoDifferenceBeyondAnchorLeavesComparisonRowsAndAddressNavigationUnchanged()
    {
        var comparison = new CompareViewModel(
            MemoryImage.FromSegments([new MemorySegment(0x1000, [0x10]), new MemorySegment(0x2000, [0x20])]),
            MemoryImage.FromSegments([new MemorySegment(0x1000, [0x11]), new MemorySegment(0x2000, [0x20])]));
        comparison.TryNavigateToAddress(0x1000, out var originalRow);
        var originalAddress = comparison.Rows[originalRow].Address;

        Assert.IsFalse(comparison.TryFindNextDifferenceFromAddress(null, 0x1000, out _));
        Assert.AreEqual(originalAddress, comparison.Rows[originalRow].Address);
        Assert.IsTrue(comparison.TryNavigateToAddress(0x1000, out var sameRow));
        Assert.AreEqual(originalRow, sameRow);
    }

    [TestMethod]
    public void SparseRegions_CollapseLargeGapsAndRangeViewDoesNotExpandThem()
    {
        var fileA = MemoryImage.FromSegments([new MemorySegment(0x1000, [0x11]), new MemorySegment(0x90000000, [0x22])]);
        var comparison = new CompareViewModel(fileA, MemoryImage.Empty, bytesPerRow: 16);

        Assert.HasCount(3, comparison.Rows);
        Assert.IsTrue(comparison.Rows[1].IsGapMarker);
        StringAssert.Contains(comparison.Rows[1].GapDescription!, "00001010");
        StringAssert.Contains(comparison.Rows[1].GapDescription!, "8FFFFFF0");
        StringAssert.Contains(comparison.Rows[1].GapDescription!, "...");
        Assert.IsFalse(comparison.Rows[1].GapDescription!.Contains("â", StringComparison.Ordinal));
        Assert.IsTrue(comparison.TryNavigateToAddress(0x50000000, out var gapIndex));
        Assert.AreEqual(2, gapIndex);
        Assert.IsFalse(comparison.Rows[gapIndex].IsGapMarker);
        Assert.AreEqual("50000000", comparison.Rows[gapIndex].Address);
        Assert.IsLessThanOrEqualTo(comparison.Rows.Count, 5, "Navigating into a large gap must keep the sparse row count bounded.");

        comparison.SetAddressRange(0x1000, 0x90000000);
        comparison.ViewMode = CompareViewMode.AddressRange;
        Assert.HasCount(5, comparison.Rows);
        comparison.ViewMode = CompareViewMode.DifferencesOnly;
        Assert.HasCount(3, comparison.Rows);
        Assert.IsFalse(comparison.Rows.Any(row => row.IsGapMarker));
    }
}
