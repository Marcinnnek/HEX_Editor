using System.Buffers;

namespace HexEditor.Core;

public sealed class MemoryImage
{
    private readonly IReadOnlyList<MemorySegment> _segments;

    private MemoryImage(IReadOnlyList<MemorySegment> segments)
    {
        _segments = Array.AsReadOnly(segments.ToArray());
    }

    public IReadOnlyList<MemorySegment> Segments => _segments;

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

    public bool TryGetByte(uint address, out byte value)
    {
        var low = 0;
        var high = _segments.Count - 1;

        while (low <= high)
        {
            var middle = low + ((high - low) / 2);
            var segment = _segments[middle];

            if (segment.TryGetByte(address, out value))
            {
                return true;
            }

            if (address < segment.StartAddress)
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
}
