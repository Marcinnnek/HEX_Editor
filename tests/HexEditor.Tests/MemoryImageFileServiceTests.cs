using System.Text;
using HexEditor.Core;
using HexEditor.Formats;

namespace HexEditor.Tests;

[TestClass]
public sealed class MemoryImageFileServiceTests
{
    [TestMethod]
    public void Import_DetectsSRecordFromContentAndPreservesAbsoluteAddress()
    {
        const string sRecord = "S1051234AABB4F\nS9030000FC\n";
        using var stream = new MemoryStream(Encoding.ASCII.GetBytes(sRecord));

        var preview = new MemoryImageFileService().PrepareImport(stream, sourcePath: "misleading.hex");
        var result = preview.Apply(OverlapDecision.OverwriteIncoming)!;

        Assert.AreEqual("Motorola S-Record", result.InputFormat);
        Assert.AreEqual("misleading.hex", result.SourcePath);
        Assert.IsTrue(result.Image.TryGetByte(0x1234, out var first));
        Assert.AreEqual((byte)0xAA, first);
        Assert.IsTrue(result.Image.TryGetByte(0x1235, out var second));
        Assert.AreEqual((byte)0xBB, second);
    }

    [TestMethod]
    public void Import_IdenticalInternalAndExistingOverlapsAreSummarizedAndKeptOnRequest()
    {
        const string sRecord = "S1051234AABB4F\nS1041235BBF9\nS9030000FC\n";
        var existing = MemoryImage.FromSegments([new MemorySegment(0x1235, [0xBB, 0xCC])]);
        using var stream = new MemoryStream(Encoding.ASCII.GetBytes(sRecord));

        var preview = new MemoryImageFileService().PrepareImport(stream, existing, "input.s19");
        var result = preview.Apply(OverlapDecision.KeepExisting)!;

        Assert.AreEqual(2, preview.OverlapSummary.IdenticalByteCount);
        Assert.AreEqual(0, preview.OverlapSummary.DifferentByteCount);
        Assert.IsTrue(result.Image.TryGetByte(0x1234, out var newByte));
        Assert.AreEqual((byte)0xAA, newByte);
        Assert.IsTrue(result.Image.TryGetByte(0x1235, out var keptByte));
        Assert.AreEqual((byte)0xBB, keptByte);
        Assert.IsTrue(result.Image.TryGetByte(0x1236, out var existingByte));
        Assert.AreEqual((byte)0xCC, existingByte);
    }

    [TestMethod]
    public void Import_OverwriteSelectsLaterRecordAndCancelDoesNotApply()
    {
        const string sRecord = "S1041234AA0B\nS1041234BBFA\nS9030000FC\n";
        using var stream = new MemoryStream(Encoding.ASCII.GetBytes(sRecord));

        var preview = new MemoryImageFileService().PrepareImport(stream);
        var result = preview.Apply(OverlapDecision.OverwriteIncoming)!;

        Assert.AreEqual(1, preview.OverlapSummary.DifferentByteCount);
        var keptResult = preview.Apply(OverlapDecision.KeepExisting)!;
        Assert.IsTrue(keptResult.Image.TryGetByte(0x1234, out var keptValue));
        Assert.AreEqual((byte)0xAA, keptValue);
        Assert.IsTrue(result.Image.TryGetByte(0x1234, out var value));
        Assert.AreEqual((byte)0xBB, value);
        Assert.IsNull(preview.Apply(OverlapDecision.Cancel));
    }

    [TestMethod]
    public void Import_DetectsIntelHexFromContentAndSummarizesIdenticalRecordOverlap()
    {
        const string intelHex = ":02000000AABB99\n:01000100BB43\n:00000001FF\n";
        using var stream = new MemoryStream(Encoding.ASCII.GetBytes(intelHex));

        var preview = new MemoryImageFileService().PrepareImport(stream, sourcePath: "input.s19");
        var result = preview.Apply(OverlapDecision.KeepExisting)!;

        Assert.AreEqual("Intel HEX", result.InputFormat);
        Assert.AreEqual(1, preview.OverlapSummary.IdenticalByteCount);
        Assert.AreEqual(0, preview.OverlapSummary.DifferentByteCount);
        Assert.IsTrue(result.Image.TryGetByte(1, out var value));
        Assert.AreEqual((byte)0xBB, value);
    }

