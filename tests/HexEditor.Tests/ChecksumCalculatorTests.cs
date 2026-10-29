using HexEditor.Core;
using HexEditor.Tools;

namespace HexEditor.Tests;

[TestClass]
public sealed class ChecksumCalculatorTests
{
    private static readonly byte[] KnownVector = "123456789"u8.ToArray();

    [TestMethod]
    public void Calculate_Crc16CcittFalseMatchesPublishedCheckValue()
    {
        var image = MemoryImage.FromSegments([new MemorySegment(0x1000, KnownVector)]);
        var result = ChecksumCalculator.Calculate(image, ChecksumRequest.Crc16CcittFalse(
            0x1000, 0x1008, MissingAddressTreatment.Reject));

        Assert.AreEqual(16, result.WidthBits);
        Assert.AreEqual(0x29B1UL, result.Value);
        StringAssert.Contains(result.Parameters, "poly=0x1021");
    }

    [TestMethod]
    public void Calculate_Crc32IsoHdlcMatchesPublishedCheckValue()
    {
        var image = MemoryImage.FromSegments([new MemorySegment(0x2000, KnownVector)]);
        var result = ChecksumCalculator.Calculate(image, ChecksumRequest.Crc32IsoHdlc(
            0x2000, 0x2008, MissingAddressTreatment.Reject));

        Assert.AreEqual(32, result.WidthBits);
        Assert.AreEqual(0xCBF43926UL, result.Value);
        StringAssert.Contains(result.Parameters, "refin=true");
    }

    [TestMethod]
    public void Calculate_AdditiveChecksumRequiresExplicitHolePolicyAndCanFillSparseAddresses()
    {
        var image = MemoryImage.FromSegments([
            new MemorySegment(0x10, [0x01]),
            new MemorySegment(0x12, [0x03])]);

        Assert.Throws<InvalidOperationException>(() => ChecksumCalculator.Calculate(
            image, ChecksumRequest.Additive(0x10, 0x12, 8, MissingAddressTreatment.Reject)));

        var result = ChecksumCalculator.Calculate(image, ChecksumRequest.Additive(
            0x10, 0x12, 8, MissingAddressTreatment.FillByte, 0x10));

        Assert.AreEqual(0x14UL, result.Value);
        StringAssert.Contains(result.Parameters, "missing=fill 0x10");
    }

    [TestMethod]
    public void Calculate_CustomReflectedCrcCanUsePublishedCrc32Parameters()
    {
        var image = MemoryImage.FromSegments([new MemorySegment(0, KnownVector)]);
        var request = ChecksumRequest.CustomCrc(
            0, 8, 32, polynomial: 0x04C11DB7, initialValue: 0xFFFFFFFF,
            reflectInput: true, reflectOutput: true, finalXor: 0xFFFFFFFF,
            missingAddressTreatment: MissingAddressTreatment.Reject);

        var result = ChecksumCalculator.Calculate(image, request);

        Assert.AreEqual(0xCBF43926UL, result.Value);
    }

    [TestMethod]
    public void Calculate_WritesResultBytesUsingReportedByteOrder()
    {
        var image = MemoryImage.FromSegments([new MemorySegment(0, [0x12, 0x34])]);
        var request = ChecksumRequest.Additive(
            0, 1, 16, MissingAddressTreatment.Reject, byteOrder: ChecksumByteOrder.LittleEndian);

        var result = ChecksumCalculator.Calculate(image, request);

        CollectionAssert.AreEqual(new byte[] { 0x46, 0x00 }, result.ToBytes());
        Assert.AreEqual(ChecksumByteOrder.LittleEndian, result.ByteOrder);
    }
}
