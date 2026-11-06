namespace HexEditor.Core;

public sealed record OrderedMemoryMergeStage(MemoryImage Image, ulong OverlappingByteCount);

/// <summary>Previews an ordered overlay of whole memory images without mutating its base.</summary>
public sealed class OrderedMemoryMergePreview
{
    public OrderedMemoryMergePreview(MemoryImage baseImage, IEnumerable<MemoryImage> sources,
        ulong maximumOccupiedByteCount = ulong.MaxValue)
    {
        BaseImage = baseImage ?? throw new ArgumentNullException(nameof(baseImage));
        ArgumentNullException.ThrowIfNull(sources);
        var stages = new List<OrderedMemoryMergeStage>();
        var result = baseImage;
        foreach (var source in sources)
        {
            ArgumentNullException.ThrowIfNull(source);
            var overlap = CountOverlappingAddresses(result.Ranges, source.Ranges);
            var resultingCount = checked(result.OccupiedByteCount + source.OccupiedByteCount - overlap);
            if (resultingCount > maximumOccupiedByteCount)
                throw new InvalidOperationException($"The merged image would exceed the limit of {maximumOccupiedByteCount:N0} occupied bytes.");
            result = result.WithRangesOverlay(source.Ranges, overwriteExisting: true);
            stages.Add(new OrderedMemoryMergeStage(source, overlap));
        }
        if (stages.Count == 0) throw new ArgumentException("Choose at least one image to merge.", nameof(sources));
        Stages = stages.AsReadOnly();
        Result = result;
    }

    public MemoryImage BaseImage { get; }
    public IReadOnlyList<OrderedMemoryMergeStage> Stages { get; }
    public MemoryImage Result { get; }

    private static ulong CountOverlappingAddresses(IReadOnlyList<MemoryImageRange> left, IReadOnlyList<MemoryImageRange> right)
    {
        var i = 0;
        var j = 0;
        ulong count = 0;
        while (i < left.Count && j < right.Count)
        {
            var start = Math.Max((ulong)left[i].StartAddress, right[j].StartAddress);
            var end = Math.Min((ulong)left[i].EndAddress, right[j].EndAddress);
            if (start <= end) count += end - start + 1;
            var leftEnd = left[i].EndAddress;
            var rightEnd = right[j].EndAddress;
            if (leftEnd <= rightEnd) i++;
            if (rightEnd <= leftEnd) j++;
        }
        return count;
    }
}
