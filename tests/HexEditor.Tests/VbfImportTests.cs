using System.Buffers.Binary;
using System.Text;
using HexEditor.Core;
using HexEditor.Formats;
using HexEditor.UI;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace HexEditor.Tests;

[TestClass]
public sealed class VbfImportTests
{
    [TestMethod]
    public void RawBlocksImportAtTheirAddressesWithoutOccupyingEraseGaps()
    {
        using var input = BuildVbf(0, (0x1000, [0x12, 0x34]), (0x2000, [0x56]));
        var imported = new MemoryImageFileService().PrepareImport(input, sourcePath: "source.vbf")
            .Apply(OverlapDecision.OverwriteIncoming)!;
        Assert.AreEqual(MemoryImageExportFormat.Vbf, imported.DetectedFormat);
        Assert.AreEqual(3UL, imported.Image.OccupiedByteCount);
        Assert.IsFalse(imported.Image.TryGetByte(0x1002, out _));
        Assert.IsTrue(imported.Image.TryGetByte(0x2000, out var value));
        Assert.AreEqual((byte)0x56, value);
    }

    [TestMethod]
    public void CompressedFordTenFourLzssBlockImportsAndChecksDecodedCrc()
    {
        using var input = BuildVbf(0x10, (0x200, [0x41, 0x42, 0x43]));
        var imported = new MemoryImageFileService().PrepareImport(input).Apply(OverlapDecision.OverwriteIncoming)!;
        Assert.AreEqual(3UL, imported.Image.OccupiedByteCount);
        Assert.IsTrue(imported.Image.TryGetByte(0x202, out var value));
        Assert.AreEqual((byte)0x43, value);
    }

    [TestMethod]
    public void VectorTwelveSixLzssBackReferenceImports()
    {
        // Literal 'A', then a two-byte reference to the first history slot.
        var stored = PackBits("1" + "01000001" + "0" + "000000000001" + "000000");
        using var input = BuildVbf(0x10, 21, stored, (0x300, [0x41, 0x41, 0x41]));
        var imported = new MemoryImageFileService().PrepareImport(input).Apply(OverlapDecision.OverwriteIncoming)!;
        Assert.AreEqual(3UL, imported.Image.OccupiedByteCount);
        Assert.IsTrue(imported.Image.TryGetByte(0x302, out var value) && value == 0x41);
    }

    [TestMethod]
    public void InvalidBlockOrFileChecksumRejectsEntireImage()
    {
        using var blockError = BuildVbf(0, (0x100, [0x01]));
        var bytes = blockError.ToArray();
        bytes[^3] ^= 1;
        Assert.Throws<MemoryImageImportException>(() => new MemoryImageFileService().PrepareImport(new MemoryStream(bytes)));
        bytes = blockError.ToArray();
        bytes[^1] ^= 1;
        Assert.Throws<MemoryImageImportException>(() => new MemoryImageFileService().PrepareImport(new MemoryStream(bytes)));
    }

    [TestMethod]
    public void EncryptedAndUnknownCodecAreRejectedBeforeImport()
    {
        using var encrypted = BuildVbf(0x01, (0x100, [0x01]));
        using var unknown = BuildVbf(0x20, (0x100, [0x01]));
        Assert.Throws<MemoryImageImportException>(() => new MemoryImageFileService().PrepareImport(encrypted));
        Assert.Throws<MemoryImageImportException>(() => new MemoryImageFileService().PrepareImport(unknown));
    }

    [TestMethod]
    public void InternalBlockOverlapUsesTheImportDecision()
    {
        using var input = BuildVbf(0, (0x100, [0x11, 0x22]), (0x101, [0x33]));
        var preview = new MemoryImageFileService().PrepareImport(input);
        Assert.AreEqual(1L, preview.OverlapSummary.TotalByteCount);
        Assert.AreEqual(1L, preview.OverlapSummary.DifferentByteCount);
        Assert.IsTrue(preview.Apply(OverlapDecision.KeepExisting)!.Image.TryGetByte(0x101, out var kept) && kept == 0x22);
        Assert.IsTrue(preview.Apply(OverlapDecision.OverwriteIncoming)!.Image.TryGetByte(0x101, out var overwritten) && overwritten == 0x33);
        Assert.IsNull(preview.Apply(OverlapDecision.Cancel));
    }

    [TestMethod]
    public void CompressedStoredLengthCanExceedTheDecodedLimitWhenDecodedBytesFit()
    {
        using var input = BuildVbf(0x10, (0x100, [0x11, 0x22]));
        var imported = new MemoryImageFileService(2).PrepareImport(input).Apply(OverlapDecision.OverwriteIncoming)!;
        Assert.AreEqual(2UL, imported.Image.OccupiedByteCount);
    }

