using System.Diagnostics;
using System.Collections.ObjectModel;
using System.Collections;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace HexEditor.UI;

public partial class HexViewControl : UserControl
{
    private uint? _anchorAddress;
    private bool _refreshingWindow;
    private int _windowStart;
    private int _visibleRowCount = 24;
    public WindowedRowCollection WindowRows { get; } = [];

    public HexViewControl()
    {
        InitializeComponent();
    }

    public static readonly DependencyProperty ItemsSourceProperty = DependencyProperty.Register(
        nameof(ItemsSource), typeof(IEnumerable), typeof(HexViewControl), new PropertyMetadata(null, OnItemsSourceChanged));

    public static readonly DependencyProperty UseWindowedScrollingProperty = DependencyProperty.Register(
        nameof(UseWindowedScrolling), typeof(bool), typeof(HexViewControl), new PropertyMetadata(false, OnWindowModeChanged));

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

    public bool UseWindowedScrolling
    {
        get => (bool)GetValue(UseWindowedScrollingProperty);
        set => SetValue(UseWindowedScrollingProperty, value);
    }

    public int FirstVisibleRowIndex => _windowStart;

    public DataGrid ItemsGrid => HexGrid;

    public Thickness RowGap
    {
        get => (Thickness)GetValue(RowGapProperty);
        private set => SetValue(RowGapProperty, value);
    }

    public event EventHandler<AddressRangeSelectedEventArgs>? AddressRangeSelected;
    public event EventHandler<ByteClickFailedEventArgs>? ByteClickFailed;
    public event EventHandler<WindowedByteDoubleClickEventArgs>? WindowedByteDoubleClicked;

    public void ScrollToRow(int rowIndex)
    {
        if (UseWindowedScrolling) { SetWindowStart(rowIndex); return; }
        if ((uint)rowIndex >= (uint)HexGrid.Items.Count) return;
        HexGrid.ScrollIntoView(HexGrid.Items[rowIndex]);
    }

    public void ScrollToMatch(int rowIndex)
    {
        if (UseWindowedScrolling)
        {
            SetWindowStart(rowIndex);
            WindowedScroll.ScrollToHorizontalOffset(0);
            return;
        }
        if ((uint)rowIndex >= (uint)HexGrid.Items.Count) return;
        HexGrid.ScrollIntoView(HexGrid.Items[rowIndex]);
        HexGrid.UpdateLayout();

        var scrollViewer = FindScrollViewer(HexGrid);
        if (scrollViewer is null) return;
        scrollViewer.ScrollToHorizontalOffset(0);
        // DataGrid item scrolling leaves a sliver of the prior row at an exact item offset.
        scrollViewer.ScrollToVerticalOffset(rowIndex + 1);
        HexGrid.UpdateLayout();
    }

    private static ScrollViewer? FindScrollViewer(DependencyObject parent)
    {
        for (var index = 0; index < System.Windows.Media.VisualTreeHelper.GetChildrenCount(parent); index++)
        {
            var child = System.Windows.Media.VisualTreeHelper.GetChild(parent, index);
            if (child is ScrollViewer viewer) return viewer;
            if (FindScrollViewer(child) is { } descendant) return descendant;
        }
        return null;
    }

    private static void OnShowAsciiChanged(DependencyObject owner, DependencyPropertyChangedEventArgs e)
    {
        if (owner is HexViewControl control)
        {
            control.AsciiColumn.Visibility = (bool)e.NewValue ? Visibility.Visible : Visibility.Collapsed;
            control.WindowedAsciiHeader.Visibility = (bool)e.NewValue ? Visibility.Visible : Visibility.Collapsed;
            control.RefreshWindow();
        }
    }

    private static void OnItemsSourceChanged(DependencyObject owner, DependencyPropertyChangedEventArgs e)
    {
        if (owner is not HexViewControl control) return;
        control.HexGrid.ItemsSource = control.UseWindowedScrolling ? null : e.NewValue as IEnumerable;
        control._windowStart = 0;
        control.RefreshWindow();
    }

    private static void OnWindowModeChanged(DependencyObject owner, DependencyPropertyChangedEventArgs e)
    {
        if (owner is not HexViewControl control) return;
        var enabled = (bool)e.NewValue;
        control.HexGrid.ItemsSource = enabled ? null : control.ItemsSource as IEnumerable;
        control.HexGrid.Visibility = enabled ? Visibility.Collapsed : Visibility.Visible;
        control.WindowedView.Visibility = enabled ? Visibility.Visible : Visibility.Collapsed;
        ScrollViewer.SetCanContentScroll(control.WindowedScroll, enabled);
        control._windowStart = 0;
        control.RefreshWindow();
    }

