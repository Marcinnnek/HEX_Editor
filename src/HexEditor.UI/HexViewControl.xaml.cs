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
using System.ComponentModel;

namespace HexEditor.UI;

public partial class HexViewControl : UserControl
{
    private uint? _anchorAddress;
    private bool _refreshingWindow;
    private int _windowStart;
    private int _visibleRowCount = 24;
    private int _selectedRowIndex = -1;
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
        HexGrid.LoadingRow += (_, e) => ApplyRowBackground(e.Row);
        DependencyPropertyDescriptor.FromProperty(Control.FontFamilyProperty, typeof(DataGrid))!
            .AddValueChanged(HexGrid, (_, _) => { UpdateAddressColumnWidth(); UpdateHexDataWidth(resetHorizontalScroll: true); UpdateAsciiColumnWidth(); });
        DependencyPropertyDescriptor.FromProperty(Control.FontSizeProperty, typeof(DataGrid))!
            .AddValueChanged(HexGrid, (_, _) => { UpdateAddressColumnWidth(); UpdateHexDataWidth(resetHorizontalScroll: true); UpdateAsciiColumnWidth(); });
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

    public static readonly DependencyProperty AddressColumnWidthProperty = DependencyProperty.Register(
        nameof(AddressColumnWidth), typeof(double), typeof(HexViewControl), new PropertyMetadata(100d));
    public double AddressColumnWidth => (double)GetValue(AddressColumnWidthProperty);

    public event EventHandler<AddressRangeSelectedEventArgs>? AddressRangeSelected;
    public event EventHandler<ByteClickFailedEventArgs>? ByteClickFailed;
    public event EventHandler<WindowedByteDoubleClickEventArgs>? WindowedByteDoubleClicked;
    public event EventHandler? FirstVisibleRowChanged;
    public event EventHandler<AddressRowSelectedEventArgs>? AddressRowSelected;

    public int SelectedRowIndex => _selectedRowIndex;

    public void SetSelectedRow(int rowIndex)
    {
        _selectedRowIndex = rowIndex;
        foreach (var item in WindowRows) item.IsSelected = item.RowIndex == rowIndex;
        if (!UseWindowedScrolling)
        {
            for (var i = 0; i < HexGrid.Items.Count; i++)
                if (HexGrid.ItemContainerGenerator.ContainerFromIndex(i) is DataGridRow row) ApplyRowBackground(row);
        }
    }

    private void ApplyRowBackground(DataGridRow row) =>
        row.Background = row.GetIndex() == _selectedRowIndex
            ? TryFindResource("HexSelectionBrush") as Brush
            : TryFindResource("HexSurfaceBrush") as Brush;

