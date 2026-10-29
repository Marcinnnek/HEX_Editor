using System.Globalization;
using System.Text;
using HexEditor.Core;

namespace HexEditor.Formats;

internal static class IntelHexReader
{
    public static (MemoryImage Image, IntelHexMetadata Metadata) Read(Stream input)
    {
        ArgumentNullException.ThrowIfNull(input);
        if (!input.CanRead)
        {
            throw new ArgumentException("The input stream must be readable.", nameof(input));
        }

        using var reader = new StreamReader(
            input,
            Encoding.ASCII,
            detectEncodingFromByteOrderMarks: true,
            bufferSize: 4096,
            leaveOpen: true);

        var records = new List<(MemorySegment Segment, int LineNumber)>();
        uint addressBase = 0;
        uint? startLinearAddress = null;
        ushort? startSegmentCodeSegment = null;
        ushort? startSegmentInstructionPointer = null;
        var lineNumber = 0;
        var sawEndOfFile = false;

        while (reader.ReadLine() is { } line)
        {
            lineNumber++;

            if (sawEndOfFile)
            {
                throw Error(lineNumber, "No records are allowed after the end-of-file record.");
            }

            var (address, recordType, data) = ParseRecord(line, lineNumber);
            switch (recordType)
            {
                case 0x00:
                    if (data.Length > 0)
                    {
                        var start = (ulong)addressBase + address;
                        var end = start + (ulong)data.Length - 1;
                        if (end > uint.MaxValue)
                        {
                            throw Error(lineNumber, "The data record extends beyond the 32-bit address range.");
                        }

                        records.Add((new MemorySegment((uint)start, data), lineNumber));
                    }

                    break;
                case 0x01:
                    RequireControlRecord(address, data, 0, lineNumber);
                    sawEndOfFile = true;
                    break;
                case 0x02:
                    RequireControlRecord(address, data, 2, lineNumber);
                    addressBase = (uint)ReadUInt16(data) << 4;
                    break;
                case 0x03:
                    RequireControlRecord(address, data, 4, lineNumber);
                    startSegmentCodeSegment = ReadUInt16(data.AsSpan(0, 2));
                    startSegmentInstructionPointer = ReadUInt16(data.AsSpan(2, 2));
                    break;
                case 0x04:
                    RequireControlRecord(address, data, 2, lineNumber);
                    addressBase = (uint)ReadUInt16(data) << 16;
                    break;
                case 0x05:
                    RequireControlRecord(address, data, 4, lineNumber);
                    startLinearAddress = ReadUInt32(data);
                    break;
                default:
                    throw Error(lineNumber, $"Record type 0x{recordType:X2} is unsupported.");
            }
        }

        if (!sawEndOfFile)
        {
            throw Error(Math.Max(lineNumber, 1), "The file is missing an end-of-file record.");
        }

        var orderedRecords = records.OrderBy(record => record.Segment.StartAddress).ToArray();
        for (var index = 1; index < orderedRecords.Length; index++)
        {
            var previous = orderedRecords[index - 1];
            var current = orderedRecords[index];
            if (current.Segment.StartAddress <= previous.Segment.EndAddress)
            {
                throw Error(
                    current.LineNumber,
                    $"Data overlaps an earlier record at address range 0x{current.Segment.StartAddress:X8}-0x{Math.Min(current.Segment.EndAddress, previous.Segment.EndAddress):X8}.");
            }
        }

        var image = MemoryImage.FromSegments(orderedRecords.Select(record => record.Segment));
        var metadata = new IntelHexMetadata(
            startLinearAddress,
            startSegmentCodeSegment,
            startSegmentInstructionPointer);
        return (image, metadata);
    }

    private static (ushort Address, byte RecordType, byte[] Data) ParseRecord(string line, int lineNumber)
    {
        if (line.Length < 11 || line[0] != ':')
        {
            throw Error(lineNumber, "Expected an Intel HEX record starting with ':' and containing a complete header.");
        }

        var byteCount = ParseByte(line.AsSpan(1, 2), lineNumber, "byte count");
        var expectedLength = 11 + (byteCount * 2);
        if (line.Length != expectedLength)
        {
            throw Error(lineNumber, $"Record declares {byteCount} data bytes but its length is incorrect.");
        }

        var addressHigh = ParseByte(line.AsSpan(3, 2), lineNumber, "address");
        var addressLow = ParseByte(line.AsSpan(5, 2), lineNumber, "address");
        var address = (ushort)((addressHigh << 8) | addressLow);
        var recordType = ParseByte(line.AsSpan(7, 2), lineNumber, "record type");
        var data = new byte[byteCount];
        var checksum = (int)byteCount + addressHigh + addressLow + recordType;

        for (var index = 0; index < byteCount; index++)
        {
            var value = ParseByte(line.AsSpan(9 + (index * 2), 2), lineNumber, "data");
            data[index] = value;
            checksum += value;
        }

        var recordChecksum = ParseByte(line.AsSpan(expectedLength - 2, 2), lineNumber, "checksum");
        checksum += recordChecksum;
        if ((checksum & 0xFF) != 0)
        {
            throw Error(lineNumber, "The record checksum is invalid.");
        }

        return (address, recordType, data);
    }

    private static void RequireControlRecord(ushort address, byte[] data, int expectedDataLength, int lineNumber)
    {
        if (address != 0 || data.Length != expectedDataLength)
        {
            throw Error(
                lineNumber,
                $"This control record must use address 0000 and contain {expectedDataLength} data bytes.");
        }
    }

    private static byte ParseByte(ReadOnlySpan<char> value, int lineNumber, string fieldName)
    {
        if (!byte.TryParse(value, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var parsed))
        {
            throw Error(lineNumber, $"The {fieldName} field contains a non-hexadecimal value.");
        }

        return parsed;
    }

    private static ushort ReadUInt16(ReadOnlySpan<byte> data)
    {
        return (ushort)((data[0] << 8) | data[1]);
    }

    private static uint ReadUInt32(ReadOnlySpan<byte> data)
    {
        return ((uint)data[0] << 24) |
               ((uint)data[1] << 16) |
               ((uint)data[2] << 8) |
               data[3];
    }

    private static MemoryImageImportException Error(int lineNumber, string reason)
    {
        return new MemoryImageImportException(lineNumber, reason);
    }
}