    [TestMethod]
    public void Import_InvalidSRecordChecksumReportsLineAndReason()
    {
        const string sRecord = "S1041234AA00\nS9030000FC\n";
        using var stream = new MemoryStream(Encoding.ASCII.GetBytes(sRecord));

        var exception = Assert.Throws<MemoryImageImportException>(
            () => new MemoryImageFileService().PrepareImport(stream));

        Assert.AreEqual(1, exception.LineNumber);
        StringAssert.Contains(exception.Reason, "checksum");
    }

    [TestMethod]
    public void Import_SRecordAddressWidthsAndCountRecordAreValidated()
    {
        const string sRecord =
            "S1041234AA0B\n" +
            "S205123456BBA3\n" +
            "S306A0000000CC8D\n" +
            "S5030003F9\n" +
            "S70500000000FA\n";
        using var stream = new MemoryStream(Encoding.ASCII.GetBytes(sRecord));

        var result = new MemoryImageFileService().PrepareImport(stream).Apply(OverlapDecision.OverwriteIncoming)!;

        Assert.IsTrue(result.Image.TryGetByte(0x1234, out var s1));
        Assert.AreEqual((byte)0xAA, s1);
        Assert.IsTrue(result.Image.TryGetByte(0x123456, out var s2));
        Assert.AreEqual((byte)0xBB, s2);
        Assert.IsTrue(result.Image.TryGetByte(0xA0000000, out var s3));
        Assert.AreEqual((byte)0xCC, s3);
        Assert.AreEqual(3, ((SRecordMetadata)result.FormatMetadata!).DataRecordCount);
    }

    [TestMethod]
    public void ImportIntelHex_PreservesSparseAbsoluteAddresses()
    {
        const string intelHex = ":02000004A0005A\n:0200000041427B\n:0100020043BA\n:0100100043AC\n:0400000500001234B1\n:00000001FF\n";
        using var stream = new MemoryStream(Encoding.ASCII.GetBytes(intelHex));

        var result = new MemoryImageFileService().PrepareImport(stream, sourcePath: "fixture.hex").Apply(OverlapDecision.OverwriteIncoming)!;

        Assert.AreEqual("Intel HEX", result.InputFormat);
        Assert.AreEqual("fixture.hex", result.SourcePath);
        Assert.AreEqual(0x1234u, ((IntelHexMetadata)result.FormatMetadata!).StartLinearAddress);
        Assert.HasCount(2, result.Image.Segments);
        Assert.AreEqual(0xA0000000u, result.Image.Segments[0].StartAddress);
        CollectionAssert.AreEqual(new byte[] { 0x41, 0x42, 0x43 }, result.Image.Segments[0].Data.ToArray());
        Assert.AreEqual(0xA0000010u, result.Image.Segments[1].StartAddress);
        CollectionAssert.AreEqual(new byte[] { 0x43 }, result.Image.Segments[1].Data.ToArray());
        Assert.IsFalse(result.Image.TryGetByte(0xA0000003u, out _));
        Assert.IsFalse(result.Image.TryGetByte(0xA000000Fu, out _));
    }

    [TestMethod]
    public void PrepareImport_UnknownFormatReportsLineAndReason()
    {
        const string intelHex = "not an Intel HEX record\n";
        using var stream = new MemoryStream(Encoding.ASCII.GetBytes(intelHex));

        var exception = Assert.Throws<MemoryImageImportException>(
            () => new MemoryImageFileService().PrepareImport(stream));

        Assert.AreEqual(1, exception.LineNumber);
        StringAssert.Contains(exception.Reason, "Unable to detect format");
    }

