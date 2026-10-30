using System.Text;
using HexEditor.Core;
using HexEditor.Formats;
using HexEditor.UI;

namespace HexEditor.Tests;

[TestClass]
public sealed class ComparisonImageLoaderTests
{
    [TestMethod]
    public async Task LoadAsync_ReportsFileReadProgressAndReturnsCompleteImage()
    {
        var path = CreateHexFile();
        try
        {
            var progress = new RecordingProgress();
            var loaded = await new ComparisonImageLoader(new MainViewModel()).LoadAsync(path, CancellationToken.None, progress);

            Assert.AreEqual(path, loaded.Path);
            Assert.AreEqual(MemoryImageExportFormat.IntelHex, loaded.Format);
            Assert.IsTrue(loaded.Image.TryGetByte(0x1234, out var value));
            Assert.AreEqual((byte)0xAA, value);
            Assert.IsGreaterThan(0, progress.Reports);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [TestMethod]
    public async Task Merge_source_loader_uses_disk_bytes_for_same_path_and_preserves_dirty_editor_preview_destination()
    {
        var path = CreateHexFile();
        try
        {
            var viewModel = new MainViewModel();
            viewModel.LoadImage(MemoryImage.FromSegments([new MemorySegment(0x1234, [0x11, 0x22])]), path,
                MemoryImageExportFormat.IntelHex);
            viewModel.EditByte(0x1234, 0x33, OverlapDecision.OverwriteIncoming);

            var loaded = await new ComparisonImageLoader(viewModel).LoadReferenceAsync(path, CancellationToken.None, null);
            var preview = viewModel.Editing.PreviewMerge(loaded.Image);

            Assert.AreSame(viewModel.Workspace.Document.CurrentImage, viewModel.CurrentImage);
            Assert.IsTrue(loaded.Image.TryGetByte(0x1234, out var savedByte));
            Assert.AreEqual((byte)0xAA, savedByte);
            Assert.IsTrue(preview.Source.TryGetByte(0x1234, out var previewSourceByte));
            Assert.AreEqual((byte)0xAA, previewSourceByte);
            Assert.IsTrue(preview.Destination.TryGetByte(0x1234, out var previewDestinationByte));
            Assert.AreEqual((byte)0x33, previewDestinationByte);
            Assert.IsTrue(viewModel.CurrentImage.TryGetByte(0x1234, out var unsavedByte));
            Assert.AreEqual((byte)0x33, unsavedByte);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [TestMethod]
    public async Task LoadAsync_CancellationDuringReadDoesNotReturnPartialImage()
    {
        var path = CreateHexFile();
        using var cancellation = new CancellationTokenSource();
        var progress = new CancellingProgress(cancellation);
        try
        {
            await Assert.ThrowsAsync<OperationCanceledException>(() =>
                new ComparisonImageLoader(new MainViewModel()).LoadAsync(path, cancellation.Token, progress));

            Assert.IsTrue(progress.WasReported);
        }
        finally
        {
            File.Delete(path);
        }
    }

    private static string CreateHexFile()
    {
        var path = Path.Combine(Path.GetTempPath(), $"comparison-{Guid.NewGuid():N}.hex");
        File.WriteAllText(path, ":02123400AABB53\n:00000001FF\n", Encoding.ASCII);
        return path;
    }

    private sealed class RecordingProgress : IProgress<MemoryImageFileProgress>
    {
        public int Reports { get; private set; }
        public void Report(MemoryImageFileProgress value) => Reports++;
    }

    private sealed class CancellingProgress(CancellationTokenSource cancellation) : IProgress<MemoryImageFileProgress>
    {
        public bool WasReported { get; private set; }

        public void Report(MemoryImageFileProgress value)
        {
            if (value.Stage != "Reading file") return;
            WasReported = true;
            cancellation.Cancel();
        }
    }
}
