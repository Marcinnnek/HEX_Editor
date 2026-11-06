namespace HexEditor.Formats;

/// <summary>Describes a known filename extension that conflicts with the parsed content format.</summary>
public sealed record MemoryImageFormatMismatch(
    string FileName,
    MemoryImageExportFormat ExpectedFormat,
    MemoryImageExportFormat DetectedFormat)
{
    public string Message => $"The file '{FileName}' has an extension associated with {ExpectedFormat.GetDisplayName()}, " +
                            $"but its content is {DetectedFormat.GetDisplayName()}.";
}

public static class MemoryImageFormatClassifier
{
    public static MemoryImageFormatMismatch? GetExtensionMismatch(string? path, MemoryImageExportFormat detectedFormat)
    {
        if (string.IsNullOrWhiteSpace(path)) return null;

        var extension = Path.GetExtension(path);
        var expectedFormat = extension.ToLowerInvariant() switch
        {
            ".hex" or ".ihx" => MemoryImageExportFormat.IntelHex,
            ".s19" or ".s28" or ".s37" or ".srec" or ".mot" => MemoryImageExportFormat.SRecord,
            ".vbf" => MemoryImageExportFormat.Vbf,
            _ => (MemoryImageExportFormat?)null
        };

        return expectedFormat is { } expected && expected != detectedFormat
            ? new MemoryImageFormatMismatch(Path.GetFileName(path), expected, detectedFormat)
            : null;
    }
}
