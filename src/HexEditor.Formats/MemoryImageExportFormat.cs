namespace HexEditor.Formats;

public enum MemoryImageExportFormat
{
    IntelHex,
    SRecord,
    Vbf
}

public static class MemoryImageExportFormatExtensions
{
    public static string GetDisplayName(this MemoryImageExportFormat format) => format switch
    {
        MemoryImageExportFormat.IntelHex => "Intel HEX",
        MemoryImageExportFormat.SRecord => "Motorola S-Record",
        MemoryImageExportFormat.Vbf => "VBF",
        _ => throw new ArgumentOutOfRangeException(nameof(format), format, "Unknown memory image format.")
    };
}
