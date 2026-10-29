namespace HexEditor.Formats;

public sealed record OverlapRange(
    uint StartAddress,
    uint EndAddress,
    long IdenticalByteCount,
    long DifferentByteCount);

public sealed class OverlapSummary
{
    internal OverlapSummary(IReadOnlyList<OverlapRange> ranges)
    {
        Ranges = Array.AsReadOnly(ranges.ToArray());
        IdenticalByteCount = ranges.Sum(range => range.IdenticalByteCount);
        DifferentByteCount = ranges.Sum(range => range.DifferentByteCount);
    }

    public IReadOnlyList<OverlapRange> Ranges { get; }
    public long IdenticalByteCount { get; }
    public long DifferentByteCount { get; }
    public long TotalByteCount => IdenticalByteCount + DifferentByteCount;

    public static OverlapSummary Empty { get; } = new(Array.Empty<OverlapRange>());
}
