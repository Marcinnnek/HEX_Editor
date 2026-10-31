using System.Globalization;
using HexEditor.Core;

namespace HexEditor.Formats;

public sealed class MemoryImageImportPreview
{
    private readonly MemoryImage _existingImage;
    private readonly IReadOnlyList<MemorySegment> _records;
    private readonly MemoryImage? _orderedReplacementImage;

    internal MemoryImageImportPreview(
        MemoryImage existingImage,
        IEnumerable<MemorySegment> records,
        MemoryImageExportFormat detectedFormat,
        string? sourcePath,
        MemoryImageFormatMetadata? metadata,
        ulong maximumOccupiedByteCount)
    {
        _existingImage = existingImage;
        _records = Array.AsReadOnly(records.ToArray());
        DetectedFormat = detectedFormat;
        SourcePath = sourcePath;
        FormatMetadata = metadata;
        if (existingImage.OccupiedByteCount == 0 &&
            TryBuildOrderedReplacement(_records, maximumOccupiedByteCount, out var replacementImage))
        {
            _orderedReplacementImage = replacementImage;
            OverlapSummary = OverlapSummary.Empty;
        }
        else
        {
            OverlapSummary = BuildOverlapSummary(existingImage, _records, maximumOccupiedByteCount);
        }
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

        if (_orderedReplacementImage is not null)
        {
            return new MemoryImageImportResult(_orderedReplacementImage, DetectedFormat, SourcePath, FormatMetadata);
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

    private static bool TryBuildOrderedReplacement(IReadOnlyList<MemorySegment> records,
        ulong maximumOccupiedByteCount, out MemoryImage image)
    {
        image = MemoryImage.Empty;
        ulong occupiedByteCount = 0;
        ulong previousEnd = 0;
        var hasPrevious = false;
        foreach (var record in records)
        {
            if (record.ByteCount == 0) continue;
            var recordByteCount = (ulong)record.ByteCount;
            var end = (ulong)record.StartAddress + recordByteCount - 1;
            if (end > uint.MaxValue || (hasPrevious && record.StartAddress <= previousEnd)) return false;
            occupiedByteCount = checked(occupiedByteCount + recordByteCount);
            EnsureWithinLimit(occupiedByteCount, maximumOccupiedByteCount);
            previousEnd = end;
            hasPrevious = true;
        }

        image = records.Count == 0 ? MemoryImage.Empty : MemoryImage.FromSegments(records);
        return true;
    }

    private static void EnsureWithinLimit(ulong count, ulong maximum)
    {
        if (count > maximum)
            throw new MemoryImageImportException(
                $"Image exceeds the maximum of {maximum.ToString("N0", CultureInfo.InvariantCulture)} occupied bytes.");
    }

    private static OverlapSummary BuildOverlapSummary(MemoryImage existing, IReadOnlyList<MemorySegment> records,
        ulong maximumOccupiedByteCount)
    {
        var ranges = new List<OverlapRange>();
        var run = new OverlapRun();
        var seenIncoming = new Dictionary<uint, byte>();
        var resultingOccupiedByteCount = existing.OccupiedByteCount;
        EnsureWithinLimit(resultingOccupiedByteCount, maximumOccupiedByteCount);
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
                else
                {
                    resultingOccupiedByteCount++;
                    EnsureWithinLimit(resultingOccupiedByteCount, maximumOccupiedByteCount);
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
