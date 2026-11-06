using System.Buffers.Binary;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using HexEditor.Core;

namespace HexEditor.Formats;

/// <summary>Reads the Ford/Volvo VBF block stream; the header's erase ranges are metadata only.</summary>
internal static class VbfReader
{
    private const int MaximumHeaderBytes = 1024 * 1024;
    private static readonly Regex Field = new(@"(?im)\b(?<name>vbf_version|data_format_identifier|data_processing_method2?|datproc_method2?|file_checksum)\s*=\s*(?<value>[^;]+);", RegexOptions.Compiled);

    public static IReadOnlyList<MemorySegment> Read(Stream input, long startPosition, ulong maximumOccupiedBytes,
        CancellationToken cancellationToken, IProgress<MemoryImageFileProgress>? progress)
    {
        input.Position = startPosition;
        var header = ReadHeader(input, cancellationToken);
        var fields = Field.Matches(header).Cast<Match>().ToDictionary(
            match => match.Groups["name"].Value.ToLowerInvariant(),
            match => match.Groups["value"].Value.Trim().Trim('"'), StringComparer.OrdinalIgnoreCase);
        if (!fields.TryGetValue("vbf_version", out var versionText) ||
            !Version.TryParse(versionText, out var version) || version.Major is not (2 or 3) ||
            (version.Major == 2 && version.Minor is < 1 or > 6) ||
            (version.Major == 3 && version.Minor > 2))
            throw new MemoryImageImportException($"Unsupported VBF version '{versionText ?? "missing"}'.");
        if (!fields.TryGetValue("file_checksum", out var checksumText))
            throw new MemoryImageImportException("VBF file_checksum is missing.");
        var expectedFileCrc = ParseNumber(checksumText, "file_checksum");
        var dataFormat = fields.TryGetValue("data_format_identifier", out var dataFormatText)
            ? ParseNumber(dataFormatText, "data_format_identifier") : 0U;
        if ((dataFormat & 0x0F) != 0)
            throw new MemoryImageImportException($"Encrypted VBF data format 0x{dataFormat:X} is unsupported.");
        var compression = dataFormat >> 4;
        if (compression is not (0 or 1))
            throw new MemoryImageImportException($"Unsupported VBF compression format 0x{dataFormat:X}.");
        var method = fields.TryGetValue("data_processing_method", out var methodText) ||
                     fields.TryGetValue("datproc_method", out methodText)
            ? ParseNumber(methodText!, "data_processing_method") : (uint?)null;
        if (compression == 1 && method is not (null or 21 or 26))
            throw new MemoryImageImportException($"Unsupported VBF data processing method {method}.");
        if (compression == 0 && method is not null and not 0)
            throw new MemoryImageImportException($"Unsupported VBF data processing method {method} for raw data.");
        var secondMethod = fields.TryGetValue("data_processing_method2", out var secondMethodText) ||
                           fields.TryGetValue("datproc_method2", out secondMethodText)
            ? ParseNumber(secondMethodText!, "data_processing_method2") : 0U;
        if (secondMethod != 0)
            throw new MemoryImageImportException($"Unsupported chained VBF data processing method {secondMethod}.");

        var segments = new List<MemorySegment>();
        var crc32 = 0xFFFFFFFFU;
        ulong totalDecoded = 0;
        var blockNumber = 0;
        Span<byte> blockHeader = stackalloc byte[8];
        Span<byte> checksum = stackalloc byte[2];
        while (input.Position < input.Length)
        {
            cancellationToken.ThrowIfCancellationRequested();
            blockNumber++;
            if (input.Length - input.Position < 10)
                throw new MemoryImageImportException($"VBF block {blockNumber} is truncated.");
            input.ReadExactly(blockHeader);
            crc32 = UpdateCrc32(crc32, blockHeader);
            var address = BinaryPrimitives.ReadUInt32BigEndian(blockHeader[..4]);
            var storedLength = BinaryPrimitives.ReadUInt32BigEndian(blockHeader[4..]);
            var storedLimit = compression == 0 ? maximumOccupiedBytes :
                maximumOccupiedBytes >= (ulong)int.MaxValue / 2 ? (ulong)int.MaxValue :
                maximumOccupiedBytes * 2 + 1024;
            if (storedLength == 0 || storedLength > storedLimit ||
                storedLength > int.MaxValue || (ulong)storedLength + 2 > (ulong)(input.Length - input.Position))
                throw new MemoryImageImportException($"VBF block {blockNumber} has an invalid stored length {storedLength}.");
            var stored = new byte[(int)storedLength];
            input.ReadExactly(stored);
            crc32 = UpdateCrc32(crc32, stored);
            input.ReadExactly(checksum);
            crc32 = UpdateCrc32(crc32, checksum);
            var remainingLimit = maximumOccupiedBytes - Math.Min(maximumOccupiedBytes, totalDecoded);
            var expectedBlockCrc = BinaryPrimitives.ReadUInt16BigEndian(checksum);
            var decoded = compression == 0 ? stored : DecodeCompressed(stored, remainingLimit,
                blockNumber, method, expectedBlockCrc, cancellationToken);
            totalDecoded = checked(totalDecoded + (ulong)decoded.Length);
            if (totalDecoded > maximumOccupiedBytes)
                throw new MemoryImageImportException($"VBF exceeds the maximum of {maximumOccupiedBytes:N0} decoded bytes.");
            if ((ulong)address + (ulong)decoded.Length - 1 > uint.MaxValue)
                throw new MemoryImageImportException($"VBF block {blockNumber} extends beyond the 32-bit address range.");
            if (Crc16(decoded) != expectedBlockCrc)
                throw new MemoryImageImportException($"VBF block {blockNumber} CRC16 does not match.");
            segments.Add(new MemorySegment(address, decoded));
            progress?.Report(new MemoryImageFileProgress("Reading VBF blocks", input.Position - startPosition,
                input.Length - startPosition));
        }
        if (segments.Count == 0)
            throw new MemoryImageImportException("VBF contains no data blocks.");
        if (~crc32 != expectedFileCrc)
            throw new MemoryImageImportException("VBF file_checksum CRC32 does not match.");
        return segments;
    }

