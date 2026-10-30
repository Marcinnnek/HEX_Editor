namespace HexEditor.Core;

public sealed class MemoryImage
{
    private readonly IReadOnlyList<MemorySegment> _segments;
    private readonly IReadOnlyList<MemoryImageRange> _ranges;
    private readonly IReadOnlyList<LogicalMemoryBlock> _blocks;

    private MemoryImage(IReadOnlyList<MemorySegment> segments, IReadOnlyList<MemoryImageRange>? ranges = null)
    {
        _segments = Array.AsReadOnly(segments.ToArray());
        _ranges = Array.AsReadOnly((ranges ?? segments.Select(MemoryImageRange.FromSegment).ToArray()).ToArray());
        _blocks = Array.AsReadOnly(CreateBlocks(_ranges));
        OccupiedByteCount = _ranges.Aggregate(0UL, (count, range) => checked(count + range.ByteCount));
    }

    public IReadOnlyList<MemorySegment> Segments => _segments;

    /// <summary>Occupied byte ranges, including ranges too large to materialize as a <see cref="MemorySegment"/>.</summary>
    public IReadOnlyList<MemoryImageRange> Ranges => _ranges;

    /// <summary>Maximal contiguous occupied address ranges, independent of backing range boundaries.</summary>
    public IReadOnlyList<LogicalMemoryBlock> Blocks => _blocks;

    private static LogicalMemoryBlock[] CreateBlocks(IReadOnlyList<MemoryImageRange> ranges)
    {
        var blocks = new List<LogicalMemoryBlock>();
        foreach (var range in ranges)
        {
            if (blocks.Count > 0 && (ulong)blocks[^1].EndAddress + 1 == range.StartAddress)
            {
                var prior = blocks[^1];
                blocks[^1] = prior with { EndAddress = range.EndAddress, ByteCount = prior.ByteCount + range.ByteCount };
            }
            else blocks.Add(new LogicalMemoryBlock(range.StartAddress, range.EndAddress, range.ByteCount));
        }
        return blocks.ToArray();
    }

    /// <summary>The number of explicitly occupied bytes, which may be 2^32.</summary>
    public ulong OccupiedByteCount { get; }

    public static MemoryImage Empty { get; } = new(Array.Empty<MemorySegment>());

    public static MemoryImage FromSegments(IEnumerable<MemorySegment> segments)
    {
        ArgumentNullException.ThrowIfNull(segments);

        var ranges = segments.OrderBy(segment => segment.StartAddress).Select(MemoryImageRange.FromSegment).ToArray();
        return ranges.Length == 0 ? Empty : FromRanges(ranges);
    }

    /// <summary>Creates a logically occupied inclusive address range filled with one byte value.</summary>
    public static MemoryImage FromRepeatedRange(uint startAddress, uint endAddress, byte value)
        => FromPatternRange(startAddress, endAddress, new[] { value });

    /// <summary>Creates an inclusive occupied range whose bytes repeat the supplied pattern from its start.</summary>
    public static MemoryImage FromPatternRange(uint startAddress, uint endAddress, ReadOnlySpan<byte> pattern)
    {
        if (endAddress < startAddress)
        {
            throw new ArgumentOutOfRangeException(nameof(endAddress), "The range end must not precede its start.");
        }
        if (pattern.IsEmpty)
        {
            throw new ArgumentException("A repeated range requires a non-empty byte pattern.", nameof(pattern));
        }

        return new MemoryImage(Array.Empty<MemorySegment>(), [MemoryImageRange.Repeated(startAddress, endAddress, pattern.ToArray())]);
    }

    public static MemoryImage FromRanges(IEnumerable<MemoryImageRange> ranges)
    {
        ArgumentNullException.ThrowIfNull(ranges);
        var ordered = ranges.OrderBy(range => range.StartAddress).ToArray();
        for (var i = 1; i < ordered.Length; i++)
            if (ordered[i].StartAddress <= ordered[i - 1].EndAddress)
                throw new ArgumentException("Memory ranges must not overlap.", nameof(ranges));
        var normalized = NormalizeSmallAdjacentRanges(ordered);
        var segments = normalized.Select(range => range.ToSmallSegment()).Where(segment => segment is not null).Cast<MemorySegment>().ToArray();
        return normalized.Length == 0 ? Empty : new MemoryImage(segments, normalized);
    }

