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
        MemoryImageFormatMetadata? formatMetadata = null,
        CancellationToken cancellationToken = default,
        IProgress<MemoryImageFileProgress>? progress = null)
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

        ExportSelectedImage(selectedSegments, format, maxDataBytesPerRecord, output, formatMetadata, cancellationToken, progress);
    }

    public void ExportSegments(
        HexEditor.Core.MemoryImage image,
        IEnumerable<int> segmentIndexes,
        MemoryImageExportFormat format,
        int maxDataBytesPerRecord,
        Stream output,
        MemoryImageFormatMetadata? formatMetadata = null,
        CancellationToken cancellationToken = default,
        IProgress<MemoryImageFileProgress>? progress = null)
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

        ExportSelectedImage(selectedSegments, format, maxDataBytesPerRecord, output, formatMetadata, cancellationToken, progress);
    }

    public void SaveAtomically(
        HexEditor.Core.MemoryImage image,
        string path,
        MemoryImageExportFormat format,
        int maxDataBytesPerRecord,
        bool overwriteExisting,
        MemoryImageFormatMetadata? formatMetadata = null,
        CancellationToken cancellationToken = default,
        IProgress<MemoryImageFileProgress>? progress = null)
    {
        ArgumentNullException.ThrowIfNull(image);
        WriteAtomically(path, overwriteExisting,
            (output, token, operationProgress) => Export(image, format, maxDataBytesPerRecord, output,
                formatMetadata, token, operationProgress), cancellationToken, progress);
    }

    public void ExportAtomically(string path, bool overwriteExisting,
        Action<Stream, CancellationToken, IProgress<MemoryImageFileProgress>?> export,
        CancellationToken cancellationToken = default, IProgress<MemoryImageFileProgress>? progress = null)
    {
        ArgumentNullException.ThrowIfNull(export);
        WriteAtomically(path, overwriteExisting, export, cancellationToken, progress);
    }

    private static void WriteAtomically(string path, bool overwriteExisting,
        Action<Stream, CancellationToken, IProgress<MemoryImageFileProgress>?> write,
        CancellationToken cancellationToken, IProgress<MemoryImageFileProgress>? progress)
    {
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
                write(output, cancellationToken, progress);
                cancellationToken.ThrowIfCancellationRequested();
                output.Flush(flushToDisk: true);
            }

            cancellationToken.ThrowIfCancellationRequested();

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
        MemoryImageFormatMetadata? formatMetadata = null,
        CancellationToken cancellationToken = default,
        IProgress<MemoryImageFileProgress>? progress = null)
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
                IntelHexWriter.Write(image, maxDataBytesPerRecord, output, GetIntelMetadata(formatMetadata), cancellationToken, progress);
                break;
            case MemoryImageExportFormat.SRecord:
                SRecordWriter.Write(image, maxDataBytesPerRecord, output, GetSRecordMetadata(formatMetadata), cancellationToken, progress);
                break;
        }
    }

    public static MemoryImageFormatMetadata ConvertMetadata(MemoryImageFormatMetadata? metadata, MemoryImageExportFormat format, int dataRecordCount = 0)
    {
        var startAddress = GetExecutionStartAddress(metadata);
        return format switch
        {
            MemoryImageExportFormat.IntelHex => MemoryImageFormatMetadata.FromIntelHex(
                metadata?.IntelHex ?? new IntelHexMetadata(startAddress, null, null)),
            MemoryImageExportFormat.SRecord => MemoryImageFormatMetadata.FromSRecord(new SRecordMetadata(
                startAddress, dataRecordCount, metadata?.SRecord?.HeaderAddress, metadata?.SRecord?.HeaderData?.ToArray())),
            _ => throw new ArgumentOutOfRangeException(nameof(format))
        };
    }

    private static uint? GetExecutionStartAddress(MemoryImageFormatMetadata? metadata)
    {
        if (metadata?.IntelHex is { } intel)
        {
            if (intel.StartLinearAddress is { } linear) return linear;
            if (intel.StartSegmentCodeSegment is { } code && intel.StartSegmentInstructionPointer is { } pointer)
                return checked(((uint)code << 4) + pointer);
        }
        return metadata?.SRecord?.StartAddress;
    }

    private static IntelHexMetadata? GetIntelMetadata(MemoryImageFormatMetadata? metadata) =>
        metadata?.IntelHex ?? (metadata?.SRecord?.StartAddress is { } start
            ? new IntelHexMetadata(start, null, null)
            : null);

    private static SRecordMetadata GetSRecordMetadata(MemoryImageFormatMetadata? metadata) =>
        metadata?.SRecord ?? new SRecordMetadata(GetExecutionStartAddress(metadata), 0);

    private void ExportSelectedImage(
        IReadOnlyList<HexEditor.Core.MemorySegment> selectedSegments,
        MemoryImageExportFormat format,
        int maxDataBytesPerRecord,
        Stream output,
        MemoryImageFormatMetadata? formatMetadata = null,
        CancellationToken cancellationToken = default,
        IProgress<MemoryImageFileProgress>? progress = null)
    {
        if (selectedSegments.Count == 0)
            throw new InvalidOperationException("The selected address range contains no occupied bytes.");
        Export(HexEditor.Core.MemoryImage.FromSegments(selectedSegments), format, maxDataBytesPerRecord, output, formatMetadata, cancellationToken, progress);
    }

    public MemoryImageImportPreview PrepareImport(
        Stream input,
        HexEditor.Core.MemoryImage? existingImage = null,
        string? sourcePath = null,
        CancellationToken cancellationToken = default,
        IProgress<MemoryImageFileProgress>? progress = null)
    {
        ArgumentNullException.ThrowIfNull(input);
        if (!input.CanRead)
        {
            throw new ArgumentException("The input stream must be readable.", nameof(input));
        }

        var content = new MemoryStream();
        var buffer = new byte[64 * 1024];
        var total = input.CanSeek ? input.Length - input.Position : (long?)null;
        long copied = 0;
        int bytesRead;
        while ((bytesRead = input.Read(buffer, 0, buffer.Length)) != 0)
        {
            cancellationToken.ThrowIfCancellationRequested();
            content.Write(buffer, 0, bytesRead);
            copied += bytesRead;
            progress?.Report(new MemoryImageFileProgress("Reading file", copied, total));
        }
        cancellationToken.ThrowIfCancellationRequested();
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
            ':' => ReadIntelHex(content, cancellationToken, progress),
            'S' => ReadSRecord(content, cancellationToken, progress),
            _ => throw new MemoryImageImportException(1, "Unable to detect format. Expected Intel HEX ':' or Motorola S-record 'S' content.")
        };

        return new MemoryImageImportPreview(
            existingImage ?? HexEditor.Core.MemoryImage.Empty,
            records,
            format,
            sourcePath,
            metadata);
    }

    private static (IReadOnlyList<HexEditor.Core.MemorySegment> Records, MemoryImageExportFormat Format, MemoryImageFormatMetadata Metadata) ReadIntelHex(Stream input, CancellationToken cancellationToken, IProgress<MemoryImageFileProgress>? progress)
    {
        var (records, metadata) = IntelHexReader.Read(input, cancellationToken, progress);
        return (records, MemoryImageExportFormat.IntelHex, MemoryImageFormatMetadata.FromIntelHex(metadata));
    }

    private static (IReadOnlyList<HexEditor.Core.MemorySegment> Records, MemoryImageExportFormat Format, MemoryImageFormatMetadata Metadata) ReadSRecord(Stream input, CancellationToken cancellationToken, IProgress<MemoryImageFileProgress>? progress)
    {
        var (records, metadata) = SRecordReader.Read(input, cancellationToken, progress);
        return (records, MemoryImageExportFormat.SRecord, MemoryImageFormatMetadata.FromSRecord(metadata));
    }
}
