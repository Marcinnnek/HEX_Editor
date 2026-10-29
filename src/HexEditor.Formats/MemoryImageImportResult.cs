using HexEditor.Core;

namespace HexEditor.Formats;

public sealed record MemoryImageImportResult(
    MemoryImage Image,
    string InputFormat,
    string? SourcePath,
    object? FormatMetadata);