    public MemoryImage WithoutRange(uint startAddress, uint endAddress)
    {
        if (endAddress < startAddress) throw new ArgumentOutOfRangeException(nameof(endAddress));
        var remaining = new List<MemoryImageRange>();
        foreach (var range in _ranges)
        {
            if (range.EndAddress < startAddress || range.StartAddress > endAddress) { remaining.Add(range); continue; }
            if (range.StartAddress < startAddress) remaining.Add(range.Slice(range.StartAddress, startAddress - 1));
            if (range.EndAddress > endAddress && endAddress < uint.MaxValue)
                remaining.Add(range.Slice(endAddress + 1, range.EndAddress));
        }
        return FromRanges(remaining);
    }

    /// <summary>Fills an inclusive address interval without materializing its bytes as one array.</summary>
    public MemoryImage WithPatternRange(uint startAddress, uint endAddress, ReadOnlySpan<byte> pattern, bool overwriteExisting)
    {
        if (endAddress < startAddress) throw new ArgumentOutOfRangeException(nameof(endAddress));
        if (pattern.IsEmpty) throw new ArgumentException("A fill pattern must not be empty.", nameof(pattern));
        var patternMemory = (ReadOnlyMemory<byte>)pattern.ToArray();
        var result = new List<MemoryImageRange>(_ranges.Count + 2);
        if (overwriteExisting)
        {
            foreach (var range in _ranges)
            {
                if (range.EndAddress < startAddress || range.StartAddress > endAddress) result.Add(range);
                else
                {
                    if (range.StartAddress < startAddress) result.Add(range.Slice(range.StartAddress, startAddress - 1));
                    if (range.EndAddress > endAddress && endAddress < uint.MaxValue) result.Add(range.Slice(endAddress + 1, range.EndAddress));
                }
            }
            result.Add(MemoryImageRange.Repeated(startAddress, endAddress, patternMemory));
        }
        else
        {
            ulong cursor = startAddress;
            foreach (var range in _ranges)
            {
                if (range.EndAddress < startAddress || range.StartAddress > endAddress)
                {
                    result.Add(range);
                    continue;
                }
                if (cursor < range.StartAddress)
                    result.Add(CreateFillRange((uint)cursor, range.StartAddress - 1, startAddress, patternMemory));
                result.Add(range);
                cursor = Math.Max(cursor, (ulong)range.EndAddress + 1);
            }
            if (cursor <= endAddress) result.Add(CreateFillRange((uint)cursor, endAddress, startAddress, patternMemory));
        }
        return FromRanges(result);
    }

    private static MemoryImageRange CreateFillRange(uint start, uint end, uint fillStart, ReadOnlyMemory<byte> pattern) =>
        MemoryImageRange.Repeated(start, end, pattern, ((ulong)start - fillStart) % (ulong)pattern.Length);

    /// <summary>Overlays logical occupied ranges while preserving their bounded or repeated backing.</summary>
    public MemoryImage WithRangesOverlay(IEnumerable<MemoryImageRange> incomingRanges, bool overwriteExisting)
    {
        ArgumentNullException.ThrowIfNull(incomingRanges);
        var incoming = incomingRanges.OrderBy(range => range.StartAddress).ToArray();
        for (var i = 1; i < incoming.Length; i++)
            if (incoming[i].StartAddress <= incoming[i - 1].EndAddress)
                throw new ArgumentException("Incoming memory ranges must not overlap.", nameof(incomingRanges));
        if (incoming.Length == 0) return this;

        var result = new List<MemoryImageRange>(_ranges.Count + incoming.Length);
        if (!overwriteExisting)
        {
            result.AddRange(_ranges);
            foreach (var added in incoming)
            {
                ulong cursor = added.StartAddress;
                foreach (var occupied in _ranges)
                {
                    if (occupied.EndAddress < added.StartAddress) continue;
                    if (occupied.StartAddress > added.EndAddress) break;
                    if (cursor < occupied.StartAddress)
                        result.Add(added.Slice((uint)cursor, occupied.StartAddress - 1));
                    cursor = Math.Max(cursor, (ulong)occupied.EndAddress + 1);
                    if (cursor > added.EndAddress) break;
                }
                if (cursor <= added.EndAddress) result.Add(added.Slice((uint)cursor, added.EndAddress));
            }
        }
        else
        {
            foreach (var occupied in _ranges)
            {
                ulong cursor = occupied.StartAddress;
                foreach (var added in incoming)
                {
                    if (added.EndAddress < occupied.StartAddress) continue;
                    if (added.StartAddress > occupied.EndAddress) break;
                    if (cursor < added.StartAddress) result.Add(occupied.Slice((uint)cursor, added.StartAddress - 1));
                    cursor = Math.Max(cursor, (ulong)added.EndAddress + 1);
                    if (cursor > occupied.EndAddress) break;
                }
                if (cursor <= occupied.EndAddress) result.Add(occupied.Slice((uint)cursor, occupied.EndAddress));
            }
            result.AddRange(incoming);
        }
        return FromRanges(result);
    }

