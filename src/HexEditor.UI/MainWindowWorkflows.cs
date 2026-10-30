using System.IO;
using HexEditor.Core;
using HexEditor.Formats;
using HexEditor.Tools;

namespace HexEditor.UI;

/// <summary>Coordinates image edit use cases while leaving prompts and control state to the WPF view.</summary>
internal sealed class ImageEditWorkflow(MainViewModel viewModel)
{
    public void Insert(uint address, IReadOnlyList<byte> bytes, Func<int, string, OverlapDecision> chooseOverlap)
    {
        var preview = viewModel.Editing.PreviewInsert(address, bytes);
        var decision = preview.OverlapCount == 0 ? OverlapDecision.OverwriteIncoming : chooseOverlap(preview.OverlapCount, "insert/replace");
        viewModel.Editing.InsertBytes(address, bytes, decision);
    }

    public void Paste(uint address, Func<int, string, OverlapDecision> chooseOverlap)
    {
        var collisions = viewModel.Editing.GetPasteOverlapCount(address);
        viewModel.Editing.PasteAt(address, collisions == 0 ? OverlapDecision.OverwriteIncoming : chooseOverlap(collisions, "paste"));
    }

    public bool Relocate(int segmentIndex, uint destination, Func<MemoryRelocationPreview, RelocationOverlapDecision> chooseOverlap)
    {
        var preview = viewModel.Workspace.PrepareRelocation(segmentIndex, destination);
        return viewModel.Workspace.ApplyRelocation(preview, preview.Overlaps.Count == 0
            ? RelocationOverlapDecision.Overwrite
            : chooseOverlap(preview));
    }

    public void Copy(uint startAddress, uint endAddress) => viewModel.CopyRange(startAddress, endAddress);
    public void Cut(uint startAddress, uint endAddress) => viewModel.CutRange(startAddress, endAddress);
    public void Delete(uint startAddress, uint endAddress) => viewModel.DeleteRange(startAddress, endAddress);
}

/// <summary>Runs checksum operations and writes a previously calculated result.</summary>
internal sealed class ChecksumWorkflow(MainViewModel viewModel)
{
    public ChecksumCalculationResult Calculate(ChecksumRequest request) => viewModel.CalculateChecksum(request);

    public void Write(uint address, Func<int, string, OverlapDecision> chooseOverlap)
    {
        var result = viewModel.Checksum.GetWritableResult();
        var collisions = Enumerable.Range(0, result.WidthBits / 8)
            .Count(offset => viewModel.CurrentImage.TryGetByte(checked(address + (uint)offset), out _));
        var decision = collisions == 0 ? (OverlapDecision?)null : chooseOverlap(collisions, "checksum write");
        if (decision == OverlapDecision.Cancel) return;
        viewModel.WriteChecksumResult(address, decision);
    }
}

/// <summary>Loads images used by compare and merge workflows without changing the active image.</summary>
public sealed class ComparisonImageLoader(MainViewModel viewModel)
{
    public async Task<LoadedMemoryImage> LoadAsync(string path, CancellationToken cancellationToken,
        IProgress<MemoryImageFileProgress>? progress)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!string.IsNullOrWhiteSpace(viewModel.SourcePath) &&
            string.Equals(Path.GetFullPath(viewModel.SourcePath), Path.GetFullPath(path), StringComparison.OrdinalIgnoreCase))
        {
            return new LoadedMemoryImage(viewModel.CurrentImage, viewModel.SourcePath!, viewModel.SourceFormat);
        }

        return await LoadReferenceAsync(path, cancellationToken, progress);
    }

    public Task<LoadedMemoryImage> LoadReferenceAsync(string path, CancellationToken cancellationToken,
        IProgress<MemoryImageFileProgress>? progress) => Task.Run(() =>
    {
        cancellationToken.ThrowIfCancellationRequested();
        using var stream = File.OpenRead(path);
        var preview = new MemoryImageFileService().PrepareImport(stream, MemoryImage.Empty, path,
            cancellationToken, progress);
        cancellationToken.ThrowIfCancellationRequested();
        var imported = preview.Apply(OverlapDecision.OverwriteIncoming)
            ?? throw new InvalidOperationException("The selected reference image could not be imported.");
        return new LoadedMemoryImage(imported.Image, path, imported.DetectedFormat);
    }, cancellationToken);
}

/// <summary>Runs the picker-driven Compare flow against the current editable document.</summary>
public sealed class CompareCurrentImageWorkflow(MainViewModel viewModel)
{
    private readonly ComparisonImageLoader _loader = new(viewModel);

