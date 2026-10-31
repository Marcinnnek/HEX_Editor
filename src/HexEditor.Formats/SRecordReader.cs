using System.Diagnostics;
using System.Globalization;
using System.Text;
using HexEditor.Core;

namespace HexEditor.Formats;

internal static class SRecordReader
{
    private readonly record struct ParsedRecord(char Type, uint Address, byte[] Data, int LineNumber);

    public static (IReadOnlyList<MemorySegment> Records, SRecordMetadata Metadata) Read(
        Stream input, CancellationToken cancellationToken = default, IProgress<MemoryImageFileProgress>? progress = null)
    {
        ArgumentNullException.ThrowIfNull(input);
        if (!input.CanRead)
        {
            throw new ArgumentException("The input stream must be readable.", nameof(input));
        }

        using var reader = new StreamReader(input, Encoding.ASCII, true, 4096, leaveOpen: true);
        var dataRecords = new List<MemorySegment>();
        var dataRecordCount = 0;
        var lineNumber = 0;
        var lastProgressTimestamp = 0L;
        var sawTermination = false;
        var sawHeader = false;
        int? declaredDataRecordCount = null;
        uint? startAddress = null;
        char? dataRecordType = null;
        ParsedRecord? headerRecord = null;

        while (reader.ReadLine() is { } line)
        {
            cancellationToken.ThrowIfCancellationRequested();
            lineNumber++;
            var progressTimestamp = Stopwatch.GetTimestamp();
            if (lineNumber == 1 || Stopwatch.GetElapsedTime(lastProgressTimestamp, progressTimestamp) >= TimeSpan.FromMilliseconds(100))
            {
                progress?.Report(new MemoryImageFileProgress("Parsing records", lineNumber));
                lastProgressTimestamp = progressTimestamp;
            }
            if (sawTermination)
            {
                throw Error(lineNumber, "No records are allowed after the termination record.");
            }

            var record = ParseRecord(line, lineNumber);
            switch (record.Type)
            {
                case '0':
                    if (sawHeader || dataRecordCount > 0 || declaredDataRecordCount is not null)
                    {
                        throw Error(lineNumber, "The S0 header record must appear once before data and count records.");
                    }

                    sawHeader = true;
                    headerRecord = record;
                    break;
                case '1':
                case '2':
                case '3':
                    if (declaredDataRecordCount is not null)
                    {
                        throw Error(lineNumber, "No data records are allowed after the S5 or S6 count record.");
                    }

                    if (dataRecordType is not null && dataRecordType != record.Type)
                    {
                        throw Error(lineNumber, $"All data records must use the same S-record type and address width; S{record.Type} does not match S{dataRecordType}.");
                    }

                    dataRecordCount++;
                    if (record.Data.Length > 0)
                    {
                        var endAddress = (ulong)record.Address + (ulong)record.Data.Length - 1;
                        if (endAddress > uint.MaxValue)
                            throw Error(record.LineNumber, "The data record extends beyond the 32-bit address range.");
                        dataRecords.Add(new MemorySegment(record.Address, record.Data));
                    }
                    dataRecordType = record.Type;
                    break;
                case '5':
                case '6':
                    if (declaredDataRecordCount is not null)
                    {
                        throw Error(lineNumber, "Only one S5 or S6 count record is allowed.");
                    }

                    declaredDataRecordCount = checked((int)record.Address);
                    if (declaredDataRecordCount != dataRecordCount)
                    {
                        throw Error(lineNumber, $"The count record declares {declaredDataRecordCount} data records, but {dataRecordCount} were read.");
                    }

                    break;
                case '7':
                case '8':
                case '9':
                    var expectedTermination = dataRecordType switch
                    {
                        '1' => '9',
                        '2' => '8',
                        '3' => '7',
                        _ => record.Type
                    };
                    if (record.Type != expectedTermination)
                    {
                        throw Error(lineNumber, $"S{record.Type} does not match the address width of the data records; expected S{expectedTermination}.");
                    }

                    sawTermination = true;
                    startAddress = record.Address;
                    break;
            }
        }

        if (!sawTermination)
        {
            throw Error(Math.Max(lineNumber, 1), "The file is missing an S7, S8, or S9 termination record.");
        }

        if (declaredDataRecordCount is not null && declaredDataRecordCount != dataRecordCount)
        {
            throw Error(Math.Max(lineNumber, 1), "The S5 or S6 count record does not match the number of data records.");
        }

        return (dataRecords, new SRecordMetadata(
            startAddress,
            dataRecordCount,
            headerRecord is null ? null : checked((ushort)headerRecord.Value.Address),
            headerRecord?.Data.ToArray()));
    }

    private static ParsedRecord ParseRecord(string line, int lineNumber)
    {
        if (line.Length < 4 || line[0] != 'S' || line[1] is < '0' or > '9')
        {
            throw Error(lineNumber, "Expected an S-record beginning with S0 through S9.");
        }

        var type = line[1];
        if (type is not ('0' or '1' or '2' or '3' or '5' or '6' or '7' or '8' or '9'))
        {
            throw Error(lineNumber, $"S{type} is an unsupported S-record type.");
        }

        var count = ParseByte(line.AsSpan(2, 2), lineNumber, "byte count");
        if (line.Length != 4 + count * 2 || count < 1)
        {
            throw Error(lineNumber, $"Record declares {count} bytes but its length is incorrect.");
        }

        var addressByteCount = type switch
        {
            '0' or '1' or '5' or '9' => 2,
            '2' or '6' or '8' => 3,
            '3' or '7' => 4,
            _ => throw Error(lineNumber, $"S{type} is an unsupported S-record type.")
        };
        if (count < addressByteCount + 1)
        {
            throw Error(lineNumber, $"An S{type} record must contain a {addressByteCount}-byte address and checksum.");
        }

        var dataLength = count - addressByteCount - 1;
        var data = new byte[dataLength];
        var sum = count;
        uint address = 0;
        for (var index = 0; index < count; index++)
        {
            var value = ParseByte(line.AsSpan(4 + index * 2, 2), lineNumber, "record data");
            sum += value;
            if (index < addressByteCount) address = (address << 8) | value;
            else if (index < count - 1) data[index - addressByteCount] = value;
        }

        if ((sum & 0xFF) != 0xFF)
        {
            throw Error(lineNumber, "The record checksum is invalid.");
        }

        if ((type is '5' or '6') && dataLength != 0)
        {
            throw Error(lineNumber, "An S5 or S6 count record must not contain data bytes.");
        }

        if ((type is '7' or '8' or '9') && dataLength != 0)
        {
            throw Error(lineNumber, "An S7, S8, or S9 termination record must not contain data bytes.");
        }

        return new ParsedRecord(type, address, data, lineNumber);
    }

    private static byte ParseByte(ReadOnlySpan<char> value, int lineNumber, string fieldName)
    {
        if (!byte.TryParse(value, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var parsed))
        {
            throw Error(lineNumber, $"The {fieldName} field contains a non-hexadecimal value.");
        }

        return parsed;
    }

    private static MemoryImageImportException Error(int lineNumber, string reason) => new(lineNumber, reason);
}