    /// <summary>Returns an image with the specified occupied bytes added or replaced, preserving untouched logical ranges.</summary>
    public MemoryImage WithByteChanges(IReadOnlyDictionary<uint, byte> changes)
    {
        ArgumentNullException.ThrowIfNull(changes);
        if (changes.Count == 0) return this;
        var orderedChanges = changes.OrderBy(pair => pair.Key).ToArray();
        var output = new List<MemoryImageRange>();
        var changedAddresses = new HashSet<uint>();
        foreach (var range in _ranges)
        {
            var cursor = (ulong)range.StartAddress;
            foreach (var change in orderedChanges)
            {
                if (change.Key < range.StartAddress || change.Key > range.EndAddress) continue;
                if (cursor < change.Key) output.Add(range.Slice((uint)cursor, change.Key - 1));
                output.Add(MemoryImageRange.FromSegment(new MemorySegment(change.Key, [change.Value])));
                changedAddresses.Add(change.Key);
                cursor = (ulong)change.Key + 1;
            }
            if (cursor <= range.EndAddress) output.Add(range.Slice((uint)cursor, range.EndAddress));
        }

        foreach (var change in orderedChanges.Where(pair => !changedAddresses.Contains(pair.Key)))
            output.Add(MemoryImageRange.FromSegment(new MemorySegment(change.Key, [change.Value])));

        return FromRanges(output);
    }

    private static MemoryImageRange[] NormalizeSmallAdjacentRanges(MemoryImageRange[] ranges)
    {
        const ulong maximumMaterializedBytes = 1_048_576;
        var result = new List<MemoryImageRange>();
        for (var index = 0; index < ranges.Length;)
        {
            var endIndex = index;
            ulong count = ranges[index].ByteCount;
            while (endIndex + 1 < ranges.Length && (ulong)ranges[endIndex].EndAddress + 1 == ranges[endIndex + 1].StartAddress &&
                   count + ranges[endIndex + 1].ByteCount <= maximumMaterializedBytes)
            {
                endIndex++;
                count += ranges[endIndex].ByteCount;
            }
            if (endIndex == index)
            {
                result.Add(ranges[index++]);
                continue;
            }
            var data = new byte[(int)count];
            var offset = 0;
            for (var part = index; part <= endIndex; part++)
                for (var address = (ulong)ranges[part].StartAddress; address <= ranges[part].EndAddress; address++)
                {
                    ranges[part].TryGetByte((uint)address, out data[offset++]);
                }
            result.Add(MemoryImageRange.FromSegment(new MemorySegment(ranges[index].StartAddress, data)));
            index = endIndex + 1;
        }
        return result.ToArray();
    }

    public bool TryGetByte(uint address, out byte value)
    {
        var low = 0;
        var high = _ranges.Count - 1;

        while (low <= high)
        {
            var middle = low + ((high - low) / 2);
            var range = _ranges[middle];

            if (range.TryGetByte(address, out value))
            {
                return true;
            }

            if (address < range.StartAddress)
            {
                high = middle - 1;
            }
            else
            {
                low = middle + 1;
            }
        }

        value = default;
        return false;
    }

    public MemoryRelocationPreview PrepareRelocation(int segmentIndex, uint destinationBaseAddress)
    {
        var blocks = Blocks;
        if ((uint)segmentIndex >= (uint)blocks.Count)
        {
            throw new ArgumentOutOfRangeException(nameof(segmentIndex));
        }

        var source = blocks[segmentIndex];
        var destinationEnd = (ulong)destinationBaseAddress + source.ByteCount - 1;
        if (destinationEnd > uint.MaxValue)
        {
            throw new ArgumentOutOfRangeException(nameof(destinationBaseAddress), "The relocated segment extends beyond the 32-bit address range.");
        }

        var overlaps = new List<RelocationOverlap>();
        foreach (var range in _ranges.Where(range => range.EndAddress < source.StartAddress || range.StartAddress > source.EndAddress))
        {
            var start = Math.Max((ulong)destinationBaseAddress, range.StartAddress);
            var end = Math.Min(destinationEnd, range.EndAddress);
            if (start > end)
            {
                continue;
            }

            for (var address = start; address <= end; address++)
            {
                TryGetByte((uint)(source.StartAddress + (address - destinationBaseAddress)), out var incoming);
                _ = range.TryGetByte((uint)address, out var existing);
                overlaps.Add(new RelocationOverlap((uint)address, existing, incoming));
            }
        }

        return new MemoryRelocationPreview(this, source, destinationBaseAddress, overlaps);
    }
}

public sealed record LogicalMemoryBlock(uint StartAddress, uint EndAddress, ulong ByteCount);
