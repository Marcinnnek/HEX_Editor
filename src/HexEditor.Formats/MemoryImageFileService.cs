namespace HexEditor.Formats;

public sealed class MemoryImageFileService
{
    public void SaveAtomically(
        HexEditor.Core.MemoryImage image,
        string path,
        MemoryImageExportFormat format,
        int maxDataBytesPerRecord,
        bool overwriteExisting)
    {
        ArgumentNullException.ThrowIfNull(image);
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        var targetPath = Path.GetFullPath(path);
        var directory = Path.GetDirectoryName(targetPath)!;
        Directory.CreateDirectory(directory);
        if (File.Exists(targetPath) && !overwriteExisting)
            throw new IOException($"The target file already exists: {targetPath}");

        var temporaryPath = Path.Combine(directory, $".{Path.GetFileName(targetPath)}.{Guid.NewGuid():N}.tmp");
        try
        {
            using (var output = new FileStream(temporaryPath, FileMode.CreateNew, FileAccess.Write, FileShare.None, 64 * 1024, FileOptions.WriteThrough))
            {
                Export(image, format, maxDataBytesPerRecord, output);
                output.Flush(flushToDisk: true);
            }

            if (File.Exists(targetPath))
            {
                if (!overwriteExisting) throw new IOException($"The target file already exists: {targetPath}");
                File.Move(temporaryPath, targetPath, overwrite: true);
            }
            else
            {
                File.Move(temporaryPath, targetPath);
            }
        }
        finally
        {
            if (File.Exists(temporaryPath)) File.Delete(temporaryPath);
        }
    }

    public void Export(
        HexEditor.Core.MemoryImage image,
        MemoryImageExportFormat format,
        int maxDataBytesPerRecord,
        Stream output)
    {
        ArgumentNullException.ThrowIfNull(image);
        ArgumentNullException.ThrowIfNull(output);
        if (!output.CanWrite)
        {
            throw new ArgumentException("The output stream must be writable.", nameof(output));
        }

        if (!Enum.IsDefined(format))
        {
            throw new ArgumentOutOfRangeException(nameof(format), format, "Choose Intel HEX or Motorola S-Record output.");
        }

        if (maxDataBytesPerRecord is not (8 or 16 or 32))
        {
            throw new ArgumentOutOfRangeException(
                nameof(maxDataBytesPerRecord),
                maxDataBytesPerRecord,
                "The maximum record payload must be 8, 16, or 32 bytes.");
        }

        switch (format)
        {
            case MemoryImageExportFormat.IntelHex:
                IntelHexWriter.Write(image, maxDataBytesPerRecord, output);
                break;
            case MemoryImageExportFormat.SRecord:
                SRecordWriter.Write(image, maxDataBytesPerRecord, output);
                break;
        }
    }

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