    private void AddressRow_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.OriginalSource is not DependencyObject source) return;
        if (FindAncestor<HexByteRowControl>(source) is { } byteControl &&
            SelectRowFromOuterInset(byteControl, e.GetPosition(byteControl)))
        {
            e.Handled = true;
            return;
        }
        HexViewRow? row = null;
        var index = -1;
        if (FindAncestor<DataGridRow>(source) is { Item: HexViewRow dataGridRow } dataRow &&
            FindAncestor<DataGridCell>(source) is { } cell &&
            (cell.Column.DisplayIndex == 0 || FindAncestor<HexByteRowControl>(source) is null))
        {
            row = dataGridRow;
            index = dataRow.GetIndex();
        }
        else if (FindAncestor<ContentPresenter>(source)?.DataContext is WindowedRowItem item &&
                 (FindAncestor<HexRowAddressControl>(source) is not null || FindAncestor<HexByteRowControl>(source) is null))
        {
            row = FindAncestor<HexRowAddressControl>(source)?.Row ?? item.Row;
            index = item.RowIndex;
        }
        if (row is null || index < 0) return;
        SetSelectedRow(index);
        AddressRowSelected?.Invoke(this, new AddressRowSelectedEventArgs(index, row));
        e.Handled = true;
    }

    internal bool SelectRowFromOuterInset(HexByteRowControl byteControl, Point position)
    {
        if (!byteControl.IsOuterInsetAt(position)) return false;

        HexViewRow? row;
        int index;
        if (FindAncestor<DataGridRow>(byteControl) is { Item: HexViewRow dataGridRow } dataGridRowContainer)
        {
            row = dataGridRow;
            index = dataGridRowContainer.GetIndex();
        }
        else if (FindAncestor<ContentPresenter>(byteControl)?.DataContext is WindowedRowItem item)
        {
            row = item.Row;
            index = item.RowIndex;
        }
        else return false;

        SetSelectedRow(index);
        AddressRowSelected?.Invoke(this, new AddressRowSelectedEventArgs(index, row));
        return true;
    }

    private void AddressCell_Click(object sender, MouseButtonEventArgs e)
    {
        if (sender is not HexRowAddressControl { Row: { } row } || ItemsSource is not IList<HexViewRow> rows) return;
        var index = -1;
        for (var candidate = 0; candidate < rows.Count; candidate++)
        {
            var candidateRow = rows[candidate];
            if (candidateRow.IsGapMarker == row.IsGapMarker && candidateRow.Address == row.Address &&
                candidateRow.GapDescription == row.GapDescription)
            {
                index = candidate;
                break;
            }
        }
        if (index < 0) return;
        SetSelectedRow(index);
        AddressRowSelected?.Invoke(this, new AddressRowSelectedEventArgs(index, row));
        e.Handled = true;
    }

    private static T? FindAncestor<T>(DependencyObject source) where T : DependencyObject
    {
        for (var current = source; current is not null; current = current is Visual ? VisualTreeHelper.GetParent(current) : LogicalTreeHelper.GetParent(current))
            if (current is T match) return match;
        return null;
    }

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
            control.UpdateAsciiColumnWidth();
        }
    }

    private void UpdateAsciiColumnWidth()
    {
        if (!IsLoaded) return;
        var sample = new FormattedText("0", System.Globalization.CultureInfo.InvariantCulture,
            FlowDirection.LeftToRight, new Typeface(HexGrid.FontFamily, HexGrid.FontStyle, HexGrid.FontWeight, HexGrid.FontStretch), HexGrid.FontSize, Foreground,
            VisualTreeHelper.GetDpi(this).PixelsPerDip);
        var width = BytesPerRow * sample.WidthIncludingTrailingWhitespace + 12;
        AsciiColumn.Width = new DataGridLength(width);
        WindowedHeaderAsciiColumn.Width = new GridLength(width);
        foreach (var item in WindowRows)
        {
            var container = WindowedRows.ItemContainerGenerator.ContainerFromItem(item) as FrameworkElement;
            if (container?.FindName("WindowedRowAsciiColumn") is ColumnDefinition column)
                column.Width = new GridLength(width);
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
        foreach (var item in WindowRows) item.IsSelected = item.RowIndex == _selectedRowIndex;
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
        UpdateAddressColumnWidth();
        UpdateHexDataWidth(resetHorizontalScroll: false);
        RefreshWindow();
        UpdateAsciiColumnWidth();
    }

    private void Control_Unloaded(object sender, RoutedEventArgs e) => GridSpacingSettings.SpacingChanged -= SpacingChanged;

    private void SpacingChanged() => Dispatcher.BeginInvoke(() => UpdateHexDataWidth(resetHorizontalScroll: true));

    private static void OnGridGeometryChanged(DependencyObject owner, DependencyPropertyChangedEventArgs e)
    {
        if (owner is HexViewControl control)
        {
            control.UpdateHexDataWidth(resetHorizontalScroll: true);
            control.UpdateAsciiColumnWidth();
        }
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

    private void UpdateAddressColumnWidth()
    {
        var dpi = VisualTreeHelper.GetDpi(this).PixelsPerDip;
        var text = new FormattedText("FFFFFFFF", System.Globalization.CultureInfo.InvariantCulture,
            FlowDirection.LeftToRight, new Typeface(HexGrid.FontFamily, FontStyles.Normal, FontWeights.Normal, FontStretches.Normal),
            HexGrid.FontSize, Foreground, dpi);
        SetValue(AddressColumnWidthProperty, text.Width + 12);
        if (HexGrid.Columns.Count > 0) HexGrid.Columns[0].Width = new DataGridLength(AddressColumnWidth);
        InvalidateMeasure();
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
        for (var index = 0; index < count; index++)
        {
            this[index].Row = rows[start + index];
            this[index].RowIndex = start + index;
        }
    }
}

public sealed class WindowedRowItem(HexViewRow row) : System.ComponentModel.INotifyPropertyChanged
{
    private HexViewRow _row = row;
    public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;
    public int RowIndex { get; internal set; }
    private bool _isSelected;
    public bool IsSelected { get => _isSelected; set { if (_isSelected == value) return; _isSelected = value; PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(nameof(IsSelected))); } }
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

public sealed class AddressRowSelectedEventArgs(int rowIndex, HexViewRow row) : EventArgs
{
    public int RowIndex { get; } = rowIndex;
    public HexViewRow Row { get; } = row;
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
