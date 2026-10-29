namespace HexEditor.Formats;

public sealed class MemoryImageFileService
{
    public MemoryImageImportResult ImportIntelHex(Stream input, string? sourcePath = null)
    {
        var (image, metadata) = IntelHexReader.Read(input);
        return new MemoryImageImportResult(image, "Intel HEX", sourcePath, metadata);
    }
}
