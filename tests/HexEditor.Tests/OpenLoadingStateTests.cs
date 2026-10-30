using HexEditor.Core;
using HexEditor.Formats;
using HexEditor.UI;

namespace HexEditor.Tests;

[TestClass]
public sealed class OpenLoadingStateTests
{
    [TestMethod]
    public async Task Replacement_loading_disables_edits_and_conflicting_file_commands_until_canceled()
    {
        var path = Path.Combine(Path.GetTempPath(), $"hex-editor-open-loading-{Guid.NewGuid():N}.hex");
        File.WriteAllText(path, string.Concat(Enumerable.Repeat(":01002000BB24\n", 10_000)));
        using var enteredProgress = new ManualResetEventSlim();
        using var continueLoad = new ManualResetEventSlim();
        using var cancellation = new CancellationTokenSource();
        var viewModel = new MainViewModel(MemoryImage.FromSegments([new MemorySegment(0x10, [0xAA])]));
        var progress = new BlockingProgress<MemoryImageFileProgress>(_ =>
        {
            enteredProgress.Set();
            continueLoad.Wait();
        });

        try
        {
            var load = viewModel.PrepareOpenReplacementAsync(path, cancellation.Token, progress);
            Assert.IsTrue(enteredProgress.Wait(TimeSpan.FromSeconds(10)), "The loader should report progress while preparing the replacement.");

            Assert.IsTrue(viewModel.IsOpenReplacementLoading);
            Assert.IsFalse(viewModel.CanEdit);
            Assert.IsFalse(viewModel.CanStartOpenCompareOrMerge);
            Assert.IsFalse(viewModel.ActiveCanSave);
            Assert.IsTrue(viewModel.CurrentImage.TryGetByte(0x10, out var oldByte));
            Assert.AreEqual((byte)0xAA, oldByte);

            cancellation.Cancel();
            continueLoad.Set();
            await Assert.ThrowsExactlyAsync<OperationCanceledException>(async () => await load);

            Assert.IsFalse(viewModel.IsOpenReplacementLoading);
            Assert.IsTrue(viewModel.CanEdit);
            Assert.IsTrue(viewModel.CanStartOpenCompareOrMerge);
            Assert.IsTrue(viewModel.CurrentImage.TryGetByte(0x10, out oldByte));
            Assert.AreEqual((byte)0xAA, oldByte);
        }
        finally
        {
            continueLoad.Set();
            File.Delete(path);
        }
    }

    [TestMethod]
    public async Task Failed_replacement_restores_commands_and_keeps_the_editor_and_compare_views()
    {
        var missingPath = Path.Combine(Path.GetTempPath(), $"hex-editor-missing-{Guid.NewGuid():N}.hex");
        var viewModel = new MainViewModel(MemoryImage.FromSegments([new MemorySegment(0x10, [0xAA])]));
        var comparison = new CompareWorkspaceTabViewModel(viewModel.CurrentImage, MemoryImage.Empty,
            "old.hex", "reference.hex", baseDocument: viewModel.Workspace.Document);
        viewModel.Workspace.RegisterComparisonView(comparison);

        await Assert.ThrowsExactlyAsync<FileNotFoundException>(async () =>
            await viewModel.PrepareOpenReplacementAsync(missingPath, CancellationToken.None, null));

        Assert.IsFalse(viewModel.IsOpenReplacementLoading);
        Assert.IsTrue(viewModel.CanEdit);
        Assert.IsTrue(viewModel.CanStartOpenCompareOrMerge);
        Assert.IsTrue(viewModel.CurrentImage.TryGetByte(0x10, out var oldByte));
        Assert.AreEqual((byte)0xAA, oldByte);
        CollectionAssert.AreEqual(new[] { comparison }, viewModel.Workspace.ComparisonViews.ToArray());
    }

    private sealed class BlockingProgress<T>(Action<T> report) : IProgress<T>
    {
        public void Report(T value) => report(value);
    }
}
