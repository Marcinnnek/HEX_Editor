using System.Globalization;

namespace HexEditor.UI;

public static class HexAddressParser
{
    public static bool IsValidPartialInput(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        var digits = text.StartsWith("0x", StringComparison.OrdinalIgnoreCase) ? text[2..] : text;
        return digits.Length <= 8 && digits.All(Uri.IsHexDigit);
    }

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
