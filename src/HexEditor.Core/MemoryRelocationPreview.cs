namespace HexEditor.Core;

public enum RelocationOverlapDecision { Cancel, KeepExisting, Overwrite }
public sealed record RelocationOverlap(uint Address, byte ExistingValue, byte IncomingValue)
{
    public bool ValuesAreIdentical => ExistingValue == IncomingValue;
}

public sealed class MemoryRelocationPreview
{
    private readonly MemoryImage _sourceImage;
    private readonly LogicalMemoryBlock _source;
    private readonly uint _destinationBaseAddress;

    internal MemoryRelocationPreview(MemoryImage sourceImage, LogicalMemoryBlock source, uint destinationBaseAddress,
        IReadOnlyList<RelocationOverlap> overlaps)
    {
        _sourceImage = sourceImage;
        _source = source;
        _destinationBaseAddress = destinationBaseAddress;
        Overlaps = Array.AsReadOnly(overlaps.ToArray());
    }

    public IReadOnlyList<RelocationOverlap> Overlaps { get; }

    public MemoryImage? Apply(RelocationOverlapDecision decision)
    {
        if (decision == RelocationOverlapDecision.Cancel) return null;
        if (decision is not (RelocationOverlapDecision.KeepExisting or RelocationOverlapDecision.Overwrite))
            throw new ArgumentOutOfRangeException(nameof(decision));

        var incoming = _sourceImage.Ranges
            .Where(range => range.StartAddress >= _source.StartAddress && range.EndAddress <= _source.EndAddress)
            .Select(range => range.Rebase(checked(_destinationBaseAddress + (range.StartAddress - _source.StartAddress))))
            .ToArray();
        var remaining = _sourceImage.WithoutRange(_source.StartAddress, _source.EndAddress);
        // KeepExisting retains any destination bytes; Overwrite replaces them with the relocated block.
        return remaining.WithRangesOverlay(incoming, decision == RelocationOverlapDecision.Overwrite);
    }
}
