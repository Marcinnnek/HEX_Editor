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

    private static string CreateImageFile(byte value)
    {
        var path = Path.Combine(Path.GetTempPath(), $"comparison-{Guid.NewGuid():N}.hex");
        new MemoryImageFileService().SaveAtomically(
            MemoryImage.FromSegments([new MemorySegment(0x1000, [value])]), path,
            MemoryImageExportFormat.IntelHex, 16, overwriteExisting: false);
        return path;
    }
}
