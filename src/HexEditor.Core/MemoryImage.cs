using System.Buffers;

namespace HexEditor.Core;

public sealed class MemoryImage
{
    private readonly IReadOnlyList<MemorySegment> _segments;
    private readonly IReadOnlyList<MemoryImageRange> _ranges;

    private MemoryImage(IReadOnlyList<MemorySegment> segments, IReadOnlyList<MemoryImageRange>? ranges = null)
    {
        _segments = Array.AsReadOnly(segments.ToArray());
        _ranges = Array.AsReadOnly((ranges ?? segments.Select(MemoryImageRange.FromSegment).ToArray()).ToArray());
        OccupiedByteCount = _ranges.Aggregate(0UL, (count, range) => checked(count + range.ByteCount));
    }

    public IReadOnlyList<MemorySegment> Segments => _segments;

    /// <summary>Occupied byte ranges, including ranges too large to materialize as a <see cref="MemorySegment"/>.</summary>
    public IReadOnlyList<MemoryImageRange> Ranges => _ranges;

    /// <summary>The number of explicitly occupied bytes, which may be 2^32.</summary>
    public ulong OccupiedByteCount { get; }

    public static MemoryImage Empty { get; } = new(Array.Empty<MemorySegment>());

    public static MemoryImage FromSegments(IEnumerable<MemorySegment> segments)
    {
        ArgumentNullException.ThrowIfNull(segments);

        var orderedSegments = segments.OrderBy(segment => segment.StartAddress).ToArray();
        if (orderedSegments.Length == 0)
        {
            return Empty;
        }

        var normalizedSegments = new List<MemorySegment>();
        var runStart = orderedSegments[0].StartAddress;
        var runEnd = orderedSegments[0].EndAddress;
        var runData = new ArrayBufferWriter<byte>(orderedSegments[0].ByteCount);
        runData.Write(orderedSegments[0].Data.Span);

        for (var index = 1; index < orderedSegments.Length; index++)
        {
            var next = orderedSegments[index];
            if (next.StartAddress <= runEnd)
            {
                throw new ArgumentException(
                    $"Memory segments overlap at address 0x{next.StartAddress:X8}.",
                    nameof(segments));
            }

            if ((ulong)runEnd + 1 == next.StartAddress)
            {
                runData.Write(next.Data.Span);
                runEnd = next.EndAddress;
                continue;
            }

            normalizedSegments.Add(new MemorySegment(runStart, runData.WrittenSpan));
            runStart = next.StartAddress;
            runEnd = next.EndAddress;
            runData = new ArrayBufferWriter<byte>(next.ByteCount);
            runData.Write(next.Data.Span);
        }

        normalizedSegments.Add(new MemorySegment(runStart, runData.WrittenSpan));
        return new MemoryImage(normalizedSegments);
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
        if ((uint)segmentIndex >= (uint)_segments.Count)
        {
            throw new ArgumentOutOfRangeException(nameof(segmentIndex));
        }

        var source = _segments[segmentIndex];
        var destinationEnd = (ulong)destinationBaseAddress + (uint)source.ByteCount - 1;
        if (destinationEnd > uint.MaxValue)
        {
            throw new ArgumentOutOfRangeException(nameof(destinationBaseAddress), "The relocated segment extends beyond the 32-bit address range.");
        }

        var overlaps = new List<RelocationOverlap>();
        foreach (var segment in _segments.Where((_, index) => index != segmentIndex))
        {
            var start = Math.Max((ulong)destinationBaseAddress, segment.StartAddress);
            var end = Math.Min(destinationEnd, segment.EndAddress);
            if (start > end)
            {
                continue;
            }

            for (var address = start; address <= end; address++)
            {
                var incoming = source.Data.Span[(int)(address - destinationBaseAddress)];
                _ = segment.TryGetByte((uint)address, out var existing);
                overlaps.Add(new RelocationOverlap((uint)address, existing, incoming));
            }
        }

        return new MemoryRelocationPreview(this, segmentIndex, destinationBaseAddress, overlaps);
    }
}
