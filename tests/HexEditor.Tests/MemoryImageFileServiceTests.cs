using System.Text;
using HexEditor.Formats;

namespace HexEditor.Tests;

[TestClass]
public sealed class MemoryImageFileServiceTests
{
    [TestMethod]
    public void ImportIntelHex_PreservesSparseAbsoluteAddresses()
    {
        const string intelHex = ":02000004A0005A\n:0200000041427B\n:0100020043BA\n:0100100043AC\n:0400000500001234B1\n:00000001FF\n";
        using var stream = new MemoryStream(Encoding.ASCII.GetBytes(intelHex));

        var result = new MemoryImageFileService().ImportIntelHex(stream, "fixture.hex");

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
    public void ImportIntelHex_InvalidSyntaxReportsLineAndReason()
    {
        const string intelHex = "not an Intel HEX record\n";
        using var stream = new MemoryStream(Encoding.ASCII.GetBytes(intelHex));

        var exception = Assert.Throws<MemoryImageImportException>(
            () => new MemoryImageFileService().ImportIntelHex(stream));

        Assert.AreEqual(1, exception.LineNumber);
        StringAssert.Contains(exception.Reason, "starting with ':'");
    }

    [TestMethod]
    public void ImportIntelHex_IncorrectByteCountReportsLineAndReason()
    {
        const string intelHex = ":0200000041BE\n";
        using var stream = new MemoryStream(Encoding.ASCII.GetBytes(intelHex));

        var exception = Assert.Throws<MemoryImageImportException>(
            () => new MemoryImageFileService().ImportIntelHex(stream));

        Assert.AreEqual(1, exception.LineNumber);
        StringAssert.Contains(exception.Reason, "length is incorrect");
    }

    [TestMethod]
    public void ImportIntelHex_InvalidChecksumReportsLineAndReason()
    {
        const string intelHex = ":02000004A0005A\n:0200000041427A\n:00000001FF\n";
        using var stream = new MemoryStream(Encoding.ASCII.GetBytes(intelHex));

        var exception = Assert.Throws<MemoryImageImportException>(
            () => new MemoryImageFileService().ImportIntelHex(stream));

        Assert.AreEqual(2, exception.LineNumber);
        StringAssert.Contains(exception.Reason, "checksum");
    }

    [TestMethod]
    public void ImportIntelHex_DataPast32BitRangeReportsLine()
    {
        const string intelHex = ":02000004FFFFFC\n:02FFFF00AABB9B\n:00000001FF\n";
        using var stream = new MemoryStream(Encoding.ASCII.GetBytes(intelHex));

        var exception = Assert.Throws<MemoryImageImportException>(
            () => new MemoryImageFileService().ImportIntelHex(stream));

        Assert.AreEqual(2, exception.LineNumber);
        StringAssert.Contains(exception.Reason, "32-bit address range");
    }
}
