namespace HexEditor.Formats;

public sealed class MemoryImageFileService
{
    public void ExportAddressRange(
        HexEditor.Core.MemoryImage image,
        uint startAddress,
        uint endAddress,
        MemoryImageExportFormat format,
        int maxDataBytesPerRecord,
        Stream output,
        object? formatMetadata = null)
    {
        ArgumentNullException.ThrowIfNull(image);
        if (startAddress > endAddress)
            throw new ArgumentException("The start address must be less than or equal to the end address.", nameof(startAddress));

        var selectedSegments = new List<HexEditor.Core.MemorySegment>();
        foreach (var segment in image.Segments)
        {
            var start = Math.Max((ulong)startAddress, segment.StartAddress);
            var end = Math.Min((ulong)endAddress, segment.EndAddress);
            if (start > end) continue;
            var offset = checked((int)(start - segment.StartAddress));
            var count = checked((int)(end - start + 1));
            selectedSegments.Add(new HexEditor.Core.MemorySegment((uint)start, segment.Data.Span.Slice(offset, count)));
        }

        ExportSelectedImage(selectedSegments, format, maxDataBytesPerRecord, output, formatMetadata);
    }

    public void ExportSegments(
        HexEditor.Core.MemoryImage image,
        IEnumerable<int> segmentIndexes,
        MemoryImageExportFormat format,
        int maxDataBytesPerRecord,
        Stream output,
        object? formatMetadata = null)
    {
        ArgumentNullException.ThrowIfNull(image);
        ArgumentNullException.ThrowIfNull(segmentIndexes);
        var indexes = segmentIndexes.Distinct().Order().ToArray();
        if (indexes.Length == 0)
            throw new InvalidOperationException("Select at least one occupied segment to export.");

        var selectedSegments = new List<HexEditor.Core.MemorySegment>(indexes.Length);
        foreach (var index in indexes)
        {
            if ((uint)index >= (uint)image.Segments.Count)
                throw new ArgumentOutOfRangeException(nameof(segmentIndexes), index,
                    $"Segment index {index} is outside the available range 0-{image.Segments.Count - 1}.");
            selectedSegments.Add(image.Segments[index]);
        }

        ExportSelectedImage(selectedSegments, format, maxDataBytesPerRecord, output, formatMetadata);
    }

    public void SaveAtomically(
        HexEditor.Core.MemoryImage image,
        string path,
        MemoryImageExportFormat format,
        int maxDataBytesPerRecord,
        bool overwriteExisting,
        object? formatMetadata = null)
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
                Export(image, format, maxDataBytesPerRecord, output, formatMetadata);
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
        Stream output,
        object? formatMetadata = null)
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
                IntelHexWriter.Write(image, maxDataBytesPerRecord, output, GetIntelMetadata(formatMetadata));
                break;
            case MemoryImageExportFormat.SRecord:
                SRecordWriter.Write(image, maxDataBytesPerRecord, output, GetSRecordMetadata(formatMetadata));
                break;
        }
    }

    public static object? ConvertMetadata(object? metadata, MemoryImageExportFormat format, int dataRecordCount = 0)
    {
        var startAddress = GetExecutionStartAddress(metadata);
        return format switch
        {
            MemoryImageExportFormat.IntelHex when metadata is IntelHexMetadata intel => intel,
            MemoryImageExportFormat.IntelHex => startAddress is { } start ? new IntelHexMetadata(start, null, null) : new IntelHexMetadata(null, null, null),
            MemoryImageExportFormat.SRecord => metadata is SRecordMetadata sRecord
                ? new SRecordMetadata(startAddress, dataRecordCount, sRecord.HeaderAddress, sRecord.HeaderData?.ToArray())
                : new SRecordMetadata(startAddress, dataRecordCount),
            _ => throw new ArgumentOutOfRangeException(nameof(format))
        };
    }

    private static IntelHexMetadata? GetIntelMetadata(object? metadata) => metadata switch
    {
        IntelHexMetadata intel => intel,
        SRecordMetadata { StartAddress: { } start } => new IntelHexMetadata(start, null, null),
        _ => null
    };

    private static uint? GetExecutionStartAddress(object? metadata) => metadata switch
    {
        IntelHexMetadata { StartLinearAddress: { } start } => start,
        IntelHexMetadata { StartSegmentCodeSegment: { } code, StartSegmentInstructionPointer: { } pointer } => checked(((uint)code << 4) + pointer),
        SRecordMetadata { StartAddress: { } start } => start,
        _ => null
    };

    private static SRecordMetadata GetSRecordMetadata(object? metadata) => metadata switch
    {
        SRecordMetadata sRecord => sRecord,
        _ => new SRecordMetadata(GetExecutionStartAddress(metadata), 0)
    };

    private void ExportSelectedImage(
        IReadOnlyList<HexEditor.Core.MemorySegment> selectedSegments,
        MemoryImageExportFormat format,
        int maxDataBytesPerRecord,
        Stream output,
        object? formatMetadata = null)
    {
        if (selectedSegments.Count == 0)
            throw new InvalidOperationException("The selected address range contains no occupied bytes.");
        Export(HexEditor.Core.MemoryImage.FromSegments(selectedSegments), format, maxDataBytesPerRecord, output, formatMetadata);
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

    private static (IReadOnlyList<HexEditor.Core.MemorySegment> Records, MemoryImageExportFormat Format, object Metadata) ReadIntelHex(Stream input)
    {
        var (records, metadata) = IntelHexReader.Read(input);
        return (records, MemoryImageExportFormat.IntelHex, metadata);
    }

    private static (IReadOnlyList<HexEditor.Core.MemorySegment> Records, MemoryImageExportFormat Format, object Metadata) ReadSRecord(Stream input)
    {
        var (records, metadata) = SRecordReader.Read(input);
        return (records, MemoryImageExportFormat.SRecord, metadata);
    }
}
