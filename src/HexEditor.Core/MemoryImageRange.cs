namespace HexEditor.Core;

/// <summary>A contiguous occupied range in an image. The range may cover the full 32-bit address space.</summary>
public sealed class MemoryImageRange
{
    private readonly ReadOnlyMemory<byte> _data;
    private readonly ReadOnlyMemory<byte> _pattern;
    private readonly bool _isRepeated;
    private readonly ulong _patternOffset;

    private MemoryImageRange(uint startAddress, uint endAddress, ReadOnlyMemory<byte> data, ReadOnlyMemory<byte> pattern, bool isRepeated, ulong patternOffset = 0)
    {
        StartAddress = startAddress;
        EndAddress = endAddress;
        _data = data;
        _pattern = pattern;
        _isRepeated = isRepeated;
        _patternOffset = patternOffset;
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
            ? _pattern.Span[(int)((_patternOffset + offset) % (ulong)_pattern.Length)]
            : _data.Span[(int)offset];
        return true;
    }

    internal bool IsRepeated => _isRepeated;

    public bool ContentEquals(MemoryImageRange other)
    {
        if (StartAddress != other.StartAddress || EndAddress != other.EndAddress) return false;
        if (_isRepeated && other._isRepeated)
            return _patternOffset % (ulong)_pattern.Length == other._patternOffset % (ulong)other._pattern.Length &&
                _pattern.Span.SequenceEqual(other._pattern.Span);
        if (!_isRepeated && !other._isRepeated) return _data.Span.SequenceEqual(other._data.Span);
        if (ByteCount > 1_048_576) return false;
        for (var address = (ulong)StartAddress; address <= EndAddress; address++)
        {
            TryGetByte((uint)address, out var left);
            other.TryGetByte((uint)address, out var right);
            if (left != right) return false;
        }
        return true;
    }

    internal MemoryImageRange Slice(uint startAddress, uint endAddress)
    {
        if (startAddress < StartAddress || endAddress > EndAddress || endAddress < startAddress)
            throw new ArgumentOutOfRangeException(nameof(startAddress));
        var offset = (ulong)startAddress - StartAddress;
        return _isRepeated
            ? new MemoryImageRange(startAddress, endAddress, default, _pattern, true, _patternOffset + offset)
            : new MemoryImageRange(startAddress, endAddress, _data.Slice((int)offset, checked((int)((ulong)endAddress - startAddress + 1))), default, false);
    }

    internal MemorySegment? ToSmallSegment()
    {
        const ulong maximumMaterializedBytes = 1_048_576;
        if (ByteCount > maximumMaterializedBytes) return null;
        if (!_isRepeated) return new MemorySegment(StartAddress, _data.Span);
        var data = new byte[(int)ByteCount];
        for (var i = 0; i < data.Length; i++) data[i] = _pattern.Span[(int)((_patternOffset + (ulong)i) % (ulong)_pattern.Length)];
        return new MemorySegment(StartAddress, data);
    }
}