    private void SetWindowStart(int index)
    {
        if (ItemsSource is not IList<HexViewRow> rows || rows.Count == 0) return;
        var max = Math.Max(0, rows.Count - _visibleRowCount);
        _windowStart = Math.Clamp(index, 0, max);
        _refreshingWindow = true;
        RowThumb.Value = _windowStart;
        _refreshingWindow = false;
        RefreshWindow();
    }

    private void RefreshWindow()
    {
        if (!UseWindowedScrolling || !IsLoaded || ItemsSource is not IList<HexViewRow> rows) return;
        _visibleRowCount = Math.Max(1, (int)Math.Ceiling(Math.Max(1, WindowedView.ActualHeight - 25) / 18d) + 2);
        var maxStart = Math.Max(0, rows.Count - _visibleRowCount);
        _windowStart = Math.Clamp(_windowStart, 0, maxStart);
        _refreshingWindow = true;
        RowThumb.Maximum = maxStart;
        RowThumb.ViewportSize = _visibleRowCount;
        RowThumb.Value = _windowStart;
        _refreshingWindow = false;
        WindowRows.ReplaceAll(rows, _windowStart, Math.Min(rows.Count, _windowStart + _visibleRowCount));
    }

    private void RowThumb_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_refreshingWindow || !UseWindowedScrolling) return;
        _windowStart = (int)Math.Round(e.NewValue);
        RefreshWindow();
    }

    private void WindowedScroll_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (RowThumb.Maximum <= 0) return;
        RowThumb.Value = Math.Clamp(RowThumb.Value + (e.Delta < 0 ? 3 : -3), RowThumb.Minimum, RowThumb.Maximum);
        e.Handled = true;
    }

    private void WindowedView_SizeChanged(object sender, SizeChangedEventArgs e) => RefreshWindow();
    private void Control_Loaded(object sender, RoutedEventArgs e) => RefreshWindow();

    private void ByteCell_Click(object sender, MouseButtonEventArgs e)
    {
        if (sender is not HexByteRowControl row) return;
        e.Handled = SelectByteAt(row, e.GetPosition(row), Keyboard.Modifiers.HasFlag(ModifierKeys.Shift));
        if (e.ClickCount >= 2)
            RaiseWindowedByteDoubleClick(row, e, e.GetPosition(row));
    }

    internal void RaiseWindowedByteDoubleClick(HexByteRowControl row, MouseButtonEventArgs mouseEvent, Point position) =>
        WindowedByteDoubleClicked?.Invoke(this, new WindowedByteDoubleClickEventArgs(row, mouseEvent, position));

    internal bool SelectByteAt(HexByteRowControl row, Point point, bool extendSelection)
    {
        uint? address = null;
        try
        {
            if (!row.TryGetAddressAt(point, out var hitAddress)) return false;
            address = hitAddress;
            var anchor = extendSelection && _anchorAddress is uint existing ? existing : hitAddress;
            _anchorAddress = hitAddress;
            AddressRangeSelected?.Invoke(this, new AddressRangeSelectedEventArgs(
                Math.Min(anchor, hitAddress), Math.Max(anchor, hitAddress)));
            return true;
        }
        catch (Exception exception)
        {
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
            return true;
        }
    }
}

public sealed class WindowedRowCollection : ObservableCollection<WindowedRowItem>
{
    public void ReplaceAll(IList<HexViewRow> rows, int start, int end)
    {
        var count = end - start;
        while (Count > count) RemoveAt(Count - 1);
        while (Count < count) Add(new WindowedRowItem(rows[start + Count]));
        for (var index = 0; index < count; index++) this[index].Row = rows[start + index];
    }
}

public sealed class WindowedRowItem(HexViewRow row) : System.ComponentModel.INotifyPropertyChanged
{
    private HexViewRow _row = row;
    public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;
    public HexViewRow Row
    {
        get => _row;
        set
        {
            if (ReferenceEquals(_row, value)) return;
            _row = value;
            PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(nameof(Row)));
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

public sealed class WindowedByteDoubleClickEventArgs(HexByteRowControl row, MouseButtonEventArgs mouseEvent, Point position) : EventArgs
{
    public HexByteRowControl Row { get; } = row;
    public MouseButtonEventArgs MouseEvent { get; } = mouseEvent;
    public Point Position { get; } = position;
}
