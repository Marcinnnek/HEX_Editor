using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace HexEditor.UI;

/// <summary>Collects a destination address and previews the selected segment relocation.</summary>
public sealed class RelocateSegmentDialog : Window
{
    private readonly TextBox _destination;
    private readonly TextBlock _preview = new() { Margin = new Thickness(0, 8, 0, 12) };
    private readonly int _byteCount;

    public RelocateSegmentDialog(uint startAddress, uint endAddress, int byteCount)
    {
        _byteCount = byteCount;
        _destination = new TextBox { MinWidth = 190, Text = startAddress.ToString("X8") };
        Title = "Relocate memory segment";
        SizeToContent = SizeToContent.WidthAndHeight;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        ResizeMode = ResizeMode.NoResize;
        var panel = new StackPanel { Margin = new Thickness(18), MinWidth = 360 };
        panel.Children.Add(new TextBlock { Text = $"Current range: 0x{startAddress:X8}–0x{endAddress:X8}" });
        panel.Children.Add(new TextBlock { Text = $"Byte count: {byteCount:N0}", Margin = new Thickness(0, 4, 0, 12) });
        panel.Children.Add(new TextBlock { Text = "New start address (hexadecimal):" });
        panel.Children.Add(_destination);
        panel.Children.Add(_preview);
        var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
        var cancel = new Button { Content = "Cancel", IsCancel = true, MinWidth = 80, Margin = new Thickness(0, 0, 8, 0) };
        var apply = new Button { Content = "Continue", IsDefault = true, MinWidth = 80 };
        apply.Click += (_, _) => { if (TryGetDestination(out _)) DialogResult = true; };
        buttons.Children.Add(cancel);
        buttons.Children.Add(apply);
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

    private bool TryGetDestination(out uint destination)
    {
        if (HexAddressParser.TryParse(_destination.Text, out destination) && _byteCount > 0 &&
            (ulong)destination + (uint)_byteCount - 1 <= uint.MaxValue)
        {
            DestinationAddress = destination;
            return true;
        }

        _preview.Foreground = Brushes.Firebrick;
        _preview.Text = "Enter a valid 32-bit address with room for the entire segment.";
        return false;
    }

    private void UpdatePreview()
    {
        if (HexAddressParser.TryParse(_destination.Text, out var destination) && _byteCount > 0 &&
            (ulong)destination + (uint)_byteCount - 1 <= uint.MaxValue)
        {
            _preview.Foreground = SystemColors.ControlTextBrush;
            _preview.Text = $"New range: 0x{destination:X8}–0x{destination + (uint)_byteCount - 1:X8}";
        }
        else
        {
            _preview.Foreground = Brushes.Firebrick;
            _preview.Text = "Enter a valid 32-bit address with room for the entire segment.";
        }
    }
}
