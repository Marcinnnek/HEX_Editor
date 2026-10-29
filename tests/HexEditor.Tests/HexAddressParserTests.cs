using HexEditor.UI;

namespace HexEditor.Tests;

[TestClass]
public sealed class HexAddressParserTests
{
    [TestMethod]
    public void ParseAddress_AcceptsWhitespaceOptionalPrefixAndFull32BitRange()
    {
        Assert.IsTrue(HexAddressParser.TryParse("  0x1234  ", out var prefixed));
        Assert.AreEqual(0x1234u, prefixed);
        Assert.IsTrue(HexAddressParser.TryParse("FFFFFFFF", out var maximum));
        Assert.AreEqual(uint.MaxValue, maximum);
    }

    [TestMethod]
    public void ParseAddress_RejectsEmptyNonHexAndOverflow()
    {
        Assert.IsFalse(HexAddressParser.TryParse("", out _));
        Assert.IsFalse(HexAddressParser.TryParse("xyz", out _));
        Assert.IsFalse(HexAddressParser.TryParse("100000000", out _));
    }
}
