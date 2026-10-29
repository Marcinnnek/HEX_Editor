using System.Globalization;

namespace HexEditor.UI;

public static class HexAddressParser
{
    public static bool TryParse(string text, out uint address)
    {
        ArgumentNullException.ThrowIfNull(text);
        text = text.Trim();
        if (text.StartsWith("0x", StringComparison.OrdinalIgnoreCase)) text = text[2..];
        return uint.TryParse(text, NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out address);
    }

    public static uint Parse(string text)
    {
        if (!TryParse(text, out var address))
            throw new ArgumentException("Enter a 32-bit hexadecimal address.", nameof(text));
        return address;
    }
}
