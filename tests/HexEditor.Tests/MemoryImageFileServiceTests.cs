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
    public void Import_LimitsUniqueOccupiedBytesAndCountsRepeatedRecordsOnce()
    {
        using var atLimit = new MemoryStream(Encoding.ASCII.GetBytes(
            ":020001000102FA\n:020001000102FA\n:00000001FF\n"));
        var service = new MemoryImageFileService(maximumOccupiedByteCount: 2);

        var imported = service.PrepareImport(atLimit).Apply(OverlapDecision.OverwriteIncoming)!;
        Assert.AreEqual(2UL, imported.Image.OccupiedByteCount);

        using var overLimit = new MemoryStream(Encoding.ASCII.GetBytes(
            ":020001000102FA\n:0100030004F8\n:00000001FF\n"));
        var error = Assert.Throws<MemoryImageImportException>(() => service.PrepareImport(overLimit));
        StringAssert.Contains(error.Message, "maximum of 2 occupied bytes");

        using var sRecordAtLimit = new MemoryStream(Encoding.ASCII.GetBytes(
            "S1051234AABB4F\nS1051234AABB4F\nS9030000FC\n"));
        Assert.AreEqual(2UL, service.PrepareImport(sRecordAtLimit).Apply(OverlapDecision.OverwriteIncoming)!.Image.OccupiedByteCount);

        using var sRecordOverLimit = new MemoryStream(Encoding.ASCII.GetBytes(
            "S1051234AABB4F\nS104123604AF\nS9030000FC\n"));
        var sRecordError = Assert.Throws<MemoryImageImportException>(() => service.PrepareImport(sRecordOverLimit));
        StringAssert.Contains(sRecordError.Message, "maximum of 2 occupied bytes");
    }

    [TestMethod]
    public void Import_AllowsSparseAddressesAcrossFullDomainWithinOccupiedByteLimit()
    {
        var expected = MemoryImage.FromSegments([
            new MemorySegment(0, [0x11]),
            new MemorySegment(uint.MaxValue, [0x22])]);
        var service = new MemoryImageFileService(maximumOccupiedByteCount: 2);

        foreach (var format in new[] { MemoryImageExportFormat.IntelHex, MemoryImageExportFormat.SRecord })
        {
            using var source = new MemoryStream();
            service.Export(expected, format, 8, source);
            source.Position = 0;

            var imported = service.PrepareImport(source).Apply(OverlapDecision.OverwriteIncoming)!;
            Assert.AreEqual(2UL, imported.Image.OccupiedByteCount);
            Assert.IsTrue(imported.Image.TryGetByte(0, out var first));
            Assert.AreEqual((byte)0x11, first);
            Assert.IsTrue(imported.Image.TryGetByte(uint.MaxValue, out var last));
            Assert.AreEqual((byte)0x22, last);
        }
    }

    [TestMethod]
    public void Import_ReportsProgressAndCanBeCancelledWithoutProducingAPreview()
    {
        using var source = new MemoryStream();
        var image = MemoryImage.FromSegments([new MemorySegment(0x1000, new byte[4096])]);
        new MemoryImageFileService().Export(image, MemoryImageExportFormat.IntelHex, 8, source);
        source.Position = 0;
        using var cancellation = new CancellationTokenSource();
        var progress = new CancellingProgress(cancellation, "Parsing records");

        Assert.Throws<OperationCanceledException>(() =>
            new MemoryImageFileService().PrepareImport(source, cancellationToken: cancellation.Token, progress: progress));
        Assert.IsGreaterThan(0, progress.Reports);
    }

    [TestMethod]
    public void Import_OverlaysSmallInputOnLogicalFullRangeImage()
    {
        var source = new MemoryStream(Encoding.ASCII.GetBytes(":020001000102FA\n:00000001FF\n"));
        var existing = MemoryImage.FromRepeatedRange(0, uint.MaxValue, 0xEE);

        var imported = new MemoryImageFileService().PrepareImport(source, existing).Apply(OverlapDecision.OverwriteIncoming)!;

        Assert.AreEqual(4_294_967_296UL, imported.Image.OccupiedByteCount);
        Assert.IsTrue(imported.Image.TryGetByte(0, out var before));
        Assert.IsTrue(imported.Image.TryGetByte(1, out var firstImported));
        Assert.IsTrue(imported.Image.TryGetByte(2, out var secondImported));
        Assert.IsTrue(imported.Image.TryGetByte(3, out var after));
        Assert.AreEqual((byte)0xEE, before);
        Assert.AreEqual((byte)0x01, firstImported);
        Assert.AreEqual((byte)0x02, secondImported);
        Assert.AreEqual((byte)0xEE, after);
    }

    [TestMethod]
    public void Import_NonSeekableInputUsesBoundedDiskStagingAndReportsReadProgress()
    {
        const string intelHex = ":02000000AABB99\n:00000001FF\n";
        using var bytes = new MemoryStream(Encoding.ASCII.GetBytes(intelHex));
        using var input = new NonSeekableReadStream(bytes);
        var progress = new RecordingProgress();

        var result = new MemoryImageFileService().PrepareImport(input, progress: progress)
            .Apply(OverlapDecision.OverwriteIncoming)!;

        Assert.IsTrue(result.Image.TryGetByte(0, out var first));
        Assert.AreEqual((byte)0xAA, first);
        Assert.IsGreaterThan(0, progress.ReadReports);
    }

    private sealed class RecordingProgress : IProgress<MemoryImageFileProgress>
    {
        public int ReadReports { get; private set; }
        public void Report(MemoryImageFileProgress value)
        {
            if (value.Stage == "Reading file") ReadReports++;
        }
    }

    private sealed class NonSeekableReadStream(Stream inner) : Stream
    {
        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
        public override void Flush() => throw new NotSupportedException();
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        public override int Read(byte[] buffer, int offset, int count) => inner.Read(buffer, offset, count);
        public override int Read(Span<byte> buffer) => inner.Read(buffer);
        protected override void Dispose(bool disposing) { }
    }

    [TestMethod]
    public void SaveAtomically_CancellationPreservesExistingDestinationAndRemovesTemporaryFile()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"hex-editor-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, "firmware.hex");
        File.WriteAllText(path, "keep existing data");
        using var cancellation = new CancellationTokenSource();
        var progress = new CancellingProgress(cancellation, "Writing records");
        var image = MemoryImage.FromSegments([new MemorySegment(0x1000, new byte[4096])]);
        try
        {
            Assert.Throws<OperationCanceledException>(() =>
                new MemoryImageFileService().SaveAtomically(image, path, MemoryImageExportFormat.IntelHex, 8,
                    overwriteExisting: true, cancellationToken: cancellation.Token, progress: progress));

            Assert.AreEqual("keep existing data", File.ReadAllText(path));
            CollectionAssert.AreEqual(new[] { "firmware.hex" }, Directory.GetFiles(directory).Select(Path.GetFileName).ToArray());
            Assert.IsGreaterThan(0, progress.Reports);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [TestMethod]
    public void ExportAtomically_CancellationDoesNotLeaveAPartialDestination()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"hex-editor-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, "export.hex");
        using var cancellation = new CancellationTokenSource();
        var progress = new CancellingProgress(cancellation, "Writing records");
        var image = MemoryImage.FromSegments([new MemorySegment(0x1000, new byte[4096])]);
        try
        {
            Assert.Throws<OperationCanceledException>(() =>
                new MemoryImageFileService().ExportAtomically(path, overwriteExisting: false,
                    (output, token, operationProgress) => new MemoryImageFileService().Export(image,
                        MemoryImageExportFormat.SRecord, 8, output, cancellationToken: token, progress: operationProgress),
                    cancellation.Token, progress));

            Assert.IsFalse(File.Exists(path));
            Assert.IsEmpty(Directory.GetFiles(directory));
            Assert.IsGreaterThan(0, progress.Reports);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    private sealed class CancellingProgress(CancellationTokenSource cancellation, string stage) : IProgress<MemoryImageFileProgress>
    {
        public int Reports { get; private set; }

        public void Report(MemoryImageFileProgress value)
        {
            if (value.Stage != stage) return;
            Reports++;
            cancellation.Cancel();
        }
    }

    [TestMethod]
    public void SRecordHeader_IsPreservedInMetadataAndRoundTripsWithMemoryBytes()
    {
        const string sRecord = "S0061234484558CE\nS1051234AABB4F\nS5030001FB\nS9030000FC\n";
        var service = new MemoryImageFileService();
        using var source = new MemoryStream(Encoding.ASCII.GetBytes(sRecord));

        var imported = service.PrepareImport(source).Apply(OverlapDecision.OverwriteIncoming)!;
        var formatMetadata = imported.FormatMetadata!;
        var metadata = formatMetadata.SRecord!;
        Assert.AreEqual((ushort)0x1234, metadata.HeaderAddress);
        CollectionAssert.AreEqual(new byte[] { 0x48, 0x45, 0x58 }, metadata.HeaderData!.ToArray());
        Assert.AreEqual(1, metadata.DataRecordCount);
        Assert.IsTrue(imported.Image.TryGetByte(0x1234, out var first));
        Assert.AreEqual((byte)0xAA, first);
        Assert.IsTrue(imported.Image.TryGetByte(0x1235, out var second));
        Assert.AreEqual((byte)0xBB, second);

        using var exported = new MemoryStream();
        service.Export(imported.Image, MemoryImageExportFormat.SRecord, 8, exported, formatMetadata);
        var serialized = Encoding.ASCII.GetString(exported.ToArray());
        var records = serialized.Split(["\r\n", "\n"], StringSplitOptions.RemoveEmptyEntries);
        Assert.AreEqual("S0061234484558CE", records[0]);
        Assert.AreEqual("S5030001FB", records[^2]);
        exported.Position = 0;

        var path = Path.Combine(Path.GetTempPath(), $"hex-editor-{Guid.NewGuid():N}.s19");
        try
        {
            service.SaveAtomically(imported.Image, path, MemoryImageExportFormat.SRecord, 8, overwriteExisting: false, formatMetadata: formatMetadata);
            var savedRecords = File.ReadAllLines(path);
            Assert.AreEqual("S0061234484558CE", savedRecords[0]);
        }
        finally
        {
            File.Delete(path);
        }

        var roundTripped = service.PrepareImport(exported).Apply(OverlapDecision.OverwriteIncoming)!;
        var roundTripMetadata = roundTripped.FormatMetadata!.SRecord!;
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
        Assert.AreEqual(2, result.FormatMetadata!.SRecord!.DataRecordCount);
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
        Assert.AreEqual(0x1234u, result.FormatMetadata!.IntelHex!.StartLinearAddress);
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
        Assert.AreEqual(0x1234u, intelAgain.FormatMetadata!.IntelHex!.StartLinearAddress);
        AssertImagesEqual(intel.Image, intelAgain.Image);

        using var srecExport = new MemoryStream();
        service.Export(intel.Image, MemoryImageExportFormat.SRecord, 16, srecExport, intel.FormatMetadata);
        srecExport.Position = 0;
        var srecAgain = service.PrepareImport(srecExport).Apply(OverlapDecision.KeepExisting)!;
        Assert.AreEqual(0x1234u, srecAgain.FormatMetadata!.SRecord!.StartAddress);
        AssertImagesEqual(intel.Image, srecAgain.Image);

        using var backToIntel = new MemoryStream();
        service.Export(srecAgain.Image, MemoryImageExportFormat.IntelHex, 16, backToIntel, srecAgain.FormatMetadata);
        backToIntel.Position = 0;
        var switchedAgain = service.PrepareImport(backToIntel).Apply(OverlapDecision.KeepExisting)!;
        Assert.AreEqual(0x1234u, switchedAgain.FormatMetadata!.IntelHex!.StartLinearAddress);
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
    public void ExportSelectedBlock_IncludesAllAdjacentBackingRanges()
    {
        var first = MemoryImage.FromRepeatedRange(0x1000, 0x1000 + 600_000 - 1, 0xA1).Ranges[0];
        var second = MemoryImage.FromRepeatedRange(0x1000 + 600_000, 0x1000 + 1_200_000 - 1, 0xB2).Ranges[0];
        var source = MemoryImage.FromRanges([first, second]);
        var service = new MemoryImageFileService();

        using var exported = new MemoryStream();
        service.ExportSegments(source, [0], MemoryImageExportFormat.IntelHex, 16, exported);
        exported.Position = 0;
        var imported = service.PrepareImport(exported).Apply(OverlapDecision.OverwriteIncoming)!.Image;

        Assert.AreEqual(1_200_000UL, imported.OccupiedByteCount);
        Assert.IsTrue(imported.TryGetByte(0x1000, out var firstByte));
        Assert.AreEqual((byte)0xA1, firstByte);
        Assert.IsTrue(imported.TryGetByte(0x1000 + 600_000, out var secondByte));
        Assert.AreEqual((byte)0xB2, secondByte);
        Assert.IsTrue(imported.TryGetByte(0x1000 + 1_200_000 - 1, out var lastByte));
        Assert.AreEqual((byte)0xB2, lastByte);
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
    public void ExportAddressRange_StreamsClippedLogicalRangesForBothFormats()
    {
        var source = MemoryImage.FromPatternRange(0x1000, 0x1007, [0xAA, 0xBB, 0xCC]);
        var service = new MemoryImageFileService();

        foreach (var format in new[] { MemoryImageExportFormat.IntelHex, MemoryImageExportFormat.SRecord })
        {
            using var exported = new MemoryStream();
            service.ExportAddressRange(source, 0x1002, 0x1006, format, 8, exported);
            exported.Position = 0;
            var imported = service.PrepareImport(exported).Apply(OverlapDecision.OverwriteIncoming)!.Image;

            Assert.AreEqual((ulong)5, imported.OccupiedByteCount);
            Assert.IsTrue(imported.TryGetByte(0x1002, out var first));
            Assert.AreEqual((byte)0xCC, first);
            Assert.IsTrue(imported.TryGetByte(0x1003, out var second));
            Assert.AreEqual((byte)0xAA, second);
            Assert.IsTrue(imported.TryGetByte(0x1006, out var last));
            Assert.AreEqual((byte)0xAA, last);
            Assert.IsFalse(imported.TryGetByte(0x1001, out _));
        }
    }

    [TestMethod]
    public void Export_FullAddressSpaceStreamsWithWideProgressAndCancellation()
    {
        var image = MemoryImage.FromPatternRange(0, uint.MaxValue, [0xA5, 0x5A, 0xC3]);
        var service = new MemoryImageFileService();
        foreach (var format in new[] { MemoryImageExportFormat.IntelHex, MemoryImageExportFormat.SRecord })
        {
            using var cancellation = new CancellationTokenSource();
            using var output = new MemoryStream();
            var progress = new CapturingCancellingProgress(cancellation);

            Assert.Throws<OperationCanceledException>(() => service.Export(image, format, 32, output,
                cancellationToken: cancellation.Token, progress: progress));

            Assert.AreEqual(32L, progress.Last!.Completed);
            Assert.AreEqual(4_294_967_296L, progress.Last.Total);
            Assert.IsGreaterThan(0, output.Length);
        }
    }

    [TestMethod]
    public void SaveAtomically_FullAddressSpaceCancellationPreservesExistingOutputForBothFormats()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"hex-editor-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        var image = MemoryImage.FromPatternRange(0, uint.MaxValue, [0x11, 0x22, 0x33]);
        var service = new MemoryImageFileService();
        try
        {
            foreach (var format in new[] { MemoryImageExportFormat.IntelHex, MemoryImageExportFormat.SRecord })
            {
                var extension = format == MemoryImageExportFormat.IntelHex ? ".hex" : ".srec";
                var path = Path.Combine(directory, $"firmware{extension}");
                File.WriteAllText(path, "preserve this output");
                using var cancellation = new CancellationTokenSource();
                var progress = new CapturingCancellingProgress(cancellation);

                Assert.Throws<OperationCanceledException>(() => service.SaveAtomically(image, path, format, 32,
                    overwriteExisting: true, cancellationToken: cancellation.Token, progress: progress));

                Assert.AreEqual("preserve this output", File.ReadAllText(path));
                Assert.AreEqual(32L, progress.Last!.Completed);
                Assert.AreEqual(4_294_967_296L, progress.Last.Total);
                Assert.HasCount(1, Directory.GetFiles(directory).Where(file => Path.GetFileName(file) == Path.GetFileName(path)));
            }
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    private sealed class CapturingCancellingProgress(CancellationTokenSource cancellation) : IProgress<MemoryImageFileProgress>
    {
        public MemoryImageFileProgress? Last { get; private set; }

        public void Report(MemoryImageFileProgress value)
        {
            Last = value;
            cancellation.Cancel();
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
