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

        if (overrides.Length == 0)
            return Destination.WithRangesOverlay(Source.Ranges,
                overwriteExisting: defaultChoice == MemoryImageMergeChoice.KeepSource);

        // Keep all source-only addresses, then overlay source slices where the user chose it.
        // Working with logical ranges also preserves repeated ranges larger than a materialized segment.
        var result = Destination.WithRangesOverlay(Source.Ranges, overwriteExisting: false);
        var sourceWins = new List<MemoryImageRange>();
        var overrideIndex = 0;
        foreach (var sourceRange in Source.Ranges)
        {
            ulong cursor = sourceRange.StartAddress;
            while (cursor <= sourceRange.EndAddress)
            {
                while (overrideIndex < overrides.Length && overrides[overrideIndex].EndAddress < cursor)
                    overrideIndex++;
                var choice = GetChoice((uint)cursor, overrides, ref overrideIndex, defaultChoice.Value);
                var nextBoundary = (ulong)sourceRange.EndAddress + 1;
                if (overrideIndex < overrides.Length)
                {
                    var current = overrides[overrideIndex];
                    nextBoundary = Math.Min(nextBoundary, cursor < current.StartAddress
                        ? current.StartAddress : (ulong)current.EndAddress + 1);
                }
                if (choice == MemoryImageMergeChoice.KeepSource)
                    sourceWins.Add(sourceRange.Slice((uint)cursor, (uint)(nextBoundary - 1)));
                cursor = nextBoundary;
            }
        }
        return result.WithRangesOverlay(sourceWins, overwriteExisting: true);
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

        while (destinationIndex < destination.Ranges.Count && sourceIndex < source.Ranges.Count)
        {
            var destinationRange = destination.Ranges[destinationIndex];
            var sourceRange = source.Ranges[sourceIndex];
            var start = Math.Max(destinationRange.StartAddress, sourceRange.StartAddress);
            var end = Math.Min(destinationRange.EndAddress, sourceRange.EndAddress);
            if (start <= end)
            {
                if (total > 0 && currentEnd + 1 != start) Flush();
                if (total == 0) currentStart = start;

                var leftSlice = destinationRange.Slice(start, end);
                var rightSlice = sourceRange.Slice(start, end);
                if (leftSlice.ContentEquals(rightSlice))
                {
                    var count = checked((long)leftSlice.ByteCount);
                    total += count;
                    identical += count;
                }
                else for (var address = (ulong)start; address <= end; address++)
                {
                    leftSlice.TryGetByte((uint)address, out var left);
                    rightSlice.TryGetByte((uint)address, out var right);
                    total++;
                    if (left == right) identical++;
                    else different++;
                }
                currentEnd = end;
            }

            if (destinationRange.EndAddress <= sourceRange.EndAddress) destinationIndex++;
            if (sourceRange.EndAddress <= destinationRange.EndAddress) sourceIndex++;
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

}
