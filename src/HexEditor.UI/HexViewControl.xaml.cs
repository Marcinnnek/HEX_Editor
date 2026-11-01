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
using System.Windows.Threading;
using System.ComponentModel;
using HexEditor.Compare;

namespace HexEditor.UI;

public partial class HexViewControl : UserControl
{
    private uint? _anchorAddress;
    private SelectionTextFormat _selectionTextFormat = SelectionTextFormat.Hex;
    private uint? _byteDragAnchor;
    private uint? _byteDragEndpoint;
    private bool _byteDragActive;
    private int _byteDragScrollDirection;
    private double _byteDragPointerX;
    private readonly DispatcherTimer _byteDragScrollTimer;
    private bool _refreshingWindow;
    private bool _draggingRowThumb;
    private bool _hasRefreshedDuringDrag;
    private int? _pendingDragRow;
    private readonly DispatcherTimer _dragRefreshTimer;
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
        _dragRefreshTimer = new DispatcherTimer(DispatcherPriority.Render, Dispatcher)
        {
            Interval = TimeSpan.FromMilliseconds(200)
        };
        _dragRefreshTimer.Tick += (_, _) => ApplyPendingDragRow();
        _byteDragScrollTimer = new DispatcherTimer(DispatcherPriority.Input, Dispatcher)
        {
            Interval = TimeSpan.FromMilliseconds(80)
        };
        _byteDragScrollTimer.Tick += (_, _) => AutoScrollByteDrag();
        AddHandler(MouseMoveEvent, new MouseEventHandler(ByteDrag_MouseMove), handledEventsToo: true);
        AddHandler(MouseLeftButtonUpEvent, new MouseButtonEventHandler(ByteDrag_MouseLeftButtonUp), handledEventsToo: true);
        LostMouseCapture += (_, _) => EndByteDrag();
        RowThumb.AddHandler(Thumb.DragStartedEvent,
            new DragStartedEventHandler(RowThumb_DragStarted), handledEventsToo: true);
        RowThumb.AddHandler(Thumb.DragCompletedEvent,
            new DragCompletedEventHandler(RowThumb_DragCompleted), handledEventsToo: true);
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

    public static readonly DependencyProperty SelectionStartProperty = DependencyProperty.Register(
        nameof(SelectionStart), typeof(uint?), typeof(HexViewControl), new PropertyMetadata(null));
    public static readonly DependencyProperty SelectionEndProperty = DependencyProperty.Register(
        nameof(SelectionEnd), typeof(uint?), typeof(HexViewControl), new PropertyMetadata(null));

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
    public uint? SelectionStart { get => (uint?)GetValue(SelectionStartProperty); private set => SetValue(SelectionStartProperty, value); }
    public uint? SelectionEnd { get => (uint?)GetValue(SelectionEndProperty); private set => SetValue(SelectionEndProperty, value); }
    public SelectionTextFormat ClipboardFormat => _selectionTextFormat;
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
        if (rowIndex >= 0) EndByteDrag();
        _anchorAddress = null;
        SelectionStart = SelectionEnd = null;
        if (rowIndex >= 0) _selectionTextFormat = SelectionTextFormat.Hex;
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
        var minimumWidth = MeasureColumnTextWidth("ASCII");
        var width = Math.Max(BytesPerRow * sample.WidthIncludingTrailingWhitespace + 12, minimumWidth);
        AsciiColumn.MinWidth = minimumWidth;
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
        control.EndByteDrag();
        control.CancelPendingDrag();
        if (e.OldValue is INotifyCollectionChanged previous)
            previous.CollectionChanged -= control.ItemsSource_CollectionChanged;

        var nextStart = 0;
        if (e.OldValue is SparseRowList<HexViewRow> oldRows &&
            e.NewValue is SparseRowList<HexViewRow> newRows &&
            oldRows.SourceIdentity is not null &&
            ReferenceEquals(oldRows.SourceIdentity, newRows.SourceIdentity))
        {
            if (control.RowThumb.Maximum > 0 && control._windowStart >= control.RowThumb.Maximum)
            {
                nextStart = Math.Max(0, newRows.Count - control._visibleRowCount);
            }
            else if ((uint)control._windowStart < (uint)oldRows.Count)
            {
                var oldRow = oldRows[control._windowStart];
                if (!oldRow.IsGapMarker && HexAddressParser.TryParse(oldRow.Address, out var address) &&
                    newRows.TryGetRowIndex(address, out var rowIndex))
                    nextStart = rowIndex;
                else
                    nextStart = (int)((long)control._windowStart * newRows.Count / oldRows.Count);
            }
        }

