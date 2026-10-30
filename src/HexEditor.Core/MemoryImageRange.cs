namespace HexEditor.Core;

/// <summary>A contiguous occupied range in an image. The range may cover the full 32-bit address space.</summary>
public sealed class MemoryImageRange
{
    private readonly ReadOnlyMemory<byte> _data;
    private readonly ReadOnlyMemory<byte> _pattern;
    private readonly bool _isRepeated;
    private readonly ulong _patternOffset;

    /// <summary>Whether this range stores bytes as a repeating pattern instead of a materialized segment.</summary>
    public bool IsRepeated => _isRepeated;

    /// <summary>The number of bytes in the repeating backing pattern, or zero for a materialized range.</summary>
    public int RepetitionLength => _isRepeated ? _pattern.Length : 0;

    private MemoryImageRange(uint startAddress, uint endAddress, ReadOnlyMemory<byte> data, ReadOnlyMemory<byte> pattern,
        bool isRepeated, ulong patternOffset = 0)
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

    internal static MemoryImageRange Repeated(uint startAddress, uint endAddress, ReadOnlyMemory<byte> pattern, ulong patternOffset = 0) =>
        new(startAddress, endAddress, default, pattern, true, patternOffset);

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

    /// <summary>Copies occupied bytes into a bounded caller-owned buffer.</summary>
    public void CopyTo(ulong offset, Span<byte> destination)
    {
        if (offset > ByteCount || (ulong)destination.Length > ByteCount - offset)
            throw new ArgumentOutOfRangeException(nameof(offset), "The requested bytes are outside this occupied range.");

        if (!_isRepeated)
        {
            _data.Span.Slice(checked((int)offset), destination.Length).CopyTo(destination);
            return;
        }

        var pattern = _pattern.Span;
        var patternOffset = (int)((_patternOffset + offset) % (ulong)pattern.Length);
        for (var index = 0; index < destination.Length; index++)
        {
            destination[index] = pattern[patternOffset++];
            if (patternOffset == pattern.Length) patternOffset = 0;
        }
    }

    /// <summary>Returns a view clipped to an inclusive subrange without materializing repeated data.</summary>
    public MemoryImageRange Slice(uint startAddress, uint endAddress)
    {
        if (startAddress < StartAddress || endAddress > EndAddress || startAddress > endAddress)
            throw new ArgumentOutOfRangeException(nameof(startAddress), "The requested subrange must be contained in this occupied range.");

        var offset = (ulong)startAddress - StartAddress;
        return _isRepeated
            ? new MemoryImageRange(startAddress, endAddress, default, _pattern, true, _patternOffset + offset)
            : new MemoryImageRange(startAddress, endAddress,
                _data.Slice(checked((int)offset), checked((int)((ulong)endAddress - startAddress + 1))), default, false);
    }

    public MemoryImageRange Rebase(uint startAddress)
    {
        var endAddress = (ulong)startAddress + ByteCount - 1;
        if (endAddress > uint.MaxValue) throw new ArgumentOutOfRangeException(nameof(startAddress));
        return new MemoryImageRange(startAddress, (uint)endAddress, _data, _pattern, _isRepeated, _patternOffset);
    }

    public bool ContentEquals(MemoryImageRange other)
    {
        ArgumentNullException.ThrowIfNull(other);
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

    internal MemorySegment? ToSmallSegment()
    {
        const ulong maximumMaterializedBytes = 1_048_576;
        if (ByteCount > maximumMaterializedBytes) return null;
        if (!_isRepeated) return new MemorySegment(StartAddress, _data.Span);
        var data = new byte[(int)ByteCount];
        CopyTo(0, data);
        return new MemorySegment(StartAddress, data);
    }
}
