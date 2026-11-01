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

    [TestMethod]
    public void InputAllowsEightHexDigitsOrTenCharactersWithPrefix()
    {
        foreach (var input in new[] { "", "0", "0x", "FFFFFFFF", "0xFFFFFFFF" })
            Assert.IsTrue(HexAddressParser.IsValidPartialInput(input), input);

        foreach (var input in new[] { "100000000", "0x100000000", "0x12G4", "12 34", "0x0x12" })
            Assert.IsFalse(HexAddressParser.IsValidPartialInput(input), input);
    }
}