    [TestMethod]
    public void SignedButUnencryptedHeaderImportsWithoutSignatureVerification()
    {
        using var plain = BuildVbf(0, (0x400, [0x5A]));
        var bytes = plain.ToArray();
        var marker = Encoding.ASCII.GetBytes(" file_checksum");
        var index = bytes.AsSpan().IndexOf(marker);
        Assert.IsGreaterThanOrEqualTo(0, index);
        var signature = Encoding.ASCII.GetBytes(" sw_signature = {\n \"NOT_A_VERIFIED_SIGNATURE\"\n };\n");
        using var signed = new MemoryStream();
        signed.Write(bytes.AsSpan(0, index));
        signed.Write(signature);
        signed.Write(bytes.AsSpan(index));
        signed.Position = 0;
        var imported = new MemoryImageFileService().PrepareImport(signed).Apply(OverlapDecision.OverwriteIncoming)!;
        Assert.IsTrue(imported.Image.TryGetByte(0x400, out var value) && value == 0x5A);
    }

    [TestMethod]
    public void VbfSourceCannotBeSavedBackAsVbf()
    {
        var document = new EditorDocumentWorkspace();
        document.OpenImage(MemoryImage.FromSegments([new MemorySegment(0, [0x01])]), "source.vbf", MemoryImageExportFormat.Vbf);
        Assert.Throws<InvalidOperationException>(() => document.Document.Save());
        using var output = new MemoryStream();
        Assert.Throws<ArgumentOutOfRangeException>(() => new MemoryImageFileService().Export(
            document.Document.CurrentImage, MemoryImageExportFormat.Vbf, 16, output));
    }

    private static MemoryStream BuildVbf(byte dataFormat, params (uint Address, byte[] Data)[] blocks) =>
        BuildVbf(dataFormat, null, null, blocks);

    private static MemoryStream BuildVbf(byte dataFormat, uint? method, byte[]? compressedOverride,
        params (uint Address, byte[] Data)[] blocks)
    {
        using var binary = new MemoryStream();
        Span<byte> header = stackalloc byte[8];
        Span<byte> crc16 = stackalloc byte[2];
        foreach (var (address, data) in blocks)
        {
            var stored = compressedOverride ?? (dataFormat == 0x10 ? EncodeLiterals(data) : data);
            BinaryPrimitives.WriteUInt32BigEndian(header[..4], address);
            BinaryPrimitives.WriteUInt32BigEndian(header[4..], (uint)stored.Length);
            binary.Write(header);
            binary.Write(stored);
            BinaryPrimitives.WriteUInt16BigEndian(crc16, CalculateCrc16(data));
            binary.Write(crc16);
        }
        var body = binary.ToArray();
        var checksum = CalculateCrc32(body);
        var text = $"vbf_version = 2.4;\nheader {{\n data_format_identifier = 0x{dataFormat:X2};\n" +
                   (method is null ? string.Empty : $" data_processing_method = {method};\n") +
                   $" erase = {{ 0x1000, 0x1000 }};\n file_checksum = 0x{checksum:X8};\n}}";
        var output = new MemoryStream();
        output.Write(Encoding.ASCII.GetBytes(text));
        output.Write(body);
        output.Position = 0;
        return output;
    }

    private static byte[] EncodeLiterals(byte[] data)
    {
        var bits = new List<int>();
        foreach (var value in data)
        {
            bits.Add(1);
            for (var bit = 7; bit >= 0; bit--) bits.Add((value >> bit) & 1);
        }
        var output = new byte[(bits.Count + 7) / 8];
        for (var bit = 0; bit < bits.Count; bit++)
            output[bit / 8] |= (byte)(bits[bit] << (7 - bit % 8));
        return output;
    }

    private static byte[] PackBits(string bits)
    {
        var output = new byte[(bits.Length + 7) / 8];
        for (var i = 0; i < bits.Length; i++) output[i / 8] |= (byte)((bits[i] - '0') << (7 - i % 8));
        return output;
    }

    private static ushort CalculateCrc16(byte[] data)
    {
        ushort crc = 0xFFFF;
        foreach (var value in data)
        {
            crc ^= (ushort)(value << 8);
            for (var bit = 0; bit < 8; bit++) crc = (ushort)((crc & 0x8000) != 0 ? (crc << 1) ^ 0x1021 : crc << 1);
        }
        return crc;
    }

    private static uint CalculateCrc32(byte[] data)
    {
        var crc = 0xFFFFFFFFU;
        foreach (var value in data)
        {
            crc ^= value;
            for (var bit = 0; bit < 8; bit++) crc = (crc >> 1) ^ ((crc & 1) != 0 ? 0xEDB88320U : 0U);
        }
        return ~crc;
    }
}