    [TestMethod]
    public void ImportIntelHex_IncorrectByteCountReportsLineAndReason()
    {
        const string intelHex = ":0200000041BE\n";
        using var stream = new MemoryStream(Encoding.ASCII.GetBytes(intelHex));

        var exception = Assert.Throws<MemoryImageImportException>(
            () => new MemoryImageFileService().PrepareImport(stream));

        Assert.AreEqual(1, exception.LineNumber);
        StringAssert.Contains(exception.Reason, "length is incorrect");
    }

    [TestMethod]
    public void ImportIntelHex_InvalidChecksumReportsLineAndReason()
    {
        const string intelHex = ":02000004A0005A\n:0200000041427A\n:00000001FF\n";
        using var stream = new MemoryStream(Encoding.ASCII.GetBytes(intelHex));

        var exception = Assert.Throws<MemoryImageImportException>(
            () => new MemoryImageFileService().PrepareImport(stream));

        Assert.AreEqual(2, exception.LineNumber);
        StringAssert.Contains(exception.Reason, "checksum");
    }

    [TestMethod]
    public void ImportIntelHex_DataPast32BitRangeReportsLine()
    {
        const string intelHex = ":02000004FFFFFC\n:02FFFF00AABB9B\n:00000001FF\n";
        using var stream = new MemoryStream(Encoding.ASCII.GetBytes(intelHex));

        var exception = Assert.Throws<MemoryImageImportException>(
            () => new MemoryImageFileService().PrepareImport(stream));

        Assert.AreEqual(2, exception.LineNumber);
        StringAssert.Contains(exception.Reason, "32-bit address range");
    }

    [TestMethod]
    public void Export_RoundTripsSparseImagesForBothFormatsAndEveryPayloadSize()
    {
        var source = MemoryImage.FromSegments(
        [
            new MemorySegment(0x0000FFFC, Enumerable.Range(0, 70).Select(value => (byte)value).ToArray()),
            new MemorySegment(0xA0000000, [0x11, 0x22, 0x33]),
            new MemorySegment(0xFFFFFFFF, [0x5A])
        ]);
        var service = new MemoryImageFileService();

        foreach (var format in new[] { MemoryImageExportFormat.IntelHex, MemoryImageExportFormat.SRecord })
        {
            foreach (var payloadSize in new[] { 8, 16, 32 })
            {
                using var exported = new MemoryStream();
                service.Export(source, format, payloadSize, exported);
                var serialized = Encoding.ASCII.GetString(exported.ToArray());
                var dataPayloads = serialized.Split('\n', StringSplitOptions.RemoveEmptyEntries)
                    .Select(line => GetDataRecordPayloadLength(line, format))
                    .Where(length => length is not null)
                    .Select(length => length!.Value)
                    .ToArray();
                Assert.IsTrue(dataPayloads.All(length => length <= payloadSize), $"{format}, {payloadSize} bytes");
                Assert.IsTrue(dataPayloads.Contains(payloadSize), $"{format}, {payloadSize} bytes");
                exported.Position = 0;

                var imported = service.PrepareImport(exported).Apply(OverlapDecision.OverwriteIncoming)!.Image;
                Assert.HasCount(source.Segments.Count, imported.Segments);
                for (var index = 0; index < source.Segments.Count; index++)
                {
                    Assert.AreEqual(source.Segments[index].StartAddress, imported.Segments[index].StartAddress);
                    CollectionAssert.AreEqual(
                        source.Segments[index].Data.ToArray(),
                        imported.Segments[index].Data.ToArray(),
                        $"{format}, {payloadSize} bytes");
                }

                Assert.IsFalse(imported.TryGetByte(0x00010042, out _));
                Assert.IsFalse(imported.TryGetByte(0xA0000003, out _));
            }
        }
    }

    private static int? GetDataRecordPayloadLength(string line, MemoryImageExportFormat format)
    {
        if (format == MemoryImageExportFormat.IntelHex)
        {
            return line[7..9] == "00" ? Convert.ToByte(line[1..3], 16) : null;
        }

        var addressBytes = line[1] switch { '1' => 2, '2' => 3, '3' => 4, _ => 0 };
        return addressBytes == 0 ? null : Convert.ToByte(line[2..4], 16) - addressBytes - 1;
    }
}
