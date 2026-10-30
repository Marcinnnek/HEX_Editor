namespace HexEditor.Core;

/// <summary>A contiguous occupied range in an image. The range may cover the full 32-bit address space.</summary>
public sealed class MemoryImageRange
{
    private readonly ReadOnlyMemory<byte> _data;
    private readonly ReadOnlyMemory<byte> _pattern;
    private readonly bool _isRepeated;

    private MemoryImageRange(uint startAddress, uint endAddress, ReadOnlyMemory<byte> data, ReadOnlyMemory<byte> pattern, bool isRepeated)
    {
        StartAddress = startAddress;
        EndAddress = endAddress;
        _data = data;
        _pattern = pattern;
        _isRepeated = isRepeated;
    }

    public uint StartAddress { get; }

    public uint EndAddress { get; }

    public ulong ByteCount => (ulong)EndAddress - StartAddress + 1;

    internal static MemoryImageRange FromSegment(MemorySegment segment) =>
        new(segment.StartAddress, segment.EndAddress, segment.Data, default, false);

    internal static MemoryImageRange Repeated(uint startAddress, uint endAddress, ReadOnlyMemory<byte> pattern) =>
        new(startAddress, endAddress, default, pattern, true);

    public bool TryGetByte(uint address, out byte value)
    {
        if (address < StartAddress || address > EndAddress)
        {
            value = default;
            return false;
        }

        var offset = (ulong)address - StartAddress;
        value = _isRepeated
            ? _pattern.Span[(int)(offset % (ulong)_pattern.Length)]
            : _data.Span[(int)offset];
        return true;
    }
}
