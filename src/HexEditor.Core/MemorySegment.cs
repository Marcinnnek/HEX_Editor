namespace HexEditor.Core;

public sealed class MemorySegment
{
    private readonly byte[] _data;

    public MemorySegment(uint startAddress, ReadOnlySpan<byte> data)
    {
        if (data.IsEmpty)
        {
            throw new ArgumentException("A memory segment must contain at least one byte.", nameof(data));
        }

        var lastAddress = (ulong)startAddress + (ulong)data.Length - 1;
        if (lastAddress > uint.MaxValue)
        {
            throw new ArgumentOutOfRangeException(nameof(data), "The segment extends beyond the 32-bit address range.");
        }

        StartAddress = startAddress;
        _data = data.ToArray();
    }

    public uint StartAddress { get; }

    public uint EndAddress => (uint)((ulong)StartAddress + (ulong)_data.Length - 1);

    public int ByteCount => _data.Length;

    public ReadOnlyMemory<byte> Data => _data;

    public bool TryGetByte(uint address, out byte value)
    {
        if (address < StartAddress || address > EndAddress)
        {
            value = default;
            return false;
        }

        value = _data[(int)(address - StartAddress)];
        return true;
    }
}
