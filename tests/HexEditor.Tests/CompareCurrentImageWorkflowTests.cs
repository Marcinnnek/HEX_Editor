using HexEditor.Core;
using HexEditor.Formats;
using HexEditor.UI;

namespace HexEditor.Tests;

[TestClass]
public sealed class CompareCurrentImageWorkflowTests
{
    [TestMethod]
    public async Task CompareWithOpenDirtyBaseAsksOnlyForReferenceAndUsesCurrentBytes()
    {
        var basePath = CreateImageFile(0x10);
        var referencePath = CreateImageFile(0x20);
        try
        {
            var viewModel = new MainViewModel();
            viewModel.Workspace.Open(basePath, OverlapDecision.OverwriteIncoming);
            viewModel.EditByte(0x1000, 0x30, OverlapDecision.OverwriteIncoming);
            var document = viewModel.Workspace.Document;
            var prompts = new List<string>();

            var comparison = await new CompareCurrentImageWorkflow(viewModel).StartAsync(
                (title, _) => { prompts.Add(title); return referencePath; },
                () => Assert.Fail("An existing base must stay in Editor."), CancellationToken.None, null);

            Assert.IsNotNull(comparison);
            Assert.HasCount(1, prompts);
            Assert.AreSame(document, viewModel.Workspace.Document);
            Assert.IsTrue(document.IsModified);
            Assert.IsTrue(comparison.Comparison.FileA.TryGetByte(0x1000, out var baseByte));
            Assert.AreEqual((byte)0x30, baseByte);
            Assert.IsTrue(comparison.Comparison.FileB.TryGetByte(0x1000, out var referenceByte));
            Assert.AreEqual((byte)0x20, referenceByte);
        }
        finally { File.Delete(basePath); File.Delete(referencePath); }
    }

    [TestMethod]
    public async Task CompareWithoutImageOpensBaseBeforeSecondPickerAndKeepsItWhenCancelled()
    {
        var basePath = CreateImageFile(0x10);
        try
        {
            var viewModel = new MainViewModel();
            var prompts = new List<string>();
            var baseOpened = false;

            var comparison = await new CompareCurrentImageWorkflow(viewModel).StartAsync(
                (title, _) =>
                {
                    prompts.Add(title);
                    if (prompts.Count == 1) return basePath;
                    Assert.IsTrue(baseOpened);
                    Assert.AreEqual(Path.GetFullPath(basePath), viewModel.SourcePath);
                    return null;
                },
                () => baseOpened = true, CancellationToken.None, null);

            Assert.IsNull(comparison);
            Assert.HasCount(2, prompts);
            Assert.IsTrue(baseOpened);
            Assert.AreEqual(Path.GetFullPath(basePath), viewModel.SourcePath);
            Assert.IsFalse(viewModel.IsModified);
        }
        finally { File.Delete(basePath); }
    }

    [TestMethod]
    public async Task CompareWithoutImageOpensBaseAndCreatesComparisonAfterTwoSelections()
    {
        var basePath = CreateImageFile(0x10);
        var referencePath = CreateImageFile(0x20);
        try
        {
            var viewModel = new MainViewModel();
            var prompts = 0;
            var comparison = await new CompareCurrentImageWorkflow(viewModel).StartAsync(
                (_, _) => ++prompts == 1 ? basePath : referencePath,
                () => { }, CancellationToken.None, null);

            Assert.IsNotNull(comparison);
            Assert.AreEqual(2, prompts);
            Assert.AreEqual(Path.GetFullPath(basePath), viewModel.SourcePath);
            Assert.IsTrue(comparison.Comparison.FileA.TryGetByte(0x1000, out var value));
            Assert.AreEqual((byte)0x10, value);
        }
        finally { File.Delete(basePath); File.Delete(referencePath); }
    }

