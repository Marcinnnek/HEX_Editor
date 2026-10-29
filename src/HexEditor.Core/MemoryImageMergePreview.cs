namespace HexEditor.Core;

public enum MemoryImageMergeChoice
{
    KeepSource,
    KeepDestination
}

public sealed record MemoryImageMergeRangeOverride(
    uint StartAddress,
    uint EndAddress,
    MemoryImageMergeChoice Choice);

public sealed record MemoryImageOverlapRange(
    uint StartAddress,
    uint EndAddress,
    long TotalByteCount,
    long IdenticalByteCount,
    long DifferentByteCount);

public sealed class MemoryImageMergePreview
{
    public MemoryImageMergePreview(MemoryImage destination, MemoryImage source)
    {
        ArgumentNullException.ThrowIfNull(destination);
        ArgumentNullException.ThrowIfNull(source);

        Destination = destination;
        Source = source;
        Overlaps = Array.AsReadOnly(SummarizeOverlaps(destination, source));
        TotalOverlapByteCount = Overlaps.Sum(range => range.TotalByteCount);
        IdenticalOverlapByteCount = Overlaps.Sum(range => range.IdenticalByteCount);
        DifferentOverlapByteCount = Overlaps.Sum(range => range.DifferentByteCount);
    }

    public MemoryImage Destination { get; }
    public MemoryImage Source { get; }
    public IReadOnlyList<MemoryImageOverlapRange> Overlaps { get; }
    public long TotalOverlapByteCount { get; }
    public long IdenticalOverlapByteCount { get; }
    public long DifferentOverlapByteCount { get; }

    /// <summary>Builds the merged address map. A null choice cancels without producing an image.</summary>
    public MemoryImage? Resolve(
        MemoryImageMergeChoice? defaultChoice,
        IEnumerable<MemoryImageMergeRangeOverride>? rangeOverrides = null)
    {
        if (defaultChoice is null) return null;

        var overrides = (rangeOverrides ?? Array.Empty<MemoryImageMergeRangeOverride>())
            .OrderBy(item => item.StartAddress)
            .ToArray();
        ValidateOverrides(overrides);

        if (Destination.Segments.Count == 0) return Source;
        if (Source.Segments.Count == 0) return Destination;

        var result = new List<MemorySegment>();
        var destinationIndex = 0;
        var sourceIndex = 0;
        var overrideIndex = 0;
        ulong cursor = Math.Min(Destination.Segments[0].StartAddress, Source.Segments[0].StartAddress);
        var maximum = Math.Max(Destination.Segments[^1].EndAddress, Source.Segments[^1].EndAddress);
        uint runStart = 0;
        uint previousAddress = 0;
        var run = new List<byte>();

        while (cursor <= maximum)
        {
            Advance(Destination.Segments, ref destinationIndex, cursor);
            Advance(Source.Segments, ref sourceIndex, cursor);
            var hasDestination = TryValue(Destination.Segments, destinationIndex, cursor, out var destinationValue);
            var hasSource = TryValue(Source.Segments, sourceIndex, cursor, out var sourceValue);
            if (!hasDestination && !hasSource)
            {
                Flush();
                cursor = Math.Min(NextStart(Destination.Segments, destinationIndex), NextStart(Source.Segments, sourceIndex));
                if (cursor == ulong.MaxValue) break;
                continue;
            }

            var address = (uint)cursor;
            if (run.Count == 0) runStart = address;
            else if ((ulong)previousAddress + 1 != cursor)
            {
                Flush();
                runStart = address;
            }

            var choice = GetChoice(address, overrides, ref overrideIndex, defaultChoice.Value);
            run.Add(hasSource && (!hasDestination || choice == MemoryImageMergeChoice.KeepSource)
                ? sourceValue
                : destinationValue);
            previousAddress = address;

            if (cursor == maximum) break;
            cursor++;
        }

        Flush();
        return MemoryImage.FromSegments(result);

        void Flush()
        {
            if (run.Count == 0) return;
            result.Add(new MemorySegment(runStart, run.ToArray()));
            run.Clear();
        }
    }

