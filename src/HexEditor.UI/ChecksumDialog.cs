using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using HexEditor.Core;
using HexEditor.Tools;

namespace HexEditor.UI;

/// <summary>Collects checksum parameters, displays a calculated result, and optionally writes it.</summary>
public sealed class ChecksumDialog : Window
{
    private readonly MainViewModel _viewModel;
    private readonly Func<int, string, OverlapDecision> _chooseOverlap;
    private readonly ComboBox _algorithm = new() { ItemsSource = new[] { "Additive", "CRC-16/CCITT-FALSE", "CRC-32/ISO-HDLC", "Custom CRC" }, SelectedIndex = 0 };
    private readonly TextBox _start = new() { Text = "00000000", Width = 105 };
    private readonly TextBox _end = new() { Text = "00000000", Width = 105 };
    private readonly ComboBox _width = new() { ItemsSource = new[] { 8, 16, 32 }, SelectedItem = 8, Width = 74 };
    private readonly ComboBox _missing = new() { ItemsSource = new[] { "Reject missing addresses", "Fill missing addresses" }, SelectedIndex = 0 };
    private readonly TextBox _fill = new() { Text = "FF", Width = 75 };
    private readonly ComboBox _byteOrder = new() { ItemsSource = new[] { "Big endian", "Little endian" }, SelectedIndex = 0 };
    private readonly TextBox _polynomial = new() { Text = "1021", Width = 100 };
    private readonly TextBox _initial = new() { Text = "FFFF", Width = 100 };
    private readonly CheckBox _reflectInput = new() { Content = "Reflect input" };
    private readonly CheckBox _reflectOutput = new() { Content = "Reflect output" };
    private readonly TextBox _finalXor = new() { Text = "0", Width = 100 };
    private readonly TextBox _writeAddress = new() { Text = "00000000", Width = 130 };
    private readonly TextBlock _result = new() { Text = "Calculate to inspect the result.", TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 8, 0, 4) };
    private readonly TextBlock _error = new() { Foreground = Brushes.Firebrick, TextWrapping = TextWrapping.Wrap };

    public ChecksumDialog(MainViewModel viewModel, Func<int, string, OverlapDecision> chooseOverlap)
    {
        _viewModel = viewModel;
        _chooseOverlap = chooseOverlap;
        Title = "Calculate checksum / CRC";
        Width = 510;
        SizeToContent = SizeToContent.Height;
        MinWidth = 480;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        ResizeMode = ResizeMode.NoResize;

        var root = new StackPanel { Margin = new Thickness(18) };
        AddField(root, "Algorithm", _algorithm);
        AddField(root, "Inclusive address range", RangeInputs());
        AddField(root, "Width (bits)", _width);
        AddField(root, "Missing addresses", _missing);
        AddField(root, "Fill byte (hex)", _fill);
        AddField(root, "Result byte order", _byteOrder);
        var custom = new StackPanel { Margin = new Thickness(0, 7, 0, 0) };
        custom.Children.Add(new TextBlock { Text = "Custom CRC parameters (hexadecimal)" });
        var customFields = new WrapPanel { Margin = new Thickness(0, 4, 0, 0) };
        customFields.Children.Add(Labeled("Polynomial", _polynomial));
        customFields.Children.Add(Labeled("Initial", _initial));
        customFields.Children.Add(_reflectInput);
        customFields.Children.Add(_reflectOutput);
        customFields.Children.Add(Labeled("Final XOR", _finalXor));
        custom.Children.Add(customFields);
        root.Children.Add(custom);
        root.Children.Add(_result);
        root.Children.Add(_error);
        var writeRow = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 10, 0, 10) };
        writeRow.Children.Add(new TextBlock { Text = "Write result at", VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 8, 0) });
        writeRow.Children.Add(_writeAddress);
        var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
        var calculate = new Button { Content = "Calculate", IsDefault = true, MinWidth = 90, Padding = new Thickness(10, 5, 10, 5), Margin = new Thickness(0, 0, 8, 0) };
        calculate.Click += (_, _) => Calculate();
        var write = new Button { Content = "Write result", MinWidth = 90, Padding = new Thickness(10, 5, 10, 5), Margin = new Thickness(0, 0, 8, 0) };
        write.Click += (_, _) => Write();
        var cancel = new Button { Content = "Close", IsCancel = true, MinWidth = 80, Padding = new Thickness(10, 5, 10, 5) };
        buttons.Children.Add(calculate);
        buttons.Children.Add(write);
        buttons.Children.Add(cancel);
        root.Children.Add(writeRow);
        root.Children.Add(buttons);
        Content = root;
        _algorithm.SelectionChanged += (_, _) => UpdateControls();
        _missing.SelectionChanged += (_, _) => _fill.IsEnabled = _missing.SelectedIndex == 1;
        UpdateControls();
        _fill.IsEnabled = false;
        RenderResult();
        _viewModel.Checksum.PropertyChanged += ChecksumChanged;
        Closed += (_, _) => _viewModel.Checksum.PropertyChanged -= ChecksumChanged;
    }

    private void Calculate()
    {
        _error.Text = string.Empty;
        try
        {
            var request = BuildRequest();
            var result = _viewModel.CalculateChecksum(request);
            _result.Text = $"{result.AlgorithmName}: {result.FormattedValue}\n{result.Parameters}";
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException or FormatException or OverflowException)
        {
            _error.Text = exception.Message;
        }
    }

    private void Write()
    {
        _error.Text = string.Empty;
        try
        {
            if (!HexAddressParser.TryParse(_writeAddress.Text, out var address))
                throw new ArgumentException("Write address must be a valid 32-bit hexadecimal address.");
            var result = _viewModel.Checksum.GetWritableResult();
            var collisions = Enumerable.Range(0, result.WidthBits / 8)
                .Count(offset => (ulong)address + (uint)offset <= uint.MaxValue && _viewModel.CurrentImage.TryGetByte(address + (uint)offset, out _));
            if (collisions > 0 && _chooseOverlap(collisions, "checksum write") == OverlapDecision.Cancel) return;
            var decision = collisions > 0 ? OverlapDecision.OverwriteIncoming : (OverlapDecision?)null;
            _viewModel.WriteChecksumResult(address, decision);
            _error.Foreground = SystemColors.ControlTextBrush;
            _error.Text = "Checksum bytes written as one undoable edit.";
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException or FormatException or OverflowException)
        {
            _error.Foreground = Brushes.Firebrick;
            _error.Text = exception.Message;
        }
    }

    private ChecksumRequest BuildRequest()
    {
        if (!HexAddressParser.TryParse(_start.Text, out var start)) throw new ArgumentException("Start address must be valid hexadecimal.");
        if (!HexAddressParser.TryParse(_end.Text, out var end) || end < start) throw new ArgumentException("End address must be valid hexadecimal and not precede the start address.");
        if (_width.SelectedItem is not int width) throw new ArgumentException("Choose a checksum width.");
        var missing = _missing.SelectedIndex switch
        {
            0 => MissingAddressTreatment.Reject,
            1 => MissingAddressTreatment.FillByte,
            _ => throw new ArgumentException("Choose how missing addresses are treated.")
        };
        byte? fill = missing == MissingAddressTreatment.FillByte ? ParseByte(_fill.Text, "Fill byte") : null;
        var order = _byteOrder.SelectedIndex switch
        {
            0 => ChecksumByteOrder.BigEndian,
            1 => ChecksumByteOrder.LittleEndian,
            _ => throw new ArgumentException("Choose the result byte order.")
        };
        return _algorithm.SelectedItem?.ToString() switch
        {
            "Additive" => ChecksumRequest.Additive(start, end, width, missing, fill, order),
            "CRC-16/CCITT-FALSE" => ChecksumRequest.Crc16CcittFalse(start, end, missing, fill, order),
            "CRC-32/ISO-HDLC" => ChecksumRequest.Crc32IsoHdlc(start, end, missing, fill, order),
            "Custom CRC" => ChecksumRequest.CustomCrc(start, end, width,
                ParseHex(_polynomial.Text, "Polynomial"), ParseHex(_initial.Text, "Initial value"),
                _reflectInput.IsChecked == true, _reflectOutput.IsChecked == true,
                ParseHex(_finalXor.Text, "Final XOR"), missing, fill, order),
            _ => throw new ArgumentException("Choose a checksum algorithm.")
        };
    }

    private void ChecksumChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ChecksumViewModel.Result))
            RenderResult();
    }

    private void RenderResult() => _result.Text = _viewModel.Checksum.Result is { } result
        ? $"{result.AlgorithmName}: {result.FormattedValue}" + (result.IsStale ? " (STALE — recalculate before writing)" : string.Empty) + $"\n{result.Parameters}"
        : "Calculate to inspect the result.";

    private void UpdateControls()
    {
        var custom = _algorithm.SelectedIndex == 3;
        _width.IsEnabled = _algorithm.SelectedIndex is 0 or 3;
        _polynomial.IsEnabled = _initial.IsEnabled = _reflectInput.IsEnabled = _reflectOutput.IsEnabled = _finalXor.IsEnabled = custom;
    }

    private FrameworkElement RangeInputs()
    {
        var row = new StackPanel { Orientation = Orientation.Horizontal };
        row.Children.Add(_start);
        row.Children.Add(new TextBlock { Text = " to ", VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(6, 0, 6, 0) });
        row.Children.Add(_end);
        return row;
    }

    private static FrameworkElement Labeled(string label, FrameworkElement control)
    {
        var panel = new StackPanel { Margin = new Thickness(0, 0, 8, 4) };
        panel.Children.Add(new TextBlock { Text = label });
        panel.Children.Add(control);
        return panel;
    }

    private static void AddField(Panel panel, string label, FrameworkElement control)
    {
        panel.Children.Add(new TextBlock { Text = label, Margin = new Thickness(0, 7, 0, 2) });
        panel.Children.Add(control);
    }

    private static ulong ParseHex(string text, string field)
    {
        text = text.Trim();
        if (text.StartsWith("0x", StringComparison.OrdinalIgnoreCase)) text = text[2..];
        if (!ulong.TryParse(text, NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out var value))
            throw new ArgumentException($"{field} must be a hexadecimal value.");
        return value;
    }

    private static byte ParseByte(string text, string field)
    {
        var value = ParseHex(text, field);
        if (value > byte.MaxValue) throw new ArgumentException($"{field} must fit in one byte (00–FF).");
        return (byte)value;
    }
}
