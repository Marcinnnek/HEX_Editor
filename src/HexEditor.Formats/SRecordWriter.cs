using System.Globalization;
using System.Text;
using HexEditor.Core;

namespace HexEditor.Formats;

internal static class SRecordWriter
{
    public static void Write(IReadOnlyList<MemoryImageRange> ranges, ulong occupiedByteCount, int maxPayload, Stream output, SRecordMetadata metadata,
        CancellationToken cancellationToken = default, IProgress<MemoryImageFileProgress>? progress = null)
    {
        ArgumentNullException.ThrowIfNull(metadata);
        var headerData = metadata.HeaderData ?? [];
        if (metadata.HeaderAddress is not null && headerData.Length > byte.MaxValue - 3)
        {
            throw new ArgumentException("An S0 header can contain at most 252 data bytes.", nameof(metadata));
        }

        var highestAddress = ranges.Count == 0 ? 0 : ranges[^1].EndAddress;
        if (metadata.StartAddress is { } entryAddress) highestAddress = Math.Max(highestAddress, entryAddress);
        var addressBytes = highestAddress <= 0xFFFF ? 2 : highestAddress <= 0xFFFFFF ? 3 : 4;
        var dataType = addressBytes switch { 2 => '1', 3 => '2', _ => '3' };
        var terminationType = addressBytes switch { 2 => '9', 3 => '8', _ => '7' };

        using var writer = new StreamWriter(output, Encoding.ASCII, 1024, leaveOpen: true);
        if (metadata.HeaderAddress is { } headerAddress)
        {
            WriteRecord(writer, '0', headerAddress, 2, headerData);
        }

        var dataRecordCount = 0;
        long writtenBytes = 0;
        var totalBytes = checked((long)occupiedByteCount);
        var buffer = new byte[maxPayload];
        foreach (var range in ranges)
        {
            for (ulong offset = 0; offset < range.ByteCount;)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var count = (int)Math.Min((ulong)maxPayload, range.ByteCount - offset);
                range.CopyTo(offset, buffer.AsSpan(0, count));
                WriteRecord(
                    writer,
                    dataType,
                    (uint)((ulong)range.StartAddress + offset),
                    addressBytes,
                    buffer.AsSpan(0, count));
                dataRecordCount++;
                offset += (ulong)count;
                writtenBytes += count;
                progress?.Report(new MemoryImageFileProgress("Writing records", writtenBytes, totalBytes));
                cancellationToken.ThrowIfCancellationRequested();
            }
        }

        cancellationToken.ThrowIfCancellationRequested();
        if (dataRecordCount <= 0xFFFFFF)
        {
            var countAddressBytes = dataRecordCount <= ushort.MaxValue ? 2 : 3;
            var countType = countAddressBytes == 2 ? '5' : '6';
            WriteRecord(writer, countType, (uint)dataRecordCount, countAddressBytes, ReadOnlySpan<byte>.Empty);
        }

        WriteRecord(writer, terminationType, metadata.StartAddress ?? 0, addressBytes, ReadOnlySpan<byte>.Empty);
    }

    private static void WriteRecord(
        StreamWriter writer,
        char type,
        uint address,
        int addressBytes,
        ReadOnlySpan<byte> data)
    {
        var count = addressBytes + data.Length + 1;
        var sum = count;
        var line = new StringBuilder(4 + count * 2);
        line.Append('S').Append(type);
        AppendByte(line, (byte)count);

        for (var shift = (addressBytes - 1) * 8; shift >= 0; shift -= 8)
        {
            var value = (byte)(address >> shift);
            AppendByte(line, value);
            sum += value;
        }

        foreach (var value in data)
        {
            AppendByte(line, value);
            sum += value;
        }

        AppendByte(line, unchecked((byte)~sum));
        writer.WriteLine(line.ToString());
    }

    private static void AppendByte(StringBuilder builder, byte value) =>
        builder.Append(value.ToString("X2", CultureInfo.InvariantCulture));
}