        if (e.NewValue is INotifyCollectionChanged current)
            current.CollectionChanged += control.ItemsSource_CollectionChanged;
        control.HexGrid.ItemsSource = control.UseWindowedScrolling ? null : e.NewValue as IEnumerable;
        control._windowStart = nextStart;
        control.RefreshWindow();
    }

    private void ItemsSource_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (UseWindowedScrolling) RefreshWindow();
    }

    private static void OnWindowModeChanged(DependencyObject owner, DependencyPropertyChangedEventArgs e)
    {
        if (owner is not HexViewControl control) return;
        control.EndByteDrag();
        control.CancelPendingDrag();
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
        CancelPendingDrag();
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
        var wasAtEnd = RowThumb.Maximum > 0 && _windowStart >= RowThumb.Maximum;
        _visibleRowCount = GetVisibleRowCount();
        var maxStart = Math.Max(0, rows.Count - _visibleRowCount);
        _windowStart = wasAtEnd ? maxStart : Math.Clamp(_windowStart, 0, maxStart);
        _refreshingWindow = true;
        RowThumb.Maximum = maxStart;
        RowThumb.ViewportSize = GetVisibleThumbViewport(maxStart);
        RowThumb.Value = _windowStart;
        _refreshingWindow = false;
        WindowRows.ReplaceAll(rows, _windowStart, Math.Min(rows.Count, _windowStart + _visibleRowCount));
        foreach (var item in WindowRows) item.IsSelected = item.RowIndex == _selectedRowIndex;
    }

    private int GetVisibleRowCount()
    {
        // CanContentScroll reports logical units through ScrollViewer.ViewportHeight;
        // the presenter gives the physical height available to the rendered rows.
        var presenter = WindowedScroll.Template?.FindName("PART_ScrollContentPresenter", WindowedScroll)
            as ScrollContentPresenter;
        var viewportHeight = presenter is { ActualHeight: > 0 }
            ? presenter.ActualHeight
            : WindowedView.ActualHeight;
        var headerHeight = WindowedRows.TranslatePoint(new Point(), WindowedScroll).Y;
        if (headerHeight <= 0) headerHeight = 25;

        var firstRow = WindowedRows.ItemContainerGenerator.ContainerFromIndex(0) as FrameworkElement;
        var rowHeight = firstRow?.ActualHeight > 0 ? firstRow.ActualHeight : 0;
        if (rowHeight <= 0)
        {
            var sample = new FormattedText("0", CultureInfo.InvariantCulture, FlowDirection.LeftToRight,
                new Typeface(WindowedScroll.FontFamily, WindowedScroll.FontStyle,
                    WindowedScroll.FontWeight, WindowedScroll.FontStretch), WindowedScroll.FontSize,
                Foreground, VisualTreeHelper.GetDpi(this).PixelsPerDip);
            rowHeight = Math.Max(18, sample.Height + GridSpacingSettings.Current.VerticalRowSpacing);
        }

        return Math.Max(1, (int)Math.Floor(Math.Max(1, viewportHeight - headerHeight) / rowHeight));
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
        _pendingDragRow = (int)Math.Round(e.NewValue);
        if (_draggingRowThumb && _hasRefreshedDuringDrag &&
            _pendingDragRow is > 0 && _pendingDragRow < RowThumb.Maximum)
        {
            // Redraw when the pointer pauses; continuous drag events only move the thumb.
            _dragRefreshTimer.Stop();
            _dragRefreshTimer.Start();
            return;
        }
        ApplyPendingDragRow();
    }

    private void RowThumb_DragStarted(object sender, DragStartedEventArgs e)
    {
        _draggingRowThumb = true;
        _hasRefreshedDuringDrag = false;
        _pendingDragRow = null;
        _dragRefreshTimer.Stop();
    }

    private void RowThumb_DragCompleted(object sender, DragCompletedEventArgs e)
    {
        _draggingRowThumb = false;
        ApplyPendingDragRow();
    }

    private void CancelPendingDrag()
    {
        _dragRefreshTimer.Stop();
        _draggingRowThumb = false;
        _pendingDragRow = null;
    }

    private void ApplyPendingDragRow()
    {
        _dragRefreshTimer.Stop();
        if (_pendingDragRow is not int rowIndex) return;
        _pendingDragRow = null;
        _hasRefreshedDuringDrag = true;
        if (_windowStart == rowIndex) return;
        var previousStart = _windowStart;
        _windowStart = rowIndex;
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

    private void Control_Unloaded(object sender, RoutedEventArgs e)
    {
        GridSpacingSettings.SpacingChanged -= SpacingChanged;
        CancelPendingDrag();
        EndByteDrag();
    }

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
        if (BytesPerRow is < 1 or > 128) return;

        var sample = new FormattedText(
            "0",
            CultureInfo.InvariantCulture,
            FlowDirection.LeftToRight,
            new Typeface(
                HexGrid.FontFamily,
                HexGrid.FontStyle,
                HexGrid.FontWeight,
                HexGrid.FontStretch),
            HexGrid.FontSize,
            Brushes.Black,
            VisualTreeHelper.GetDpi(this).PixelsPerDip);

        var contentWidth =
            BytesPerRow * 2 * sample.WidthIncludingTrailingWhitespace +
            (BytesPerRow - 1) * GridSpacingSettings.Current.HorizontalByteSpacing;

        const double leftMargin = 6;
        const double rightMargin = 6;
        var minimumWidth = MeasureColumnTextWidth("HEX Data");
        var columnWidth = Math.Max(contentWidth + leftMargin + rightMargin, minimumWidth);

        SetValue(HexDataContentWidthPropertyKey, contentWidth);
        SetValue(HexDataColumnWidthPropertyKey, columnWidth);

        HexDataColumn.MinWidth = minimumWidth;
        HexDataColumn.Width = new DataGridLength(columnWidth);

        WindowedView.InvalidateMeasure();

        if (resetHorizontalScroll)
            ResetHorizontalScroll();
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

    private double MeasureColumnTextWidth(string text)
    {
        var formatted = new FormattedText(text, CultureInfo.InvariantCulture, FlowDirection.LeftToRight,
            new Typeface(HexGrid.FontFamily, HexGrid.FontStyle, FontWeights.Bold, HexGrid.FontStretch),
            HexGrid.FontSize, Foreground, VisualTreeHelper.GetDpi(this).PixelsPerDip);
        return formatted.WidthIncludingTrailingWhitespace + 12;
    }

    private void ByteCell_Click(object sender, MouseButtonEventArgs e)
    {
        if (sender is not HexByteRowControl row) return;
        var point = e.GetPosition(row);
        e.Handled = SelectByteAt(row, point, Keyboard.Modifiers.HasFlag(ModifierKeys.Shift));
        if (e.Handled && e.ClickCount == 1 && row.TryGetAddressAt(point, out var hitAddress))
            BeginByteDrag(hitAddress);
        if (e.ClickCount >= 2)
            RaiseWindowedByteDoubleClick(row, e, point);
    }

    private void BeginByteDrag(uint address)
    {
        EndByteDrag();
        _byteDragAnchor = _anchorAddress ?? address;
        _byteDragEndpoint = address;
        _byteDragActive = Mouse.Capture(this, CaptureMode.SubTree);
    }

    private void ByteDrag_MouseMove(object sender, MouseEventArgs e)
    {
        if (!_byteDragActive) return;
        if (e.LeftButton != MouseButtonState.Pressed) { EndByteDrag(); return; }

        var point = e.GetPosition(this);
        _byteDragPointerX = point.X;
        if (point.X >= 0 && point.X < ActualWidth && point.Y >= 0 && point.Y < ActualHeight &&
            VisualTreeHelper.HitTest(this, point)?.VisualHit is DependencyObject hit &&
            FindAncestor<HexByteRowControl>(hit) is { } row &&
            row.TryGetAddressAt(e.GetPosition(row), out var address))
            UpdateByteDragSelection(address);

        var scroll = UseWindowedScrolling ? (FrameworkElement)WindowedScroll : HexGrid;
        var scrollPoint = e.GetPosition(scroll);
        var top = UseWindowedScrolling
            ? WindowedRows.TranslatePoint(new Point(), WindowedScroll).Y
            : 25;
        _byteDragScrollDirection = point.X < 0 || point.X >= ActualWidth ? 0
            : scrollPoint.Y < top ? -1
            : scrollPoint.Y >= scroll.ActualHeight ? 1 : 0;
        if (_byteDragScrollDirection == 0) _byteDragScrollTimer.Stop();
        else if (!_byteDragScrollTimer.IsEnabled) _byteDragScrollTimer.Start();
    }

    private void ByteDrag_MouseLeftButtonUp(object sender, MouseButtonEventArgs e) => EndByteDrag();

    private void AutoScrollByteDrag()
    {
        if (!_byteDragActive || Mouse.LeftButton != MouseButtonState.Pressed || _byteDragScrollDirection == 0)
        {
            EndByteDrag();
            return;
        }
        if (UseWindowedScrolling)
            SetWindowStart(_windowStart + _byteDragScrollDirection);
        else
        {
            var viewer = FindScrollViewer(HexGrid);
            if (_byteDragScrollDirection < 0) viewer?.LineUp();
            else viewer?.LineDown();
        }
        UpdateLayout();
        if (TryGetVisibleEdgeByte(_byteDragScrollDirection > 0, out var address))
            UpdateByteDragSelection(address);
    }

    private bool TryGetVisibleEdgeByte(bool last, out uint address)
    {
        address = default;
        var viewport = UseWindowedScrolling ? (FrameworkElement)WindowedScroll : HexGrid;
        var viewportTop = viewport.TranslatePoint(new Point(), this).Y;
        var viewportBottom = viewportTop + viewport.ActualHeight;
        HexByteRowControl? edge = null;
        var edgeY = last ? double.NegativeInfinity : double.PositiveInfinity;
        foreach (var row in DescendantByteRows(this))
        {
            if (!row.IsVisible || row.Row is null) continue;
            var origin = row.TranslatePoint(new Point(), this);
            var x = _byteDragPointerX - origin.X;
            if (x < 0 || x >= row.ActualWidth || origin.Y + row.ActualHeight <= viewportTop ||
                origin.Y >= viewportBottom) continue;
            if (last ? origin.Y > edgeY : origin.Y < edgeY)
            {
                edge = row;
                edgeY = origin.Y;
            }
        }
        if (edge is null) return false;
        var point = new Point(_byteDragPointerX - edge.TranslatePoint(new Point(), this).X, 2);
        return edge.TryGetAddressAt(point, out address);
    }

    private static IEnumerable<HexByteRowControl> DescendantByteRows(DependencyObject root)
    {
        for (var index = 0; index < VisualTreeHelper.GetChildrenCount(root); index++)
        {
            var child = VisualTreeHelper.GetChild(root, index);
            if (child is HexByteRowControl row) yield return row;
            else foreach (var descendant in DescendantByteRows(child)) yield return descendant;
        }
    }

    private void UpdateByteDragSelection(uint endpoint)
    {
        if (!_byteDragActive || _byteDragAnchor is not uint anchor || _byteDragEndpoint == endpoint) return;
        _byteDragEndpoint = endpoint;
        var format = _selectionTextFormat;
        var start = Math.Min(anchor, endpoint);
        var end = Math.Max(anchor, endpoint);
        AddressRangeSelected?.Invoke(this, new AddressRangeSelectedEventArgs(start, end));
        _anchorAddress = anchor;
        _selectionTextFormat = format;
        SelectionStart = start;
        SelectionEnd = end;
    }

    private void EndByteDrag()
    {
        _byteDragActive = false;
        _byteDragAnchor = null;
        _byteDragEndpoint = null;
        _byteDragScrollDirection = 0;
        _byteDragScrollTimer.Stop();
        if (Mouse.Captured == this) Mouse.Capture(null);
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
            var format = extendSelection && _anchorAddress is not null
                ? _selectionTextFormat
                : row.IsAscii ? SelectionTextFormat.Ascii : SelectionTextFormat.Hex;
            var start = Math.Min(anchor, hitAddress);
            var end = Math.Max(anchor, hitAddress);
            AddressRangeSelected?.Invoke(this, new AddressRangeSelectedEventArgs(start, end));
            _anchorAddress = anchor;
            _selectionTextFormat = format;
            SelectionStart = start;
            SelectionEnd = end;
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
