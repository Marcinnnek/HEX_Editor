using System.Diagnostics;
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

    public static readonly DependencyProperty RowGapProperty = DependencyProperty.Register(
        nameof(RowGap), typeof(Thickness), typeof(HexViewControl), new PropertyMetadata(default(Thickness)));

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

    public Thickness RowGap
    {
        get => (Thickness)GetValue(RowGapProperty);
        private set => SetValue(RowGapProperty, value);
    }

    public event EventHandler<AddressRangeSelectedEventArgs>? AddressRangeSelected;
    public event EventHandler<ByteClickFailedEventArgs>? ByteClickFailed;

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
        if (sender is not HexByteRowControl row) return;
        uint? address = null;
        try
        {
            if (!row.TryGetAddressAt(e.GetPosition(row), out var hitAddress)) return;
            address = hitAddress;
            var anchor = Keyboard.Modifiers.HasFlag(ModifierKeys.Shift) && _anchorAddress is uint existing ? existing : hitAddress;
            _anchorAddress = hitAddress;
            AddressRangeSelected?.Invoke(this, new AddressRangeSelectedEventArgs(
                Math.Min(anchor, hitAddress), Math.Max(anchor, hitAddress)));
            e.Handled = true;
        }
        catch (Exception exception)
        {
            e.Handled = true;
            if (address is null && row.Row is { } viewRow && HexAddressParser.TryParse(viewRow.Address, out var start))
                address = start;
            Trace.TraceError($"HEX byte single-click failed: address={(address is uint value ? $"0x{value:X8}" : "unknown")}, " +
                $"exception={exception.GetType().Name}, hresult=0x{exception.HResult:X8}");
            try { ByteClickFailed?.Invoke(this, new ByteClickFailedEventArgs(address, exception)); }
            catch (Exception callbackException)
            {
                Trace.TraceError($"HEX byte click failure callback failed: exception={callbackException.GetType().Name}, " +
                    $"hresult=0x{callbackException.HResult:X8}");
            }
        }
    }
}

public sealed class ByteClickFailedEventArgs(uint? address, Exception exception) : EventArgs
{
    public uint? Address { get; } = address;
    public Exception Exception { get; } = exception;
}

public sealed class AddressRangeSelectedEventArgs(uint startAddress, uint endAddress) : EventArgs
{
    public uint StartAddress { get; } = startAddress;
    public uint EndAddress { get; } = endAddress;
    public long Length => (long)EndAddress - StartAddress + 1;
}