    private static string ReadHeader(Stream input, CancellationToken cancellationToken)
    {
        using var bytes = new MemoryStream();
        var depth = 0;
        var foundHeader = false;
        var inQuote = false;
        var inComment = false;
        while (bytes.Length < MaximumHeaderBytes)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var next = input.ReadByte();
            if (next < 0) break;
            bytes.WriteByte((byte)next);
            if (next == '\n') inComment = false;
            if (inComment) continue;
            if (next == '"') { inQuote = !inQuote; continue; }
            if (inQuote) continue;
            if (next == '/' && input.Position < input.Length && input.ReadByte() is var second)
            {
                input.Position--;
                if (second == '/') { inComment = true; continue; }
            }
            if (next == '{') { depth++; foundHeader = true; }
            if (next == '}')
            {
                depth--;
                if (foundHeader && depth == 0) return Encoding.ASCII.GetString(bytes.ToArray());
            }
        }
        throw new MemoryImageImportException("VBF header is missing, malformed, or exceeds 1 MiB.");
    }

    private static uint ParseNumber(string text, string field)
    {
        var value = text.Trim();
        var hex = value.StartsWith("0x", StringComparison.OrdinalIgnoreCase);
        if (uint.TryParse(hex ? value[2..] : value, hex ? NumberStyles.HexNumber : NumberStyles.None,
                CultureInfo.InvariantCulture, out var parsed)) return parsed;
        throw new MemoryImageImportException($"Invalid VBF {field} value '{text}'.");
    }

    private static ushort Crc16(ReadOnlySpan<byte> bytes)
    {
        ushort crc = 0xFFFF;
        foreach (var value in bytes)
        {
            crc ^= (ushort)(value << 8);
            for (var bit = 0; bit < 8; bit++)
                crc = (ushort)((crc & 0x8000) != 0 ? (crc << 1) ^ 0x1021 : crc << 1);
        }
        return crc;
    }

    private static uint UpdateCrc32(uint crc, ReadOnlySpan<byte> bytes)
    {
        foreach (var value in bytes)
        {
            crc ^= value;
            for (var bit = 0; bit < 8; bit++)
                crc = (crc >> 1) ^ ((crc & 1) != 0 ? 0xEDB88320U : 0U);
        }
        return crc;
    }

    private static byte[] DecodeCompressed(byte[] stored, ulong limit, int blockNumber, uint? method,
        ushort expectedCrc, CancellationToken cancellationToken)
    {
        // VBF headers often record only the data format identifier. Vector documents
        // both method 21 (12/6) and Ford method 26 (10/4); the decoded CRC selects
        // the codec when no method identifier is present in the file.
        foreach (var (windowBits, lengthBits) in method == 21
                     ? new[] { (12, 6) }
                     : method == 26 ? new[] { (10, 4) } : new[] { (10, 4), (12, 6) })
        {
            try
            {
                var decoded = DecodeLzss(stored, limit, blockNumber, windowBits, lengthBits, cancellationToken);
                if (Crc16(decoded) == expectedCrc) return decoded;
            }
            catch (MemoryImageImportException) { }
        }
        throw new MemoryImageImportException($"VBF block {blockNumber} has an unsupported compression stream or invalid decoded CRC16.");
    }

    // Okumura LZSS bitstream: MSB first, with the window and match widths set by the method.
    private static byte[] DecodeLzss(byte[] stored, ulong limit, int blockNumber,
        int windowBits, int lengthBits, CancellationToken cancellationToken)
    {
        var output = new List<byte>();
        var windowSize = 1 << windowBits;
        var history = new byte[windowSize];
        Array.Fill(history, (byte)' ', 0, windowSize - (1 << lengthBits) - 1);
        var ringPosition = 0;
        var bitPosition = 0;
        while (bitPosition < stored.Length * 8)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var remaining = stored.Length * 8 - bitPosition;
            if (remaining <= 7 && RemainingBitsAreZero(stored, bitPosition)) break;
            var literal = ReadBits(1);
            if (literal == 1)
            {
                Require(8);
                Append((byte)ReadBits(8));
            }
            else
            {
                Require(windowBits + lengthBits);
                var index = ReadBits(windowBits);
                var count = ReadBits(lengthBits) + 2;
                if (index == 0) break;
                index--;
                for (var i = 0; i < count; i++) Append(history[(index + i) & (windowSize - 1)]);
            }
        }
        if (output.Count == 0)
            throw new MemoryImageImportException($"VBF block {blockNumber} decompresses to no data.");
        return output.ToArray();

        void Require(int bits)
        {
            if (stored.Length * 8 - bitPosition < bits)
                throw new MemoryImageImportException($"VBF block {blockNumber} has a truncated LZSS token.");
        }
        int ReadBits(int count)
        {
            var value = 0;
            for (var i = 0; i < count; i++, bitPosition++)
                value = (value << 1) | ((stored[bitPosition >> 3] >> (7 - (bitPosition & 7))) & 1);
            return value;
        }
        void Append(byte value)
        {
            if ((ulong)output.Count >= limit)
                throw new MemoryImageImportException($"VBF exceeds the maximum of {limit:N0} decoded bytes.");
            output.Add(value);
            history[ringPosition] = value;
            ringPosition = (ringPosition + 1) & (windowSize - 1);
        }
    }

    private static bool RemainingBitsAreZero(byte[] bytes, int fromBit)
    {
        for (var bit = fromBit; bit < bytes.Length * 8; bit++)
            if (((bytes[bit >> 3] >> (7 - (bit & 7))) & 1) != 0) return false;
        return true;
    }
}
