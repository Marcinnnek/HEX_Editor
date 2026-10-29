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
        Assert.AreEqual(MemoryImageExportFormat.SRecord, result.DetectedFormat);
        Assert.AreEqual("misleading.hex", result.SourcePath);
        Assert.IsTrue(result.Image.TryGetByte(0x1234, out var first));
        Assert.AreEqual((byte)0xAA, first);
        Assert.IsTrue(result.Image.TryGetByte(0x1235, out var second));
        Assert.AreEqual((byte)0xBB, second);
    }

    [TestMethod]
    public void SRecordHeader_IsPreservedInMetadataAndRoundTripsWithMemoryBytes()
    {
        const string sRecord = "S0061234484558CE\nS1051234AABB4F\nS5030001FB\nS9030000FC\n";
        var service = new MemoryImageFileService();
        using var source = new MemoryStream(Encoding.ASCII.GetBytes(sRecord));

        var imported = service.PrepareImport(source).Apply(OverlapDecision.OverwriteIncoming)!;
        var metadata = (SRecordMetadata)imported.FormatMetadata!;
        Assert.AreEqual((ushort)0x1234, metadata.HeaderAddress);
        CollectionAssert.AreEqual(new byte[] { 0x48, 0x45, 0x58 }, metadata.HeaderData!.ToArray());
        Assert.AreEqual(1, metadata.DataRecordCount);
        Assert.IsTrue(imported.Image.TryGetByte(0x1234, out var first));
        Assert.AreEqual((byte)0xAA, first);
        Assert.IsTrue(imported.Image.TryGetByte(0x1235, out var second));
        Assert.AreEqual((byte)0xBB, second);

        using var exported = new MemoryStream();
        service.Export(imported.Image, MemoryImageExportFormat.SRecord, 8, exported, metadata);
        var serialized = Encoding.ASCII.GetString(exported.ToArray());
        var records = serialized.Split(["\r\n", "\n"], StringSplitOptions.RemoveEmptyEntries);
        Assert.AreEqual("S0061234484558CE", records[0]);
        Assert.AreEqual("S5030001FB", records[^2]);
        exported.Position = 0;

        var path = Path.Combine(Path.GetTempPath(), $"hex-editor-{Guid.NewGuid():N}.s19");
        try
        {
            service.SaveAtomically(imported.Image, path, MemoryImageExportFormat.SRecord, 8, overwriteExisting: false, formatMetadata: metadata);
            var savedRecords = File.ReadAllLines(path);
            Assert.AreEqual("S0061234484558CE", savedRecords[0]);
        }
        finally
        {
            File.Delete(path);
        }

        var roundTripped = service.PrepareImport(exported).Apply(OverlapDecision.OverwriteIncoming)!;
        var roundTripMetadata = (SRecordMetadata)roundTripped.FormatMetadata!;
        Assert.AreEqual((ushort)0x1234, roundTripMetadata.HeaderAddress);
        CollectionAssert.AreEqual(new byte[] { 0x48, 0x45, 0x58 }, roundTripMetadata.HeaderData!.ToArray());
        Assert.AreEqual(1, roundTripMetadata.DataRecordCount);
        Assert.IsTrue(roundTripped.Image.TryGetByte(0x1234, out var roundTripFirst));
        Assert.AreEqual((byte)0xAA, roundTripFirst);
        Assert.IsTrue(roundTripped.Image.TryGetByte(0x1235, out var roundTripSecond));
        Assert.AreEqual((byte)0xBB, roundTripSecond);
    }

    [TestMethod]
    public void SRecordWithoutHeader_DoesNotGainOneWhenExported()
    {
        const string sRecord = "S1051234AABB4F\nS9030000FC\n";
        var service = new MemoryImageFileService();
        using var source = new MemoryStream(Encoding.ASCII.GetBytes(sRecord));
        var imported = service.PrepareImport(source).Apply(OverlapDecision.OverwriteIncoming)!;

        using var exported = new MemoryStream();
        service.Export(imported.Image, MemoryImageExportFormat.SRecord, 8, exported, imported.FormatMetadata);

        var serialized = Encoding.ASCII.GetString(exported.ToArray());
        Assert.IsFalse(serialized.Contains("S0", StringComparison.Ordinal));
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
        Assert.AreEqual(MemoryImageExportFormat.IntelHex, result.DetectedFormat);
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
            "S1041235BBF9\n" +
            "S5030002FA\n" +
            "S9030000FC\n";
        using var stream = new MemoryStream(Encoding.ASCII.GetBytes(sRecord));

        var result = new MemoryImageFileService().PrepareImport(stream).Apply(OverlapDecision.OverwriteIncoming)!;

        Assert.IsTrue(result.Image.TryGetByte(0x1234, out var s1));
        Assert.AreEqual((byte)0xAA, s1);
        Assert.IsTrue(result.Image.TryGetByte(0x1235, out var second));
        Assert.AreEqual((byte)0xBB, second);
        Assert.AreEqual(2, ((SRecordMetadata)result.FormatMetadata!).DataRecordCount);
    }

    [TestMethod]
    [DataRow("S1041234AA0B", '1', 0x1234u)]
    [DataRow("S205123456AAB4", '2', 0x123456u)]
    [DataRow("S30612345678AA3B", '3', 0x12345678u)]
    public void Import_SRecordAcceptsHomogeneousDataAndMatchingTermination(string dataRecord, char type, uint address)
    {
        var termination = type switch
        {
            '1' => "S9030000FC",
            '2' => "S804000000FB",
            '3' => "S70500000000FA",
            _ => throw new AssertFailedException("Unexpected S-record test type.")
        };
        using var stream = new MemoryStream(Encoding.ASCII.GetBytes($"{dataRecord}\n{termination}\n"));

        var result = new MemoryImageFileService().PrepareImport(stream).Apply(OverlapDecision.OverwriteIncoming)!;

        Assert.IsTrue(result.Image.TryGetByte(address, out var value));
        Assert.AreEqual((byte)0xAA, value);
    }

    [TestMethod]
    [DataRow("S1041234AA0B", "S205123456AAB4", "S804000000FB")]
    [DataRow("S1041234AA0B", "S30612345678AA3B", "S70500000000FA")]
    [DataRow("S205123456AAB4", "S1041234AA0B", "S9030000FC")]
    [DataRow("S205123456AAB4", "S30612345678AA3B", "S70500000000FA")]
    [DataRow("S30612345678AA3B", "S1041234AA0B", "S9030000FC")]
    [DataRow("S30612345678AA3B", "S205123456AAB4", "S804000000FB")]
    public void Import_SRecordRejectsMixedDataAddressWidthsAtOffendingLine(string firstRecord, string secondRecord, string termination)
    {
        using var stream = new MemoryStream(Encoding.ASCII.GetBytes($"{firstRecord}\n{secondRecord}\n{termination}\n"));

        var exception = Assert.Throws<MemoryImageImportException>(
            () => new MemoryImageFileService().PrepareImport(stream));

        Assert.AreEqual(2, exception.LineNumber);
        StringAssert.Contains(exception.Reason, "address width");
    }

    [TestMethod]
    [DataRow("S1041234AA0B", "S804000000FB", "address width")]
    [DataRow("S205123456AAB4", "S9030000FC", "address width")]
    [DataRow("S30612345678AA3B", "S804000000FB", "address width")]
    public void Import_SRecordRejectsTerminationWithMismatchedAddressWidth(string dataRecord, string termination, string reason)
    {
        using var stream = new MemoryStream(Encoding.ASCII.GetBytes($"{dataRecord}\n{termination}\n"));

        var exception = Assert.Throws<MemoryImageImportException>(
            () => new MemoryImageFileService().PrepareImport(stream));

        Assert.AreEqual(2, exception.LineNumber);
        StringAssert.Contains(exception.Reason, reason);
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

    [TestMethod]
    public void Export_PreservesExecutionStartAddressForBothFormatsAndFormatSwitches()
    {
        var service = new MemoryImageFileService();
        var intelSource = new MemoryStream(Encoding.ASCII.GetBytes(":0400000500001234B1\r\n:02001000AABB89\r\n:00000001FF\r\n"));
        var intel = service.PrepareImport(intelSource).Apply(OverlapDecision.KeepExisting)!;
        using var intelExport = new MemoryStream();
        service.Export(intel.Image, MemoryImageExportFormat.IntelHex, 16, intelExport, intel.FormatMetadata);
        intelExport.Position = 0;
        var intelAgain = service.PrepareImport(intelExport).Apply(OverlapDecision.KeepExisting)!;
        Assert.AreEqual(0x1234u, ((IntelHexMetadata)intelAgain.FormatMetadata!).StartLinearAddress);
        AssertImagesEqual(intel.Image, intelAgain.Image);

        using var srecExport = new MemoryStream();
        service.Export(intel.Image, MemoryImageExportFormat.SRecord, 16, srecExport, intel.FormatMetadata);
        srecExport.Position = 0;
        var srecAgain = service.PrepareImport(srecExport).Apply(OverlapDecision.KeepExisting)!;
        Assert.AreEqual(0x1234u, ((SRecordMetadata)srecAgain.FormatMetadata!).StartAddress);
        AssertImagesEqual(intel.Image, srecAgain.Image);

        using var backToIntel = new MemoryStream();
        service.Export(srecAgain.Image, MemoryImageExportFormat.IntelHex, 16, backToIntel, srecAgain.FormatMetadata);
        backToIntel.Position = 0;
        var switchedAgain = service.PrepareImport(backToIntel).Apply(OverlapDecision.KeepExisting)!;
        Assert.AreEqual(0x1234u, ((IntelHexMetadata)switchedAgain.FormatMetadata!).StartLinearAddress);
        AssertImagesEqual(intel.Image, switchedAgain.Image);
    }

    [TestMethod]
    public void ExportSegments_RoundTripsOnlyRequestedOccupiedSegments()
    {
        var source = MemoryImage.FromSegments(
        [
            new MemorySegment(0x1000, [0x10, 0x11]),
            new MemorySegment(0x2000, [0x20]),
            new MemorySegment(0x3000, [0x30, 0x31])
        ]);
        var service = new MemoryImageFileService();

        foreach (var format in new[] { MemoryImageExportFormat.IntelHex, MemoryImageExportFormat.SRecord })
        {
            using var exported = new MemoryStream();
            service.ExportSegments(source, [2, 0], format, 8, exported);
            exported.Position = 0;
            var imported = service.PrepareImport(exported).Apply(OverlapDecision.OverwriteIncoming)!.Image;

            Assert.HasCount(2, imported.Segments);
            Assert.AreEqual(0x1000u, imported.Segments[0].StartAddress);
            CollectionAssert.AreEqual(new byte[] { 0x10, 0x11 }, imported.Segments[0].Data.ToArray());
            Assert.AreEqual(0x3000u, imported.Segments[1].StartAddress);
            CollectionAssert.AreEqual(new byte[] { 0x30, 0x31 }, imported.Segments[1].Data.ToArray());
            Assert.IsFalse(imported.TryGetByte(0x2000, out _));
        }
    }

    [TestMethod]
    public void ExportAddressRange_ClipsSegmentsAndPreservesHolesForBothFormats()
    {
        var source = MemoryImage.FromSegments(
        [
            new MemorySegment(0x1000, [0x10, 0x11, 0x12, 0x13]),
            new MemorySegment(0x1006, [0x16, 0x17]),
            new MemorySegment(0x2000, [0x20])
        ]);
        var service = new MemoryImageFileService();

        foreach (var format in new[] { MemoryImageExportFormat.IntelHex, MemoryImageExportFormat.SRecord })
        {
            using var exported = new MemoryStream();
            service.ExportAddressRange(source, 0x1001, 0x1006, format, 8, exported);
            exported.Position = 0;
            var imported = service.PrepareImport(exported).Apply(OverlapDecision.OverwriteIncoming)!.Image;

            Assert.HasCount(2, imported.Segments);
            Assert.AreEqual(0x1001u, imported.Segments[0].StartAddress);
            CollectionAssert.AreEqual(new byte[] { 0x11, 0x12, 0x13 }, imported.Segments[0].Data.ToArray());
            Assert.AreEqual(0x1006u, imported.Segments[1].StartAddress);
            CollectionAssert.AreEqual(new byte[] { 0x16 }, imported.Segments[1].Data.ToArray());
            Assert.IsFalse(imported.TryGetByte(0x1000, out _));
            Assert.IsFalse(imported.TryGetByte(0x1004, out _));
            Assert.IsFalse(imported.TryGetByte(0x2000, out _));
        }
    }

    [TestMethod]
    public void ExportSelection_RejectsEmptyAndInvalidBoundsWithActionableErrors()
    {
        var image = MemoryImage.FromSegments([new MemorySegment(0x1000, [0x11])]);
        var service = new MemoryImageFileService();

        var reversed = Assert.Throws<ArgumentException>(
            () => service.ExportAddressRange(image, 0x1001, 0x1000, MemoryImageExportFormat.IntelHex, 8, Stream.Null));
        StringAssert.Contains(reversed.Message, "start address must be less than or equal to the end address");

        var empty = Assert.Throws<InvalidOperationException>(
            () => service.ExportAddressRange(image, 0x2000, 0x2FFF, MemoryImageExportFormat.IntelHex, 8, Stream.Null));
        StringAssert.Contains(empty.Message, "contains no occupied bytes");

        var noSegments = Assert.Throws<InvalidOperationException>(
            () => service.ExportSegments(image, [], MemoryImageExportFormat.IntelHex, 8, Stream.Null));
        StringAssert.Contains(noSegments.Message, "Select at least one occupied segment");
    }

    [TestMethod]
    public void SaveAtomically_RefusesUnconfirmedOverwriteAndKeepsExistingFileOnExportFailure()
    {
        var path = Path.Combine(Path.GetTempPath(), $"hex-editor-{Guid.NewGuid():N}.hex");
        var original = "preserve this file";
        File.WriteAllText(path, original);
        try
        {
            var service = new MemoryImageFileService();
            var image = MemoryImage.FromSegments([new MemorySegment(0x1234, [0xAA])]);
            Assert.Throws<IOException>(() => service.SaveAtomically(image, path, MemoryImageExportFormat.IntelHex, 16, overwriteExisting: false));
            Assert.AreEqual(original, File.ReadAllText(path));

            Assert.Throws<ArgumentOutOfRangeException>(() => service.SaveAtomically(image, path, (MemoryImageExportFormat)99, 16, overwriteExisting: true));
            Assert.AreEqual(original, File.ReadAllText(path));

            service.SaveAtomically(image, path, MemoryImageExportFormat.IntelHex, 16, overwriteExisting: true);
            Assert.AreNotEqual(original, File.ReadAllText(path));
            Assert.IsFalse(Directory.EnumerateFiles(Path.GetDirectoryName(path)!, $".{Path.GetFileName(path)}.*.tmp").Any());
        }
        finally
        {
            File.Delete(path);
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

    private static void AssertImagesEqual(MemoryImage expected, MemoryImage actual)
    {
        Assert.HasCount(expected.Segments.Count, actual.Segments);
        for (var index = 0; index < expected.Segments.Count; index++)
        {
            Assert.AreEqual(expected.Segments[index].StartAddress, actual.Segments[index].StartAddress);
            CollectionAssert.AreEqual(expected.Segments[index].Data.ToArray(), actual.Segments[index].Data.ToArray());
        }
    }
}
