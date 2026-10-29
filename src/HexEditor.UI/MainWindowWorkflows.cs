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
        var preview = viewModel.Editing.PrepareRelocation(segmentIndex, destination);
        return viewModel.Editing.ApplyRelocation(preview, preview.Overlaps.Count == 0
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
internal sealed class ComparisonImageLoader(MainViewModel viewModel)
{
    public LoadedMemoryImage Load(string path)
    {
        if (!string.IsNullOrWhiteSpace(viewModel.SourcePath) &&
            string.Equals(Path.GetFullPath(viewModel.SourcePath), Path.GetFullPath(path), StringComparison.OrdinalIgnoreCase))
        {
            return new LoadedMemoryImage(viewModel.CurrentImage, viewModel.SourcePath!, viewModel.SourceFormat);
        }

        var service = new MemoryImageFileService();
        using var stream = File.OpenRead(path);
        var preview = service.PrepareImport(stream, MemoryImage.Empty, path);
        var imported = preview.Apply(OverlapDecision.OverwriteIncoming)
            ?? throw new InvalidOperationException("The selected image could not be imported.");
        return new LoadedMemoryImage(imported.Image, path, imported.DetectedFormat);
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
        CancellationToken cancellationToken, IProgress<MemoryImageFileProgress>? progress) =>
        _fileService.ExportSegments(viewModel.CurrentImage, segmentIndexes, format, payloadSize, output,
            viewModel.SourceFormatMetadata, cancellationToken, progress);

    public void ExportAddressRange(uint start, uint end, MemoryImageExportFormat format, int payloadSize, Stream output,
        CancellationToken cancellationToken, IProgress<MemoryImageFileProgress>? progress) =>
        _fileService.ExportAddressRange(viewModel.CurrentImage, start, end, format, payloadSize, output,
            viewModel.SourceFormatMetadata, cancellationToken, progress);

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

internal sealed record LoadedMemoryImage(MemoryImage Image, string? Path, MemoryImageExportFormat? Format);