    private static MemoryImageOverlapRange[] SummarizeOverlaps(MemoryImage destination, MemoryImage source)
    {
        var ranges = new List<MemoryImageOverlapRange>();
        var destinationIndex = 0;
        var sourceIndex = 0;
        ulong currentStart = 0;
        ulong currentEnd = 0;
        long total = 0;
        long identical = 0;
        long different = 0;

        while (destinationIndex < destination.Segments.Count && sourceIndex < source.Segments.Count)
        {
            var destinationSegment = destination.Segments[destinationIndex];
            var sourceSegment = source.Segments[sourceIndex];
            var start = Math.Max(destinationSegment.StartAddress, sourceSegment.StartAddress);
            var end = Math.Min(destinationSegment.EndAddress, sourceSegment.EndAddress);
            if (start <= end)
            {
                if (total > 0 && currentEnd + 1 != start) Flush();
                if (total == 0) currentStart = start;

                for (var address = (ulong)start; address <= end; address++)
                {
                    var left = destinationSegment.Data.Span[(int)(address - destinationSegment.StartAddress)];
                    var right = sourceSegment.Data.Span[(int)(address - sourceSegment.StartAddress)];
                    total++;
                    if (left == right) identical++;
                    else different++;
                }
                currentEnd = end;
            }

            if (destinationSegment.EndAddress <= sourceSegment.EndAddress) destinationIndex++;
            if (sourceSegment.EndAddress <= destinationSegment.EndAddress) sourceIndex++;
        }

        Flush();
        return ranges.ToArray();

        void Flush()
        {
            if (total == 0) return;
            ranges.Add(new MemoryImageOverlapRange((uint)currentStart, (uint)currentEnd, total, identical, different));
            total = 0;
            identical = 0;
            different = 0;
        }
    }

    private static void ValidateOverrides(IReadOnlyList<MemoryImageMergeRangeOverride> overrides)
    {
        uint? priorEnd = null;
        foreach (var item in overrides)
        {
            if (item.EndAddress < item.StartAddress)
                throw new ArgumentOutOfRangeException(nameof(overrides), "A merge override must have a range end that is not before its start.");
            if (priorEnd is not null && item.StartAddress <= priorEnd.Value)
                throw new ArgumentException("Merge override ranges must not overlap.", nameof(overrides));
            if (!Enum.IsDefined(item.Choice))
                throw new ArgumentOutOfRangeException(nameof(overrides), "A merge override must choose source or destination values.");
            priorEnd = item.EndAddress;
        }
    }

    private static MemoryImageMergeChoice GetChoice(
        uint address,
        IReadOnlyList<MemoryImageMergeRangeOverride> overrides,
        ref int index,
        MemoryImageMergeChoice defaultChoice)
    {
        while (index < overrides.Count && overrides[index].EndAddress < address) index++;
        if (index < overrides.Count && overrides[index].StartAddress <= address) return overrides[index].Choice;
        return defaultChoice;
    }

    private static ulong NextStart(IReadOnlyList<MemorySegment> segments, int index) =>
        index < segments.Count ? segments[index].StartAddress : ulong.MaxValue;

    private static void Advance(IReadOnlyList<MemorySegment> segments, ref int index, ulong address)
    {
        while (index < segments.Count && segments[index].EndAddress < address) index++;
    }

    private static bool TryValue(IReadOnlyList<MemorySegment> segments, int index, ulong address, out byte value)
    {
        if (index < segments.Count && address >= segments[index].StartAddress && address <= segments[index].EndAddress)
        {
            value = segments[index].Data.Span[(int)(address - segments[index].StartAddress)];
            return true;
        }

        value = default;
        return false;
    }
}
