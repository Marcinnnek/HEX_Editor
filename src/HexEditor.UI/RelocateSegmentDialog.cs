using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace HexEditor.UI;

/// <summary>Collects a destination address and previews the selected segment relocation.</summary>
public sealed class RelocateSegmentDialog : Window
{
    private readonly TextBox _destination;
    private readonly TextBlock _preview = new() { Margin = new Thickness(0, 8, 0, 12) };
    private readonly long _byteCount;
    private readonly uint _startAddress;
    private readonly uint _endAddress;
    private readonly Func<string, bool> _confirmDelete;

    public RelocateSegmentDialog(uint startAddress, uint endAddress, long byteCount, bool canDelete = true,
        Func<string, bool>? confirmDelete = null, bool canRelocate = true)
    {
        _byteCount = byteCount;
        _startAddress = startAddress;
        _endAddress = endAddress;
        _confirmDelete = confirmDelete ?? ConfirmDelete;
        _destination = new TextBox {  
            MinWidth = 190,
            MaxLength = 10,
            FontSize = 15,
            Padding = new Thickness(8, 5, 8, 5),
            VerticalContentAlignment = VerticalAlignment.Center, 
            Text = startAddress.ToString("X8") };
        _destination.PreviewTextInput += Destination_PreviewTextInput;
        DataObject.AddPastingHandler(_destination, Destination_Pasting);
        Title = "Relocate memory segment";
        SizeToContent = SizeToContent.WidthAndHeight;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        ResizeMode = ResizeMode.NoResize;
        var panel = new StackPanel { Margin = new Thickness(18), MinWidth = 360 };
        panel.Children.Add(new TextBlock { Text = $"Current range: 0x{startAddress:X8}–0x{endAddress:X8}" });
        panel.Children.Add(new TextBlock { Text = $"Byte count: {byteCount:N0}", Margin = new Thickness(0, 4, 0, 12) });
        panel.Children.Add(new TextBlock { Text = "New start address (hexadecimal):", Margin = new Thickness(0, 8, 0, 12) });
        panel.Children.Add(_destination);
        panel.Children.Add(_preview);
        var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
        var cancel = new Button { Content = "Cancel", IsCancel = true, MinWidth = 80, Margin = new Thickness(0, 0, 8, 0) };
        buttons.Children.Add(cancel);
        if (canDelete)
        {
            var delete = new Button { Content = "Delete", MinWidth = 80, Margin = new Thickness(0, 0, 8, 0) };
            delete.Click += (_, _) =>
            {
                if (!_confirmDelete(DeleteConfirmationMessage)) return;
                Action = RelocateSegmentDialogAction.Delete;
                DialogResult = true;
            };
            buttons.Children.Add(delete);
        }
        if (canRelocate)
        {
            var apply = new Button { Content = "Continue", IsDefault = true, MinWidth = 80 };
            apply.Click += (_, _) => { if (TryGetDestination(out _)) { Action = RelocateSegmentDialogAction.Continue; DialogResult = true; } };
            buttons.Children.Add(apply);
        }
        panel.Children.Add(buttons);
        Content = panel;
        Loaded += (_, _) =>
        {
            _destination.Focus();
            _destination.SelectAll();
        };
        _destination.TextChanged += (_, _) => UpdatePreview();
        UpdatePreview();
    }

    public uint DestinationAddress { get; private set; }
    public string DestinationAddressText => _destination.Text;
    public RelocateSegmentDialogAction Action { get; private set; }
    public bool DeleteRequested => Action == RelocateSegmentDialogAction.Delete;
    public string DeleteConfirmationMessage =>
        $"Delete Block 0x{_startAddress:X8}–0x{_endAddress:X8} (Length: 0x{_byteCount:X} = {_byteCount:N0} bytes)?";

    private bool ConfirmDelete(string message) =>
        ThemedMessageBox.Show(this, message, "Confirm Block deletion", MessageBoxButton.YesNo,
            MessageBoxImage.Warning) == MessageBoxResult.Yes;

    private void Destination_PreviewTextInput(object sender, TextCompositionEventArgs e)
    {
        var proposed = _destination.Text.Remove(_destination.SelectionStart, _destination.SelectionLength)
            .Insert(_destination.SelectionStart, e.Text);
        e.Handled = !IsHexAddressInput(proposed);
    }

    private void Destination_Pasting(object sender, DataObjectPastingEventArgs e)
    {
        if (!e.SourceDataObject.GetDataPresent(DataFormats.UnicodeText, true) ||
            e.SourceDataObject.GetData(DataFormats.UnicodeText, true) is not string pasted)
        {
            e.CancelCommand();
            return;
        }

        var proposed = _destination.Text.Remove(_destination.SelectionStart, _destination.SelectionLength)
            .Insert(_destination.SelectionStart, pasted);
        if (!IsHexAddressInput(proposed)) e.CancelCommand();
    }

    private static bool IsHexAddressInput(string text)
    {
        if (text.Length == 0) return true;
        var digits = text.StartsWith("0x", StringComparison.OrdinalIgnoreCase) ? text[2..] : text;
        return digits.Length <= 8 && digits.All(Uri.IsHexDigit);
    }

    private bool TryGetDestination(out uint destination)
    {
        if (HexAddressParser.TryParse(_destination.Text, out destination) && _byteCount > 0 &&
            (ulong)destination + (ulong)_byteCount - 1 <= uint.MaxValue)
        {
            DestinationAddress = destination;
            return true;
        }

        _preview.SetResourceReference(TextBlock.ForegroundProperty, "HexErrorBrush");
        _preview.Text = "Enter a valid 32-bit address with room for the entire segment.";
        return false;
    }

    private void UpdatePreview()
    {
        if (HexAddressParser.TryParse(_destination.Text, out var destination) && _byteCount > 0 &&
            (ulong)destination + (ulong)_byteCount - 1 <= uint.MaxValue)
        {
            _preview.SetResourceReference(TextBlock.ForegroundProperty, "HexTextBrush");
            _preview.Text = $"New range: 0x{destination:X8}–0x{(uint)((ulong)destination + (ulong)_byteCount - 1):X8}";
        }
        else
        {
            _preview.SetResourceReference(TextBlock.ForegroundProperty, "HexErrorBrush");
            _preview.Text = "Enter a valid 32-bit address with room for the entire segment.";
        }
    }
}

public enum RelocateSegmentDialogAction
{
    Continue,
    Delete
}