    [TestMethod]
    public async Task SelectingBaseFileAsReferenceReadsTheSavedReferenceBytes()
    {
        var path = CreateImageFile(0x10);
        try
        {
            var viewModel = new MainViewModel();
            viewModel.Workspace.Open(path, OverlapDecision.OverwriteIncoming);
            viewModel.EditByte(0x1000, 0x30, OverlapDecision.OverwriteIncoming);

            var comparison = await new CompareCurrentImageWorkflow(viewModel).StartAsync(
                (_, _) => path, () => Assert.Fail(), CancellationToken.None, null);

            Assert.IsNotNull(comparison);
            Assert.IsTrue(comparison.Comparison.FileA.TryGetByte(0x1000, out var current));
            Assert.IsTrue(comparison.Comparison.FileB.TryGetByte(0x1000, out var saved));
            Assert.AreEqual((byte)0x30, current);
            Assert.AreEqual((byte)0x10, saved);
        }
        finally { File.Delete(path); }
    }

    [TestMethod]
    public async Task ReferenceCancelAndParseFailurePreserveExistingDirtyDocument()
    {
        var basePath = CreateImageFile(0x10);
        var badPath = Path.Combine(Path.GetTempPath(), $"comparison-invalid-{Guid.NewGuid():N}.hex");
        File.WriteAllText(badPath, "invalid record");
        try
        {
            var viewModel = new MainViewModel();
            viewModel.Workspace.Open(basePath, OverlapDecision.OverwriteIncoming);
            viewModel.EditByte(0x1000, 0x30, OverlapDecision.OverwriteIncoming);
            var document = viewModel.Workspace.Document;
            var workflow = new CompareCurrentImageWorkflow(viewModel);

            Assert.IsNull(await workflow.StartAsync((_, _) => null, () => Assert.Fail(), CancellationToken.None, null));
            await Assert.ThrowsAsync<FormatException>(() => workflow.StartAsync(
                (_, _) => badPath, () => Assert.Fail(), CancellationToken.None, null));

            Assert.AreSame(document, viewModel.Workspace.Document);
            Assert.IsTrue(document.IsModified);
            Assert.IsTrue(document.CurrentImage.TryGetByte(0x1000, out var value));
            Assert.AreEqual((byte)0x30, value);
        }
        finally { File.Delete(basePath); File.Delete(badPath); }
    }

    [TestMethod]
    public async Task MismatchedReferenceCanBeCancelledWithoutChangingTheCurrentDocument()
    {
        var basePath = CreateImageFile(0x10);
        var referencePath = CreateImageFile(0x20);
        var mismatchPath = Path.ChangeExtension(referencePath, ".srec");
        File.Move(referencePath, mismatchPath);
        try
        {
            var viewModel = new MainViewModel();
            viewModel.Workspace.Open(basePath, OverlapDecision.OverwriteIncoming);
            viewModel.EditByte(0x1000, 0x30, OverlapDecision.OverwriteIncoming);
            var document = viewModel.Workspace.Document;
            MemoryImageFormatMismatch? prompted = null;

            var comparison = await new CompareCurrentImageWorkflow(viewModel).StartAsync(
                (_, _) => mismatchPath, () => Assert.Fail(), CancellationToken.None, null,
                confirmMismatch: mismatch => { prompted = mismatch; return false; });

            Assert.IsNull(comparison);
            Assert.IsNotNull(prompted);
            Assert.AreEqual(Path.GetFileName(mismatchPath), prompted!.FileName);
            Assert.AreSame(document, viewModel.Workspace.Document);
            Assert.IsTrue(document.IsModified);
            Assert.AreEqual(0, viewModel.Workspace.ComparisonViews.Count);
        }
        finally { File.Delete(basePath); File.Delete(mismatchPath); }
    }

