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

    /// <summary>Copies occupied bytes into a bounded caller-owned buffer.</summary>
    public void CopyTo(ulong offset, Span<byte> destination)
    {
        if (offset > ByteCount || (ulong)destination.Length > ByteCount - offset)
        {
            throw new ArgumentOutOfRangeException(nameof(offset), "The requested bytes are outside this occupied range.");
        }

        if (!_isRepeated)
        {
            _data.Span.Slice(checked((int)offset), destination.Length).CopyTo(destination);
            return;
        }

        var pattern = _pattern.Span;
        var patternOffset = (int)(offset % (ulong)pattern.Length);
        for (var index = 0; index < destination.Length; index++)
        {
            destination[index] = pattern[patternOffset];
            patternOffset++;
            if (patternOffset == pattern.Length) patternOffset = 0;
        }
    }

    /// <summary>Returns a view clipped to an inclusive subrange without materializing repeated data.</summary>
    public MemoryImageRange Slice(uint startAddress, uint endAddress)
    {
        if (startAddress < StartAddress || endAddress > EndAddress || startAddress > endAddress)
        {
            throw new ArgumentOutOfRangeException(nameof(startAddress), "The requested subrange must be contained in this occupied range.");
        }

        if (_isRepeated)
        {
            var offset = (ulong)startAddress - StartAddress;
            var rotatedPattern = new byte[_pattern.Length];
            CopyTo(offset, rotatedPattern);
            return Repeated(startAddress, endAddress, rotatedPattern);
        }

        var dataOffset = checked((int)((ulong)startAddress - StartAddress));
        var count = checked((int)((ulong)endAddress - startAddress + 1));
        return new MemoryImageRange(startAddress, endAddress, _data.Slice(dataOffset, count), default, false);
    }
}
