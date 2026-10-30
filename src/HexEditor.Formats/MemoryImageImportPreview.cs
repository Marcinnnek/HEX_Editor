using HexEditor.Core;

namespace HexEditor.Formats;

public sealed class MemoryImageImportPreview
{
    private readonly MemoryImage _existingImage;
    private readonly IReadOnlyList<MemorySegment> _records;

    internal MemoryImageImportPreview(
        MemoryImage existingImage,
        IEnumerable<MemorySegment> records,
        MemoryImageExportFormat detectedFormat,
        string? sourcePath,
        MemoryImageFormatMetadata? metadata)
    {
        _existingImage = existingImage;
        _records = Array.AsReadOnly(records.ToArray());
        DetectedFormat = detectedFormat;
        SourcePath = sourcePath;
        FormatMetadata = metadata;
        OverlapSummary = BuildOverlapSummary(existingImage, _records);
    }

    public MemoryImageExportFormat DetectedFormat { get; }
    public string InputFormat => DetectedFormat.GetDisplayName();
    public string? SourcePath { get; }
    public MemoryImageFormatMetadata? FormatMetadata { get; }
    public OverlapSummary OverlapSummary { get; }

    public MemoryImageImportResult? Apply(OverlapDecision decision)
    {
        if (!Enum.IsDefined(decision))
        {
            throw new ArgumentOutOfRangeException(nameof(decision));
        }

        if (decision == OverlapDecision.Cancel)
        {
            return null;
        }

        var changes = new Dictionary<uint, byte>();
        foreach (var record in _records)
        {
            for (var offset = 0; offset < record.ByteCount; offset++)
            {
                var address = record.StartAddress + (uint)offset;
                if (_existingImage.TryGetByte(address, out _) && decision == OverlapDecision.KeepExisting) continue;
                if (decision == OverlapDecision.KeepExisting && changes.ContainsKey(address)) continue;
                changes[address] = record.Data.Span[offset];
            }
        }
        return new MemoryImageImportResult(
            _existingImage.WithByteChanges(changes),
            DetectedFormat,
            SourcePath,
            FormatMetadata);
    }

    private static OverlapSummary BuildOverlapSummary(MemoryImage existing, IReadOnlyList<MemorySegment> records)
    {
        var ranges = new List<OverlapRange>();
        var run = new OverlapRun();
        var seenIncoming = new Dictionary<uint, byte>();
        var overlaps = new Dictionary<uint, (long Identical, long Different)>();
        foreach (var record in records)
        {
            for (var offset = 0; offset < record.ByteCount; offset++)
            {
                var address = record.StartAddress + (uint)offset;
                var value = record.Data.Span[offset];
                if (seenIncoming.TryGetValue(address, out var previousValue))
                {
                    AddOverlap(address, previousValue, value);
                }
                else if (existing.TryGetByte(address, out var existingValue))
                {
                    AddOverlap(address, existingValue, value);
                }
                seenIncoming[address] = value;
            }
        }
        foreach (var pair in overlaps.OrderBy(pair => pair.Key))
            run.Add(pair.Key, pair.Value.Identical, pair.Value.Different, ranges);
        run.Flush(ranges);
        return ranges.Count == 0 ? OverlapSummary.Empty : new OverlapSummary(ranges);

        void AddOverlap(uint address, byte previous, byte current)
        {
            overlaps.TryGetValue(address, out var counts);
            overlaps[address] = previous == current
                ? (counts.Identical + 1, counts.Different)
                : (counts.Identical, counts.Different + 1);
        }
    }

    private sealed class OverlapRun
    {
        private uint? _start;
        private uint _previous;
        private long _identicalCount;
        private long _differentCount;

        public void Add(uint address, long identicalCount, long differentCount, ICollection<OverlapRange> output)
        {
            if (identicalCount == 0 && differentCount == 0)
            {
                return;
            }

            if (_start is not null && (ulong)_previous + 1 != address)
            {
                Flush(output);
            }

            if (_start is null)
            {
                _start = address;
            }

            _previous = address;
            _identicalCount += identicalCount;
            _differentCount += differentCount;
        }

        public void Flush(ICollection<OverlapRange> output)
        {
            if (_start is null)
            {
                return;
            }

            output.Add(new OverlapRange(_start.Value, _previous, _identicalCount, _differentCount));
            _start = null;
            _identicalCount = 0;
            _differentCount = 0;
        }
    }
}
