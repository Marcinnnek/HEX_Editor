using System.Globalization;
using System.Text;
using System.Windows;
using HexEditor.Core;

namespace HexEditor.UI;

internal static class SelectionClipboardText
{
    public static bool TryCopy(MemoryImage image, uint? selectionStart, uint? selectionEnd)
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
                if (text.Length > 0) text.Append(' ');
                range.TryGetByte((uint)address, out var value);
                text.Append(value.ToString("X2", CultureInfo.InvariantCulture));
            }
        }

        if (text.Length == 0) return false;
        Clipboard.SetText(text.ToString(), TextDataFormat.UnicodeText);
        return true;
    }
}
