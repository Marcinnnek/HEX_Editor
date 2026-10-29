using HexEditor.Core;

namespace HexEditor.Formats;

public sealed record MemoryImageImportResult(
    MemoryImage Image,
    MemoryImageExportFormat DetectedFormat,
    string? SourcePath,
    MemoryImageFormatMetadata? FormatMetadata)
{
    public string InputFormat => DetectedFormat.GetDisplayName();
}