    [TestMethod]
    public async Task MismatchedNewBaseCanBeCancelledBeforeOpeningIt()
    {
        var sourcePath = CreateImageFile(0x10);
        var mismatchPath = Path.ChangeExtension(sourcePath, ".s19");
        File.Move(sourcePath, mismatchPath);
        try
        {
            var viewModel = new MainViewModel();
            var originalDocument = viewModel.Workspace.Document;
            MemoryImageFormatMismatch? prompted = null;

            var comparison = await new CompareCurrentImageWorkflow(viewModel).StartAsync(
                (_, _) => mismatchPath, () => Assert.Fail(), CancellationToken.None, null,
                confirmMismatch: mismatch => { prompted = mismatch; return false; });

            Assert.IsNull(comparison);
            Assert.IsNotNull(prompted);
            Assert.AreSame(originalDocument, viewModel.Workspace.Document);
            Assert.IsNull(viewModel.SourcePath);
        }
        finally { File.Delete(mismatchPath); }
    }

    [TestMethod]
    public async Task OpenAnywayUsesDetectedFormatForMismatchedReference()
    {
        var basePath = CreateImageFile(0x10);
        var referencePath = CreateImageFile(0x20);
        var mismatchPath = Path.ChangeExtension(referencePath, ".s28");
        File.Move(referencePath, mismatchPath);
        try
        {
            var viewModel = new MainViewModel();
            viewModel.Workspace.Open(basePath, OverlapDecision.OverwriteIncoming);

            var comparison = await new CompareCurrentImageWorkflow(viewModel).StartAsync(
                (_, _) => mismatchPath, () => Assert.Fail(), CancellationToken.None, null,
                confirmMismatch: mismatch =>
                {
                    Assert.AreEqual(MemoryImageExportFormat.SRecord, mismatch.ExpectedFormat);
                    Assert.AreEqual(MemoryImageExportFormat.IntelHex, mismatch.DetectedFormat);
                    return true;
                });

            Assert.IsNotNull(comparison);
            Assert.AreEqual(Path.GetFileName(mismatchPath), comparison!.NameB);
            Assert.AreEqual((byte)0x20, ReadByte(comparison.Comparison.FileB, 0x1000));
        }
        finally { File.Delete(basePath); File.Delete(mismatchPath); }
    }

    [TestMethod]
    public void MergeFormatMismatchCanBeCancelledBeforeTheMergeIsApplied()
    {
        var viewModel = new MainViewModel();
        var document = viewModel.Workspace.Document;
        var destinationImage = MemoryImage.FromRepeatedRange(0x1000, 0x1000, 0x10);
        var sourceImage = MemoryImage.FromRepeatedRange(0x1000, 0x1000, 0x20);
        var mismatch = new MemoryImageFormatMismatch("source.hex", MemoryImageExportFormat.IntelHex,
            MemoryImageExportFormat.SRecord);
        var destination = new LoadedMemoryImage(destinationImage, "destination.hex", MemoryImageExportFormat.IntelHex);
        var source = new LoadedMemoryImage(sourceImage, "source.hex", MemoryImageExportFormat.SRecord, mismatch);
        MemoryImageFormatMismatch? prompted = null;

        var mayContinue = new MemoryImageFileWorkflow(viewModel).ConfirmMergeFormats(destination, source,
            value => { prompted = value; return false; });

        Assert.IsFalse(mayContinue);
        Assert.AreSame(mismatch, prompted);
        Assert.AreSame(document, viewModel.Workspace.Document);
        Assert.AreEqual(0UL, viewModel.Workspace.Document.CurrentImage.OccupiedByteCount);
        Assert.AreEqual(0, viewModel.Workspace.ComparisonViews.Count);
    }

    [TestMethod]
    public void MergeFormatMismatchAllowsContinuingWhenUserAcceptsDetectedFormat()
    {
        var viewModel = new MainViewModel();
        var mismatch = new MemoryImageFormatMismatch("source.srec", MemoryImageExportFormat.SRecord,
            MemoryImageExportFormat.IntelHex);
        var destination = new LoadedMemoryImage(MemoryImage.Empty, "destination.hex", MemoryImageExportFormat.IntelHex);
        var source = new LoadedMemoryImage(MemoryImage.Empty, "source.srec", MemoryImageExportFormat.IntelHex, mismatch);

        var mayContinue = new MemoryImageFileWorkflow(viewModel).ConfirmMergeFormats(destination, source,
            value => ReferenceEquals(value, mismatch));

        Assert.IsTrue(mayContinue);
    }