    public async Task<CompareWorkspaceTabViewModel?> StartAsync(
        Func<string, string?, string?> chooseFile, Action baseOpened,
        CancellationToken cancellationToken, IProgress<MemoryImageFileProgress>? progress,
        EditorDocument? baseDocument = null)
    {
        ArgumentNullException.ThrowIfNull(chooseFile);
        ArgumentNullException.ThrowIfNull(baseOpened);
        cancellationToken.ThrowIfCancellationRequested();

        if (baseDocument is null && viewModel.SourcePath is null && viewModel.CurrentImage.Segments.Count == 0)
        {
            var basePath = chooseFile("Select the base firmware image", null);
            if (basePath is null) return null;
            var preview = await viewModel.PrepareOpenAsync(basePath, cancellationToken, progress);
            cancellationToken.ThrowIfCancellationRequested();
            viewModel.ApplyImport(preview, OverlapDecision.OverwriteIncoming);
            baseOpened();
        }

        var document = baseDocument ?? viewModel.Workspace.Document;
        if (document.CurrentImage.Segments.Count == 0)
            throw new InvalidOperationException("The selected base document has no occupied bytes.");
        var referencePath = chooseFile("Select the reference firmware image",
            document.SourcePath is null ? null : Path.GetDirectoryName(document.SourcePath));
        if (referencePath is null) return null;

        var reference = await _loader.LoadReferenceAsync(referencePath, cancellationToken, progress);
        cancellationToken.ThrowIfCancellationRequested();
        var baseName = document.SourcePath is null ? "Untitled" : Path.GetFileName(document.SourcePath);
        var comparisonProgress = progress is null ? null : new Progress<ComparisonProgress>(update =>
            progress.Report(new MemoryImageFileProgress(update.Stage, update.Completed, update.Total)));
        return await Task.Run(() => new CompareWorkspaceTabViewModel(document.CurrentImage, reference.Image,
            baseName, Path.GetFileName(referencePath), document.SourcePath, reference.Path, document,
            cancellationToken, comparisonProgress), cancellationToken);
    }
}

/// <summary>Coordinates import, save, and export operations around the active memory image.</summary>
internal sealed class MemoryImageFileWorkflow(MainViewModel viewModel)
{
    private readonly MemoryImageFileService _fileService = new();

    public void Open(string path, Func<MemoryImageImportPreview, OverlapDecision> chooseOverlap)
    {
        var preview = viewModel.PrepareOpen(path);
        viewModel.ApplyImport(preview, chooseOverlap(preview));
    }

    public Task<MemoryImageImportPreview> PrepareOpenAsync(string path, CancellationToken cancellationToken,
        IProgress<MemoryImageFileProgress>? progress) => viewModel.PrepareOpenAsync(path, cancellationToken, progress);

    public void Save() => viewModel.Save();

    public Task SaveAsync(CancellationToken cancellationToken, IProgress<MemoryImageFileProgress>? progress) =>
        viewModel.SaveAsync(cancellationToken, progress);

    public void SaveAs(string path, MemoryImageExportFormat format, bool overwriteExisting) =>
        viewModel.SaveAs(path, format, overwriteExisting);

    public Task SaveAsAsync(string path, MemoryImageExportFormat format, bool overwriteExisting,
        CancellationToken cancellationToken, IProgress<MemoryImageFileProgress>? progress) =>
        viewModel.SaveAsAsync(path, format, overwriteExisting, cancellationToken, progress);

    public void ExportSegments(IEnumerable<int> segmentIndexes, MemoryImageExportFormat format, int payloadSize, Stream output,
        CancellationToken cancellationToken, IProgress<MemoryImageFileProgress>? progress, EditorDocument? document = null) =>
        _fileService.ExportSegments(document?.CurrentImage ?? viewModel.CurrentImage, segmentIndexes, format, payloadSize, output,
            document is null ? viewModel.SourceFormatMetadata : document.SourceFormatMetadata, cancellationToken, progress);

    public void ExportImage(MemoryImageExportFormat format, int payloadSize, Stream output,
        CancellationToken cancellationToken, IProgress<MemoryImageFileProgress>? progress, EditorDocument? document = null) =>
        _fileService.Export(document?.CurrentImage ?? viewModel.CurrentImage, format, payloadSize, output,
            document is null ? viewModel.SourceFormatMetadata : document.SourceFormatMetadata,
            cancellationToken, progress);

    public void ExportAddressRange(uint start, uint end, MemoryImageExportFormat format, int payloadSize, Stream output,
        CancellationToken cancellationToken, IProgress<MemoryImageFileProgress>? progress, EditorDocument? document = null) =>
        _fileService.ExportAddressRange(document?.CurrentImage ?? viewModel.CurrentImage, start, end, format, payloadSize, output,
            document is null ? viewModel.SourceFormatMetadata : document.SourceFormatMetadata, cancellationToken, progress);

    public bool ApplyMerge(LoadedMemoryImage destination, MemoryImageMergePreview preview,
        MemoryImageMergeChoice? defaultChoice, IEnumerable<MemoryImageMergeRangeOverride> rangeOverrides,
        Func<bool> confirmReplaceActiveImage)
    {
        if (!ReferenceEquals(viewModel.CurrentImage, destination.Image))
        {
            if (!confirmReplaceActiveImage()) return false;
            viewModel.LoadImage(destination.Image, destination.Path, destination.Format);
        }

        viewModel.ApplyMerge(preview, defaultChoice, rangeOverrides);
        return true;
    }

    public void Export(string path, Action<Stream, CancellationToken, IProgress<MemoryImageFileProgress>?> export,
        CancellationToken cancellationToken, IProgress<MemoryImageFileProgress>? progress)
    {
        _fileService.ExportAtomically(path, overwriteExisting: true, export, cancellationToken, progress);
    }
}

public sealed record LoadedMemoryImage(MemoryImage Image, string? Path, MemoryImageExportFormat? Format);
