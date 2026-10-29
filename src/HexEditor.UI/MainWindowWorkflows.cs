using System.Globalization;
using System.IO;
using HexEditor.Core;
using HexEditor.Formats;
using HexEditor.Tools;

namespace HexEditor.UI;

/// <summary>Coordinates image edit use cases while leaving prompts and control state to the WPF view.</summary>
internal sealed class ImageEditWorkflow(ImageEditingSession editing)
{
    public void Insert(uint address, IReadOnlyList<byte> bytes, Func<int, string, OverlapDecision> chooseOverlap)
    {
        var preview = editing.PreviewInsert(address, bytes);
        var decision = preview.OverlapCount == 0 ? OverlapDecision.OverwriteIncoming : chooseOverlap(preview.OverlapCount, "insert/replace");
        editing.InsertBytes(address, bytes, decision);
    }

    public void Paste(uint address, Func<int, string, OverlapDecision> chooseOverlap)
    {
        var collisions = editing.GetPasteOverlapCount(address);
        editing.PasteAt(address, collisions == 0 ? OverlapDecision.OverwriteIncoming : chooseOverlap(collisions, "paste"));
    }

    public bool Relocate(int segmentIndex, uint destination, Func<MemoryRelocationPreview, RelocationOverlapDecision> chooseOverlap)
    {
        var preview = editing.PrepareRelocation(segmentIndex, destination);
        return editing.ApplyRelocation(preview, preview.Overlaps.Count == 0
            ? RelocationOverlapDecision.Overwrite
            : chooseOverlap(preview));
    }
}

/// <summary>Builds and runs checksum operations from the settings supplied by the view.</summary>
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

    public static ChecksumRequest CreateRequest(
        uint start, uint end, string algorithm, int width, MissingAddressTreatment missingTreatment,
        byte? fillByte, ChecksumByteOrder byteOrder, string polynomial, string initial,
        bool reflectInput, bool reflectOutput, string finalXor)
    {
        return algorithm switch
        {
            "Additive" => ChecksumRequest.Additive(start, end, width, missingTreatment, fillByte, byteOrder),
            "CRC-16/CCITT-FALSE" => ChecksumRequest.Crc16CcittFalse(start, end, missingTreatment, fillByte, byteOrder),
            "CRC-32/ISO-HDLC" => ChecksumRequest.Crc32IsoHdlc(start, end, missingTreatment, fillByte, byteOrder),
            "Custom CRC" => ChecksumRequest.CustomCrc(start, end, width,
                ParseHexValue(polynomial), ParseHexValue(initial), reflectInput, reflectOutput,
                ParseHexValue(finalXor), missingTreatment, fillByte, byteOrder),
            _ => throw new ArgumentException("Choose a checksum algorithm.")
        };
    }

    private static ulong ParseHexValue(string text)
    {
        text = text.Trim();
        if (text.StartsWith("0x", StringComparison.OrdinalIgnoreCase)) text = text[2..];
        if (!ulong.TryParse(text, NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out var value))
            throw new ArgumentException("Enter a hexadecimal checksum parameter.");
        return value;
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

    public void Save() => viewModel.Save();

    public void SaveAs(string path, MemoryImageExportFormat format, bool overwriteExisting) =>
        viewModel.SaveAs(path, format, overwriteExisting);

    public void ExportSegments(IEnumerable<int> segmentIndexes, MemoryImageExportFormat format, int payloadSize, Stream output) =>
        _fileService.ExportSegments(viewModel.CurrentImage, segmentIndexes, format, payloadSize, output, viewModel.SourceFormatMetadata);

    public void ExportAddressRange(uint start, uint end, MemoryImageExportFormat format, int payloadSize, Stream output) =>
        _fileService.ExportAddressRange(viewModel.CurrentImage, start, end, format, payloadSize, output, viewModel.SourceFormatMetadata);

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

    public void Export(string path, Action<Stream> export)
    {
        using var output = File.Create(path);
        export(output);
        viewModel.ReportExported(Path.GetFileName(path));
    }
}

internal sealed record LoadedMemoryImage(MemoryImage Image, string? Path, MemoryImageExportFormat? Format);
