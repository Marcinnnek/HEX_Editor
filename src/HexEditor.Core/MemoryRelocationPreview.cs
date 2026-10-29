namespace HexEditor.Core;

public enum RelocationOverlapDecision
{
    Cancel,
    KeepExisting,
    Overwrite
}

public sealed record RelocationOverlap(uint Address, byte ExistingValue, byte IncomingValue)
{
    public bool ValuesAreIdentical => ExistingValue == IncomingValue;
}

public sealed class MemoryRelocationPreview
{
    private readonly MemoryImage _sourceImage;
    private readonly int _sourceSegmentIndex;
    private readonly uint _destinationBaseAddress;

    internal MemoryRelocationPreview(
        MemoryImage sourceImage,
        int sourceSegmentIndex,
        uint destinationBaseAddress,
        IReadOnlyList<RelocationOverlap> overlaps)
    {
        _sourceImage = sourceImage;
        _sourceSegmentIndex = sourceSegmentIndex;
        _destinationBaseAddress = destinationBaseAddress;
        Overlaps = Array.AsReadOnly(overlaps.ToArray());
    }

    public IReadOnlyList<RelocationOverlap> Overlaps { get; }

    public MemoryImage? Apply(RelocationOverlapDecision decision)
    {
        if (decision == RelocationOverlapDecision.Cancel)
        {
            return null;
        }

        if (decision is not (RelocationOverlapDecision.KeepExisting or RelocationOverlapDecision.Overwrite))
        {
            throw new ArgumentOutOfRangeException(nameof(decision));
        }

        var segments = _sourceImage.Segments
            .Where((_, index) => index != _sourceSegmentIndex)
            .ToList();
        var source = _sourceImage.Segments[_sourceSegmentIndex];
        var destinationEnd = (uint)((ulong)_destinationBaseAddress + (uint)source.ByteCount - 1);

        if (decision == RelocationOverlapDecision.Overwrite)
        {
            segments = segments.SelectMany(segment => RemoveRange(segment, _destinationBaseAddress, destinationEnd)).ToList();
            segments.Add(new MemorySegment(_destinationBaseAddress, source.Data.Span));
        }
        else
        {
            var cursor = 0;
            var destinationOverlaps = segments
                .Where(segment => segment.EndAddress >= _destinationBaseAddress && segment.StartAddress <= destinationEnd)
                .ToArray();
            foreach (var existing in destinationOverlaps)
            {
                var overlapStart = Math.Max((ulong)_destinationBaseAddress, existing.StartAddress);
                var overlapEnd = Math.Min((ulong)destinationEnd, existing.EndAddress);
                var incomingStart = checked((int)(overlapStart - _destinationBaseAddress));
                if (incomingStart > cursor)
                {
                    segments.Add(new MemorySegment(
                        checked(_destinationBaseAddress + (uint)cursor),
                        source.Data.Span.Slice(cursor, incomingStart - cursor)));
                }

                cursor = checked((int)(overlapEnd - _destinationBaseAddress + 1));
            }

            if (cursor < source.ByteCount)
            {
                segments.Add(new MemorySegment(
                    checked(_destinationBaseAddress + (uint)cursor),
                    source.Data.Span[cursor..]));
            }
        }

        return MemoryImage.FromSegments(segments);
    }

    private static IEnumerable<MemorySegment> RemoveRange(MemorySegment segment, uint rangeStart, uint rangeEnd)
    {
        if (segment.EndAddress < rangeStart || segment.StartAddress > rangeEnd)
        {
            yield return segment;
            yield break;
        }

        if (segment.StartAddress < rangeStart)
        {
            var length = checked((int)((ulong)rangeStart - segment.StartAddress));
            yield return new MemorySegment(segment.StartAddress, segment.Data.Span[..length]);
        }

        if (segment.EndAddress > rangeEnd)
        {
            var offset = checked((int)((ulong)rangeEnd + 1 - segment.StartAddress));
            yield return new MemorySegment(checked(rangeEnd + 1), segment.Data.Span[offset..]);
        }
    }
}
