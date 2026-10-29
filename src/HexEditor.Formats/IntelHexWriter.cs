using System.Globalization;
using System.Text;
using HexEditor.Core;

namespace HexEditor.Formats;

internal static class IntelHexWriter
{
    public static void Write(MemoryImage image, int maxPayload, Stream output)
    {
        using var writer = new StreamWriter(output, Encoding.ASCII, 1024, leaveOpen: true);
        uint? currentUpperAddress = null;

        foreach (var segment in image.Segments)
        {
            var offset = 0;
            while (offset < segment.ByteCount)
            {
                var address = (ulong)segment.StartAddress + (uint)offset;
                var upperAddress = (ushort)(address >> 16);
                if (currentUpperAddress != upperAddress)
                {
                    if (upperAddress != 0)
                    {
                        byte[] upperData = [(byte)(upperAddress >> 8), (byte)upperAddress];
                        WriteRecord(writer, 0, 0x04, upperData);
                    }

                    currentUpperAddress = upperAddress;
                }

                var lowAddress = (ushort)address;
                var bytesBeforeBoundary = 0x10000 - lowAddress;
                var count = Math.Min(maxPayload, Math.Min(bytesBeforeBoundary, segment.ByteCount - offset));
                WriteRecord(writer, lowAddress, 0x00, segment.Data.Span.Slice(offset, count));
                offset += count;
            }
        }

        WriteRecord(writer, 0, 0x01, ReadOnlySpan<byte>.Empty);
    }

    private static void WriteRecord(StreamWriter writer, ushort address, byte type, ReadOnlySpan<byte> data)
    {
        var checksum = data.Length + (address >> 8) + (address & 0xFF) + type;
        var line = new StringBuilder(11 + data.Length * 2);
        line.Append(':');
        AppendByte(line, (byte)data.Length);
        AppendByte(line, (byte)(address >> 8));
        AppendByte(line, (byte)address);
        AppendByte(line, type);
        foreach (var value in data)
        {
            AppendByte(line, value);
            checksum += value;
        }

        AppendByte(line, unchecked((byte)-checksum));
        writer.WriteLine(line.ToString());
    }

    private static void AppendByte(StringBuilder builder, byte value) =>
        builder.Append(value.ToString("X2", CultureInfo.InvariantCulture));
}
