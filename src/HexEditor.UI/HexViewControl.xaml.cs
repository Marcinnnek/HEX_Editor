using System.Diagnostics;
using System.Collections.ObjectModel;
using System.Collections;
using System.Collections.Specialized;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Globalization;
using System.Windows.Media;

namespace HexEditor.UI;

public partial class HexViewControl : UserControl
{
    private uint? _anchorAddress;
    private bool _refreshingWindow;
    private int _windowStart;
    private int _visibleRowCount = 24;
    private static readonly DependencyPropertyKey HexDataColumnWidthPropertyKey = DependencyProperty.RegisterReadOnly(
        nameof(HexDataColumnWidth), typeof(double), typeof(HexViewControl), new PropertyMetadata(0d));
    private static readonly DependencyPropertyKey HexDataContentWidthPropertyKey = DependencyProperty.RegisterReadOnly(
        nameof(HexDataContentWidth), typeof(double), typeof(HexViewControl), new PropertyMetadata(0d));
    public static readonly DependencyProperty HexDataColumnWidthProperty = HexDataColumnWidthPropertyKey.DependencyProperty;
    public static readonly DependencyProperty HexDataContentWidthProperty = HexDataContentWidthPropertyKey.DependencyProperty;
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

    public static readonly DependencyProperty BytesPerRowProperty = DependencyProperty.Register(
        nameof(BytesPerRow), typeof(int), typeof(HexViewControl), new PropertyMetadata(16, OnGridGeometryChanged));

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

    public int BytesPerRow { get => (int)GetValue(BytesPerRowProperty); set => SetValue(BytesPerRowProperty, value); }
    public double HexDataColumnWidth => (double)GetValue(HexDataColumnWidthProperty);
    public double HexDataContentWidth => (double)GetValue(HexDataContentWidthProperty);

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
    public event EventHandler? FirstVisibleRowChanged;

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
        //scrollViewer.ScrollToVerticalOffset(rowIndex + 1);
        scrollViewer.ScrollToVerticalOffset(rowIndex);
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
        if (e.OldValue is INotifyCollectionChanged previous)
            previous.CollectionChanged -= control.ItemsSource_CollectionChanged;
        if (e.NewValue is INotifyCollectionChanged current)
            current.CollectionChanged += control.ItemsSource_CollectionChanged;
        control.HexGrid.ItemsSource = control.UseWindowedScrolling ? null : e.NewValue as IEnumerable;
        control._windowStart = 0;
        control.RefreshWindow();
    }

    private void ItemsSource_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (UseWindowedScrolling) RefreshWindow();
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
        var previousStart = _windowStart;
        _windowStart = Math.Clamp(index, 0, max);
        _refreshingWindow = true;
        RowThumb.Value = _windowStart;
        _refreshingWindow = false;
        RefreshWindow();
        if (_windowStart != previousStart) FirstVisibleRowChanged?.Invoke(this, EventArgs.Empty);
    }

    private void RefreshWindow()
    {
        if (!UseWindowedScrolling || !IsLoaded || ItemsSource is not IList<HexViewRow> rows) return;
        _visibleRowCount = Math.Max(1, (int)Math.Ceiling(Math.Max(1, WindowedView.ActualHeight - 25) / 18d) + 1);
        var maxStart = Math.Max(0, rows.Count - _visibleRowCount);
        _windowStart = Math.Clamp(_windowStart, 0, maxStart);
        _refreshingWindow = true;
        RowThumb.Maximum = maxStart;
        RowThumb.ViewportSize = GetVisibleThumbViewport(maxStart);
        RowThumb.Value = _windowStart;
        _refreshingWindow = false;
        WindowRows.ReplaceAll(rows, _windowStart, Math.Min(rows.Count, _windowStart + _visibleRowCount));
    }

    private double GetVisibleThumbViewport(int maxStart)
    {
        // WPF's Track reserves space from ViewportSize before Thumb.MinHeight is applied.
        // A tiny viewport can therefore place the Page Down button over most of the thumb.
        const double minimumReservedThumbHeight = 32;
        var track = RowThumb.Template?.FindName("PART_Track", RowThumb) as Track;
        var trackHeight = track is { ActualHeight: > 0 }
            ? track.ActualHeight
            : Math.Max(0, WindowedView.ActualHeight - 36);
        if (maxStart == 0 || trackHeight <= minimumReservedThumbHeight) return _visibleRowCount;

        var minimumViewport = Math.Ceiling((double)maxStart * minimumReservedThumbHeight /
            (trackHeight - minimumReservedThumbHeight));
        return Math.Max(_visibleRowCount, minimumViewport);
    }

    private void RowThumb_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_refreshingWindow || !UseWindowedScrolling) return;
        var previousStart = _windowStart;
        _windowStart = (int)Math.Round(e.NewValue);
        RefreshWindow();
        if (_windowStart != previousStart) FirstVisibleRowChanged?.Invoke(this, EventArgs.Empty);
    }

    private void WindowedScroll_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (RowThumb.Maximum <= 0) return;
        RowThumb.Value = Math.Clamp(RowThumb.Value + (e.Delta < 0 ? 3 : -3), RowThumb.Minimum, RowThumb.Maximum);
        e.Handled = true;
    }

    private void WindowedView_SizeChanged(object sender, SizeChangedEventArgs e) => RefreshWindow();
    private void Control_Loaded(object sender, RoutedEventArgs e)
    {
        GridSpacingSettings.SpacingChanged += SpacingChanged;
        UpdateHexDataWidth(resetHorizontalScroll: false);
        RefreshWindow();
    }

    private void Control_Unloaded(object sender, RoutedEventArgs e) => GridSpacingSettings.SpacingChanged -= SpacingChanged;

    private void SpacingChanged() => Dispatcher.BeginInvoke(() => UpdateHexDataWidth(resetHorizontalScroll: true));

    private static void OnGridGeometryChanged(DependencyObject owner, DependencyPropertyChangedEventArgs e)
    {
        if (owner is HexViewControl control) control.UpdateHexDataWidth(resetHorizontalScroll: true);
    }

    private void UpdateHexDataWidth(bool resetHorizontalScroll)
    {
        if (BytesPerRow is not (8 or 16 or 32)) return;
        var sample = new FormattedText("0", CultureInfo.InvariantCulture, FlowDirection.LeftToRight,
            new Typeface(HexGrid.FontFamily, HexGrid.FontStyle, HexGrid.FontWeight, HexGrid.FontStretch),
            HexGrid.FontSize, Brushes.Black, VisualTreeHelper.GetDpi(this).PixelsPerDip);
        var width = BytesPerRow * 2 * sample.WidthIncludingTrailingWhitespace +
            (BytesPerRow - 1) * GridSpacingSettings.Current.HorizontalByteSpacing + 12;
        SetValue(HexDataColumnWidthPropertyKey, width);
        SetValue(HexDataContentWidthPropertyKey, width - 12);
        WindowedView.InvalidateMeasure();
        HexDataColumn.Width = width;
        if (resetHorizontalScroll) ResetHorizontalScroll();
    }

    private void ResetHorizontalScroll()
    {
        if (UseWindowedScrolling) { WindowedScroll.ScrollToHorizontalOffset(0); return; }
        var viewer = FindScrollViewer(HexGrid);
        if (viewer is null) return;
        var vertical = viewer.VerticalOffset;
        viewer.ScrollToHorizontalOffset(0);
        viewer.ScrollToVerticalOffset(vertical);
    }

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
