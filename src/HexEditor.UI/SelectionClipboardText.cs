using System.Globalization;
using System.Text;
using System.Windows;
using HexEditor.Core;

namespace HexEditor.UI;

internal static class SelectionClipboardText
{
    public static byte[] ParseHexBytes(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        var chunks = text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
        if (chunks.Length == 0) throw new FormatException("The clipboard contains no hexadecimal bytes.");
        var bytes = new byte[chunks.Length];
        for (var index = 0; index < chunks.Length; index++)
        {
            var token = chunks[index].StartsWith("0x", StringComparison.OrdinalIgnoreCase)
                ? chunks[index][2..] : chunks[index];
            if (token.Length is < 1 or > 2 ||
                !byte.TryParse(token, NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out bytes[index]))
                throw new FormatException($"'{chunks[index]}' is not a hexadecimal byte. Use text such as 00 FF 3A.");
        }
        return bytes;
    }

    public static bool TryCopy(MemoryImage image, uint? selectionStart, uint? selectionEnd,
        SelectionTextFormat format = SelectionTextFormat.Hex)
    {
        ArgumentNullException.ThrowIfNull(image);
        if (selectionStart is not uint start || selectionEnd is not uint end) return false;
        if (start > end) (start, end) = (end, start);

        var text = new StringBuilder();
        foreach (var range in image.Ranges)
        {
            if (range.EndAddress < start || range.StartAddress > end) continue;
            var rangeStart = Math.Max(range.StartAddress, start);
            var rangeEnd = Math.Min(range.EndAddress, end);
            for (ulong address = rangeStart; address <= rangeEnd; address++)
            {
                range.TryGetByte((uint)address, out var value);
                if (format == SelectionTextFormat.Ascii)
                    text.Append(ByteText.FromByte(value).Ascii);
                else
                {
                    if (text.Length > 0) text.Append(' ');
                    text.Append(value.ToString("X2", CultureInfo.InvariantCulture));
                }
            }
        }

        if (text.Length == 0) return false;
        Clipboard.SetText(text.ToString(), TextDataFormat.UnicodeText);
        return true;
    }
}

public enum SelectionTextFormat { Hex, Ascii }
