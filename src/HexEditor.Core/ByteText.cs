namespace HexEditor.Core;

/// <summary>
/// Textual representations of a byte value using the editor's byte-oriented conventions.
/// </summary>
public readonly record struct ByteText(string Hex, char Ascii)
{
    public string HexColumn => Hex.Length == 0 ? "  " : Hex;

    public static ByteText FromByte(byte? value) => value is byte present
        ? new ByteText(present.ToString("X2"), present is >= 0x20 and <= 0x7E ? (char)present : '.')
        : new ByteText(string.Empty, ' ');
}
