using HexEditor.Core;

namespace HexEditor.Tests;

[TestClass]
public sealed class ByteTextTests
{
    [TestMethod]
    public void FromByte_FormatsPrintableAsciiAsHexAndCharacter()
    {
        var text = ByteText.FromByte(0x41);

        Assert.AreEqual("41", text.Hex);
        Assert.AreEqual('A', text.Ascii);
    }

    [TestMethod]
    public void FromByte_FormatsNonPrintableBytesAsHexAndDot()
    {
        var text = ByteText.FromByte(0x7F);

        Assert.AreEqual("7F", text.Hex);
        Assert.AreEqual('.', text.Ascii);
    }

    [TestMethod]
    public void FromByte_FormatsMissingByteAsBlankHexAndAscii()
    {
        var text = ByteText.FromByte(null);

        Assert.AreEqual("", text.Hex);
        Assert.AreEqual(' ', text.Ascii);
        Assert.AreEqual("  ", text.HexColumn);
    }
}
