namespace HexEditor.Formats;

public sealed class MemoryImageFileService
{
    public MemoryImageImportPreview PrepareImport(
        Stream input,
        HexEditor.Core.MemoryImage? existingImage = null,
        string? sourcePath = null)
    {
        ArgumentNullException.ThrowIfNull(input);
        if (!input.CanRead)
        {
            throw new ArgumentException("The input stream must be readable.", nameof(input));
        }

        var content = new MemoryStream();
        input.CopyTo(content);
        content.Position = 0;

        using var probe = new StreamReader(content, System.Text.Encoding.ASCII, true, 1024, leaveOpen: true);
        string? firstRecord = null;
        while (probe.ReadLine() is { } line)
        {
            if (!string.IsNullOrWhiteSpace(line))
            {
                firstRecord = line.TrimStart();
                break;
            }
        }

        if (firstRecord is null)
        {
            throw new MemoryImageImportException(1, "The file contains no records.");
        }

        content.Position = 0;
        var formatMarker = firstRecord[0];
        var (records, format, metadata) = formatMarker switch
        {
            ':' => ReadIntelHex(content),
            'S' => ReadSRecord(content),
            _ => throw new MemoryImageImportException(1, "Unable to detect format. Expected Intel HEX ':' or Motorola S-record 'S' content.")
        };

        return new MemoryImageImportPreview(
            existingImage ?? HexEditor.Core.MemoryImage.Empty,
            records,
            format,
            sourcePath,
            metadata);
    }

    private static (IReadOnlyList<HexEditor.Core.MemorySegment> Records, string Format, object Metadata) ReadIntelHex(Stream input)
    {
        var (records, metadata) = IntelHexReader.Read(input);
        return (records, "Intel HEX", metadata);
    }

    private static (IReadOnlyList<HexEditor.Core.MemorySegment> Records, string Format, object Metadata) ReadSRecord(Stream input)
    {
        var (records, metadata) = SRecordReader.Read(input);
        return (records, "Motorola S-Record", metadata);
    }
}
