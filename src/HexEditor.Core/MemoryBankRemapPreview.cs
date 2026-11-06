namespace HexEditor.Core;

public sealed record BankRemapConfiguration(
    uint SourceStart, uint SourceEnd, ulong BankSize, ulong BankOffset, uint LinearBase);

/// <summary>Stages an image-wide bank address transformation before one atomic edit.</summary>
public sealed class MemoryBankRemapPreview
{
    public MemoryBankRemapPreview(MemoryImage source, BankRemapConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(configuration);
        if (configuration.SourceStart > configuration.SourceEnd)
            throw new ArgumentOutOfRangeException(nameof(configuration), "The source range end must not precede its start.");
        if (configuration.BankSize == 0 || configuration.BankOffset < configuration.BankSize)
            throw new ArgumentOutOfRangeException(nameof(configuration), "Bank offset must be at least the nonzero bank size.");

        Source = source;
        Configuration = configuration;
        var moved = new List<MemoryImageRange>();
        foreach (var range in source.Ranges)
        {
            var cursor = Math.Max((ulong)range.StartAddress, configuration.SourceStart);
            var last = Math.Min((ulong)range.EndAddress, configuration.SourceEnd);
            while (cursor <= last)
            {
                var relative = cursor - configuration.SourceStart;
                var bankIndex = relative / configuration.BankOffset;
                var withinBank = relative % configuration.BankOffset;
                if (withinBank >= configuration.BankSize)
                    throw new ArgumentException($"Occupied address 0x{cursor:X8} falls between source banks.", nameof(configuration));
                var sliceEnd = cursor + Math.Min(last - cursor + 1,
                    configuration.BankSize - withinBank) - 1;
                var target = (ulong)configuration.LinearBase + bankIndex * configuration.BankSize + withinBank;
                if (target + sliceEnd - cursor > uint.MaxValue)
                    throw new ArgumentOutOfRangeException(nameof(configuration), "Remapped data extend beyond the 32-bit address space.");
                moved.Add(range.Slice((uint)cursor, (uint)sliceEnd).Rebase((uint)target));
                cursor = sliceEnd + 1;
            }
        }
        if (moved.Count == 0)
            throw new InvalidOperationException("The selected source range contains no occupied bytes to remap.");
        Moved = MemoryImage.FromRanges(moved);
        Unchanged = source.WithoutRange(configuration.SourceStart, configuration.SourceEnd);
        Collisions = new MemoryImageMergePreview(Unchanged, Moved);
    }

    public MemoryImage Source { get; }
    public BankRemapConfiguration Configuration { get; }
    public MemoryImage Moved { get; }
    public MemoryImage Unchanged { get; }
    public MemoryImageMergePreview Collisions { get; }

    public MemoryImage? Resolve(MemoryImageMergeChoice? defaultChoice,
        IEnumerable<MemoryImageMergeRangeOverride>? overrides = null) =>
        Collisions.Resolve(defaultChoice, overrides);
}
