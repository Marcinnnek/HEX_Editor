using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace HexEditor.UI;

public partial class HexViewControl : UserControl
{
    private uint? _anchorAddress;

    public HexViewControl()
    {
        InitializeComponent();
    }

    public static readonly DependencyProperty ItemsSourceProperty = DependencyProperty.Register(
        nameof(ItemsSource), typeof(System.Collections.IEnumerable), typeof(HexViewControl), new PropertyMetadata(null));

    public static readonly DependencyProperty ShowAsciiProperty = DependencyProperty.Register(
        nameof(ShowAscii), typeof(bool), typeof(HexViewControl), new PropertyMetadata(true, OnShowAsciiChanged));

    public System.Collections.IEnumerable? ItemsSource
    {
        get => (System.Collections.IEnumerable?)GetValue(ItemsSourceProperty);
        set => SetValue(ItemsSourceProperty, value);
    }

    public bool ShowAscii
    {
        get => (bool)GetValue(ShowAsciiProperty);
        set => SetValue(ShowAsciiProperty, value);
    }

    public DataGrid ItemsGrid => HexGrid;

    public event EventHandler<AddressRangeSelectedEventArgs>? AddressRangeSelected;

    public void ScrollToRow(int rowIndex)
    {
        if ((uint)rowIndex >= (uint)HexGrid.Items.Count) return;
        HexGrid.ScrollIntoView(HexGrid.Items[rowIndex]);
    }

    private static void OnShowAsciiChanged(DependencyObject owner, DependencyPropertyChangedEventArgs e)
    {
        if (owner is HexViewControl control)
            control.AsciiColumn.Visibility = (bool)e.NewValue ? Visibility.Visible : Visibility.Collapsed;
    }

    private void ByteCell_Click(object sender, MouseButtonEventArgs e)
    {
        if (sender is not FrameworkElement { DataContext: HexByteCell { Address: uint address } }) return;
        var anchor = Keyboard.Modifiers.HasFlag(ModifierKeys.Shift) && _anchorAddress is uint existing ? existing : address;
        _anchorAddress = address;
        AddressRangeSelected?.Invoke(this, new AddressRangeSelectedEventArgs(
            Math.Min(anchor, address), Math.Max(anchor, address)));
        e.Handled = true;
    }
}

public sealed class AddressRangeSelectedEventArgs(uint startAddress, uint endAddress) : EventArgs
{
    public uint StartAddress { get; } = startAddress;
    public uint EndAddress { get; } = endAddress;
    public long Length => (long)EndAddress - StartAddress + 1;
}
