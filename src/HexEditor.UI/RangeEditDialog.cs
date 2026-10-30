using System.Globalization;
using System.Windows;
using System.Windows.Controls;

namespace HexEditor.UI;

internal enum RangeEditDialogKind { SelectRange, Insert, Fill }

internal sealed record RangeEditDialogResult(uint Start, uint End, byte[]? Pattern);

internal sealed class RangeEditDialog : Window
{
    private readonly RangeEditDialogKind _kind;
    private readonly TextBox _start = new() { MinWidth = 150 };
    private readonly TextBox _end = new() { MinWidth = 150 };
    private readonly TextBox _pattern = new() { MinWidth = 220 };
    private readonly TextBlock _error = new() { TextWrapping = TextWrapping.Wrap };

    public RangeEditDialog(RangeEditDialogKind kind, uint start, uint end)
    {
        _kind = kind;
        _error.SetResourceReference(TextBlock.ForegroundProperty, "HexErrorBrush");
        Title = kind switch { RangeEditDialogKind.SelectRange => "Select Range", RangeEditDialogKind.Insert => "Edit / Insert", _ => "Fill Range" };
        Width = 390;
        SizeToContent = SizeToContent.Height;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        ResizeMode = ResizeMode.NoResize;
        ShowInTaskbar = false;
        _start.Text = start.ToString("X8", CultureInfo.InvariantCulture);
        _end.Text = end.ToString("X8", CultureInfo.InvariantCulture);

        var panel = new StackPanel { Margin = new Thickness(18) };
        AddField(panel, kind == RangeEditDialogKind.Insert ? "Target address" : "Start address", _start);
        if (kind != RangeEditDialogKind.Insert) AddField(panel, "End address (inclusive)", _end);
        if (kind != RangeEditDialogKind.SelectRange)
            AddField(panel, kind == RangeEditDialogKind.Insert ? "Bytes (hex)" : "Repeating byte pattern (hex)", _pattern);
        panel.Children.Add(_error);
        var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 14, 0, 0) };
        var ok = new Button { Content = "OK", IsDefault = true, MinWidth = 76, Margin = new Thickness(0, 0, 8, 0) };
        ok.Click += (_, _) => Accept();
        var cancel = new Button { Content = "Cancel", IsCancel = true, MinWidth = 76 };
        buttons.Children.Add(ok); buttons.Children.Add(cancel); panel.Children.Add(buttons);
        Content = panel;
        Loaded += (_, _) => { _start.Focus(); _start.SelectAll(); };
    }

    public RangeEditDialogResult? Result { get; private set; }

    private static void AddField(Panel panel, string label, Control input)
    {
        panel.Children.Add(new TextBlock { Text = label, Margin = new Thickness(0, 8, 0, 3) });
        panel.Children.Add(input);
    }

    private void Accept()
    {
        uint start;
        uint end;
        try
        {
            start = HexAddressParser.Parse(_start.Text);
        }
        catch (ArgumentException ex)
        {
            _error.Text = ex.Message;
            _start.Focus();
            _start.SelectAll();
            return;
        }
        try
        {
            end = _kind == RangeEditDialogKind.Insert ? start : HexAddressParser.Parse(_end.Text);
            if (start > end) throw new ArgumentException("The end address must be greater than or equal to the start address.");
            var pattern = _kind == RangeEditDialogKind.SelectRange ? null : ParseBytes(_pattern.Text);
            Result = new RangeEditDialogResult(start, end, pattern);
            DialogResult = true;
        }
        catch (ArgumentException ex)
        {
            _error.Text = ex.Message;
            _end.Focus();
            _end.SelectAll();
        }
        catch (FormatException ex)
        {
            _error.Text = ex.Message;
            _pattern.Focus();
            _pattern.SelectAll();
        }
    }

    private static byte[] ParseBytes(string text)
    {
        var chunks = text.Trim().Replace("0x", "", StringComparison.OrdinalIgnoreCase).Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
        if (chunks.Length == 0) throw new FormatException("Enter one or more hexadecimal bytes, for example: 00 FF.");
        var result = new byte[chunks.Length];
        for (var i = 0; i < chunks.Length; i++)
        {
            if (chunks[i].Length is < 1 or > 2 || !byte.TryParse(chunks[i], NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out result[i]))
                throw new FormatException($"'{chunks[i]}' is not a hexadecimal byte. Enter bytes such as 00 FF.");
        }
        return result;
    }
}