    [TestMethod]
    public async Task CompareFromCompareUsesSameBaseDocumentAndEachReferenceKeepsLiveSharedEdits()
    {
        var basePath = CreateImageFile(0x10);
        var firstReferencePath = CreateImageFile(0x40);
        var secondReferencePath = CreateImageFile(0x30);
        try
        {
            var viewModel = new MainViewModel();
            viewModel.Workspace.Open(basePath, OverlapDecision.OverwriteIncoming);
            var baseDocument = viewModel.Workspace.Document;
            var workflow = new CompareCurrentImageWorkflow(viewModel);
            var first = await workflow.StartAsync((_, _) => firstReferencePath, () => { }, CancellationToken.None, null);
            var second = await workflow.StartAsync((_, _) => secondReferencePath, () => { }, CancellationToken.None, null,
                first!.BaseDocument);

            Assert.IsNotNull(first);
            Assert.IsNotNull(second);
            Assert.AreSame(baseDocument, first.BaseDocument);
            Assert.AreSame(baseDocument, second.BaseDocument);
            Assert.AreEqual((byte)0x40, ReadByte(first.Comparison.FileB, 0x1000));
            Assert.AreEqual((byte)0x30, ReadByte(second.Comparison.FileB, 0x1000));

            baseDocument.Editing.EditByte(0x1000, 0x40, OverlapDecision.OverwriteIncoming);
            Assert.AreEqual((byte)0x40, ReadByte(first.Comparison.FileA, 0x1000));
            Assert.AreEqual((byte)0x40, ReadByte(second.Comparison.FileA, 0x1000));
            Assert.AreEqual("No differences", first.Comparison.ComparisonStatus);
            Assert.IsTrue(baseDocument.Editing.Undo());
            Assert.AreEqual((byte)0x10, ReadByte(first.Comparison.FileA, 0x1000));
            Assert.AreNotEqual("No differences", first.Comparison.ComparisonStatus);
            Assert.IsTrue(baseDocument.Editing.Redo());
            Assert.AreEqual((byte)0x40, ReadByte(second.Comparison.FileA, 0x1000));

            viewModel.Workspace.OpenImage(MemoryImage.FromSegments([new MemorySegment(0x1000, [0x99])]),
                "another.hex", MemoryImageExportFormat.IntelHex);
            Assert.AreEqual((byte)0x99, ReadByte(viewModel.Workspace.Document.CurrentImage, 0x1000));
            Assert.IsFalse(viewModel.Workspace.Document.CanUndo, "A new base document starts with its own history.");
            Assert.IsTrue(baseDocument.CanUndo, "The earlier base keeps its independent edit history.");
            Assert.AreEqual((byte)0x40, ReadByte(baseDocument.CurrentImage, 0x1000));
            baseDocument.Editing.EditByte(0x1000, 0x50, OverlapDecision.OverwriteIncoming);
            Assert.AreEqual((byte)0x50, ReadByte(first.Comparison.FileA, 0x1000));
            Assert.AreEqual((byte)0x50, ReadByte(second.Comparison.FileA, 0x1000));
        }
        finally { File.Delete(basePath); File.Delete(firstReferencePath); File.Delete(secondReferencePath); }
    }

    private static byte ReadByte(MemoryImage image, uint address)
    {
        Assert.IsTrue(image.TryGetByte(address, out var value));
        return value;
    }

    private static string CreateImageFile(byte value)
    {
        var path = Path.Combine(Path.GetTempPath(), $"comparison-{Guid.NewGuid():N}.hex");
        new MemoryImageFileService().SaveAtomically(
            MemoryImage.FromSegments([new MemorySegment(0x1000, [value])]), path,
            MemoryImageExportFormat.IntelHex, 16, overwriteExisting: false);
        return path;
    }
}
