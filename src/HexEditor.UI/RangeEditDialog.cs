using System.Globalization;
using System.Windows;
using System.Windows.Controls;

namespace HexEditor.UI;

internal enum RangeEditDialogKind { SelectRange, Insert, Fill }

internal sealed record RangeEditDialogResult(uint Start, uint End, byte[]? Pattern, FillPolicy FillPolicy = FillPolicy.KeepExisting);

internal sealed class RangeEditDialog : Window
{
    private readonly RangeEditDialogKind _kind;
    private readonly TextBox _start = new();
    private readonly TextBox _end = new();
    private readonly TextBox _pattern = new() { MinWidth = 220 };
    private readonly TextBlock _error = new() { TextWrapping = TextWrapping.Wrap };
    private readonly TextBlock _count = new() { Name = "InclusiveAddressCount" };
    private readonly Button _allRegion = new() { Name = "GetFillAllRegion", Content = "Get fill all region", MinWidth = 140 };
    private readonly RadioButton _keep = new() { Name = "KeepExistingData", Content = "Keep existing data", IsChecked = true, GroupName = "FillPolicy" };
    private readonly RadioButton _overwrite = new() { Name = "OverwriteExistingData", Content = "Overwrite existing data", GroupName = "FillPolicy" };
    private readonly (uint Start, uint End)? _fillAllRegion;

    public RangeEditDialog(RangeEditDialogKind kind, uint start, uint end, (uint Start, uint End)? fillAllRegion = null)
    {
        _kind = kind;
        _fillAllRegion = fillAllRegion;
        _error.SetResourceReference(TextBlock.ForegroundProperty, "HexErrorBrush");
        HexAddressInput.Configure(_start);
        HexAddressInput.Configure(_end);
        Title = kind switch { RangeEditDialogKind.SelectRange => "Select Range", RangeEditDialogKind.Insert => "Edit / Insert", _ => "Fill Range" };
        Width = 390;
        SizeToContent = SizeToContent.Height;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        ResizeMode = ResizeMode.NoResize;
        ShowInTaskbar = false;
        _start.Text = start.ToString("X8", CultureInfo.InvariantCulture);
        _end.Text = end.ToString("X8", CultureInfo.InvariantCulture);

        var panel = new StackPanel { Margin = new Thickness(18) };
        panel.Children.Add(AddressFieldRow.Create(kind == RangeEditDialogKind.Insert ? "Target address" : "Start address",
            _start, new Thickness(0, 8, 0, 3)));
        if (kind != RangeEditDialogKind.Insert)
            panel.Children.Add(AddressFieldRow.Create("End address (inclusive)", _end, new Thickness(0, 8, 0, 3)));
        if (kind != RangeEditDialogKind.SelectRange)
            AddField(panel, kind == RangeEditDialogKind.Insert ? "Bytes (hex)" : "Repeating byte pattern (hex)", _pattern);
        if (kind == RangeEditDialogKind.Fill)
        {
            _allRegion.IsEnabled = fillAllRegion is not null;
            _allRegion.Click += (_, _) => SetFillAllRegion();
            panel.Children.Add(_allRegion);
            panel.Children.Add(_count);
            panel.Children.Add(_keep);
            panel.Children.Add(_overwrite);
            _start.TextChanged += (_, _) => UpdateCount();
            _end.TextChanged += (_, _) => UpdateCount();
            UpdateCount();
        }
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
            var count = (ulong)end - start + 1;
            if (_kind == RangeEditDialogKind.Fill && count > 50_000_000 &&
                MessageBox.Show(this, $"This fill covers {count:N0} addresses. Continue?", "Large fill range",
                    MessageBoxButton.YesNo, MessageBoxImage.Warning, MessageBoxResult.No) != MessageBoxResult.Yes)
                return;
            Result = new RangeEditDialogResult(start, end, pattern,
                _overwrite.IsChecked == true ? FillPolicy.OverwriteExisting : FillPolicy.KeepExisting);
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

    private void SetFillAllRegion()
    {
        if (_fillAllRegion is not { } range) return;
        _start.Text = range.Start.ToString("X8", CultureInfo.InvariantCulture);
        _end.Text = range.End.ToString("X8", CultureInfo.InvariantCulture);
    }

    private void UpdateCount()
    {
        if (!uint.TryParse(_start.Text, NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out var start) ||
            !uint.TryParse(_end.Text, NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out var end) || end < start)
            _count.Text = "Inclusive address count: —";
        else
        {
            var count = (ulong)end - start + 1;
            _count.Text = $"Inclusive address count: {count:N0}" + (count > 50_000_000 ? " (confirmation required)" : string.Empty);
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
