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

        var selectedRanges = new List<HexEditor.Core.MemoryImageRange>();
        ulong selectedByteCount = 0;
        foreach (var range in image.Ranges)
        {
            var start = Math.Max((ulong)startAddress, range.StartAddress);
            var end = Math.Min((ulong)endAddress, range.EndAddress);
            if (start > end) continue;
            var selected = range.Slice((uint)start, (uint)end);
            selectedRanges.Add(selected);
            selectedByteCount = checked(selectedByteCount + selected.ByteCount);
        }

        ExportSelectedImage(selectedRanges, selectedByteCount, format, maxDataBytesPerRecord, output, formatMetadata, cancellationToken, progress);
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

        var selectedRanges = new List<HexEditor.Core.MemoryImageRange>(indexes.Length);
        foreach (var index in indexes)
        {
            if ((uint)index >= (uint)image.Ranges.Count)
                throw new ArgumentOutOfRangeException(nameof(segmentIndexes), index,
                    $"Segment index {index} is outside the available range 0-{image.Ranges.Count - 1}.");
            selectedRanges.Add(image.Ranges[index]);
        }

        var selectedByteCount = selectedRanges.Aggregate(0UL, (count, range) => checked(count + range.ByteCount));
        ExportSelectedImage(selectedRanges, selectedByteCount, format, maxDataBytesPerRecord, output,
            formatMetadata, cancellationToken, progress);
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
                IntelHexWriter.Write(image.Ranges, image.OccupiedByteCount, maxDataBytesPerRecord, output, GetIntelMetadata(formatMetadata), cancellationToken, progress);
                break;
            case MemoryImageExportFormat.SRecord:
                SRecordWriter.Write(image.Ranges, image.OccupiedByteCount, maxDataBytesPerRecord, output, GetSRecordMetadata(formatMetadata), cancellationToken, progress);
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
        IReadOnlyList<HexEditor.Core.MemoryImageRange> selectedRanges,
        ulong selectedByteCount,
        MemoryImageExportFormat format,
        int maxDataBytesPerRecord,
        Stream output,
        MemoryImageFormatMetadata? formatMetadata = null,
        CancellationToken cancellationToken = default,
        IProgress<MemoryImageFileProgress>? progress = null)
    {
        if (selectedRanges.Count == 0)
            throw new InvalidOperationException("The selected address range contains no occupied bytes.");
        if (format is not (MemoryImageExportFormat.IntelHex or MemoryImageExportFormat.SRecord))
            throw new ArgumentOutOfRangeException(nameof(format), format, "Choose Intel HEX or Motorola S-Record output.");
        if (maxDataBytesPerRecord is not (8 or 16 or 32))
            throw new ArgumentOutOfRangeException(nameof(maxDataBytesPerRecord), maxDataBytesPerRecord,
                "The maximum record payload must be 8, 16, or 32 bytes.");
        ArgumentNullException.ThrowIfNull(output);
        if (!output.CanWrite) throw new ArgumentException("The output stream must be writable.", nameof(output));

        if (format == MemoryImageExportFormat.IntelHex)
            IntelHexWriter.Write(selectedRanges, selectedByteCount, maxDataBytesPerRecord, output, GetIntelMetadata(formatMetadata), cancellationToken, progress);
        else
            SRecordWriter.Write(selectedRanges, selectedByteCount, maxDataBytesPerRecord, output, GetSRecordMetadata(formatMetadata), cancellationToken, progress);
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

        var originalPosition = input.CanSeek ? input.Position : 0;
        string? stagedPath = null;
        Stream content = input;
        if (!input.CanSeek)
        {
            stagedPath = Path.Combine(Path.GetTempPath(), $"hex-editor-import-{Guid.NewGuid():N}.tmp");
            try
            {
                using var staged = new FileStream(stagedPath, FileMode.CreateNew, FileAccess.Write, FileShare.None,
                    64 * 1024, FileOptions.SequentialScan);
                var buffer = new byte[64 * 1024];
                long copied = 0;
                int bytesRead;
                while ((bytesRead = input.Read(buffer, 0, buffer.Length)) != 0)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    staged.Write(buffer, 0, bytesRead);
                    copied += bytesRead;
                    progress?.Report(new MemoryImageFileProgress("Reading file", copied));
                }
                staged.Flush();
            }
            catch (IOException exception)
            {
                TryDelete(stagedPath);
                throw new IOException("Unable to stage the input file in temporary storage. Check available disk space and permissions.", exception);
            }
            catch
            {
                TryDelete(stagedPath);
                throw;
            }

            content = new FileStream(stagedPath, FileMode.Open, FileAccess.Read, FileShare.Read, 64 * 1024,
                FileOptions.SequentialScan);
        }

        try
        {
            var firstRecord = FindFirstRecord(content, originalPosition);
            if (firstRecord is null)
                throw new MemoryImageImportException(1, "The file contains no records.");

            content.Position = originalPosition;
            using var trackedContent = new ProgressReadStream(content, progress, cancellationToken);
            var (records, format, metadata) = firstRecord[0] switch
            {
                ':' => ReadIntelHex(trackedContent, cancellationToken, progress),
                'S' => ReadSRecord(trackedContent, cancellationToken, progress),
                _ => throw new MemoryImageImportException(1, "Unable to detect format. Expected Intel HEX ':' or Motorola S-record 'S' content.")
            };

            return new MemoryImageImportPreview(
                existingImage ?? HexEditor.Core.MemoryImage.Empty,
                records,
                format,
                sourcePath,
                metadata);
        }
        finally
        {
            if (!ReferenceEquals(content, input)) content.Dispose();
            if (stagedPath is not null) TryDelete(stagedPath);
        }
    }

    private static string? FindFirstRecord(Stream content, long startPosition)
    {
        content.Position = startPosition;
        using var probe = new StreamReader(content, System.Text.Encoding.ASCII, true, 1024, leaveOpen: true);
        while (probe.ReadLine() is { } line)
        {
            if (!string.IsNullOrWhiteSpace(line)) return line.TrimStart();
        }
        return null;
    }

    private static void TryDelete(string path)
    {
        try { if (File.Exists(path)) File.Delete(path); }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }

    private sealed class ProgressReadStream(Stream inner, IProgress<MemoryImageFileProgress>? progress,
        CancellationToken cancellationToken) : Stream
    {
        private long _completed;
        private readonly long? _total = inner.CanSeek ? inner.Length - inner.Position : null;

        public override bool CanRead => inner.CanRead;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
        public override void Flush() => throw new NotSupportedException();
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        public override int Read(byte[] buffer, int offset, int count)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var read = inner.Read(buffer, offset, count);
            if (read > 0)
            {
                _completed += read;
                progress?.Report(new MemoryImageFileProgress("Reading file", _completed, _total));
                cancellationToken.ThrowIfCancellationRequested();
            }
            return read;
        }

        public override int Read(Span<byte> buffer)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var read = inner.Read(buffer);
            if (read > 0)
            {
                _completed += read;
                progress?.Report(new MemoryImageFileProgress("Reading file", _completed, _total));
                cancellationToken.ThrowIfCancellationRequested();
            }
            return read;
        }

        protected override void Dispose(bool disposing) { }
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
