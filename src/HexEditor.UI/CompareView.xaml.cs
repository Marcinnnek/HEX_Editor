using System.Diagnostics;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using HexEditor.Core;
using HexEditor.Compare;
using MessageBox = HexEditor.UI.ThemedMessageBox;

namespace HexEditor.UI;

public partial class CompareView : UserControl
{
    private bool _syncingScroll;
    private readonly CompareNavigationSession _navigation;

    public CompareView(CompareWorkspaceTabViewModel workspace)
    {
        InitializeComponent();
        CommandBindings.Add(new CommandBinding(ApplicationCommands.Copy, CopySelection_Executed, CopySelection_CanExecute));
        Workspace = workspace ?? throw new ArgumentNullException(nameof(workspace));
        ViewModel = workspace.Comparison;
        _navigation = new CompareNavigationSession(ViewModel);
        AddressBox.TextChanged += (_, _) => _navigation.SetAddressDraft(AddressBox.Text);
        ViewModes = Enum.GetValues<CompareViewMode>();
        DataContext = this;
        ViewModel.BytesPerRowChanging += ViewModel_BytesPerRowChanging;
        ViewModel.PropertyChanged += ViewModel_PropertyChanged;
        FileAView.UseWindowedScrolling = true;
        FileBView.UseWindowedScrolling = true;
        ConfigurePanel(FileAView);
        ConfigurePanel(FileBView);
        FileAView.PreviewMouseRightButtonDown += (_, _) => _navigation.FocusSide(ComparePanelSide.Base);
        ConfigureBaseContextMenu();
    }

    public CompareWorkspaceTabViewModel Workspace { get; }
    public CompareViewModel ViewModel { get; }
    public ComparePanelSide SelectedSide => _navigation.Side;
    public (ComparePanelSide Side, uint Address)? CurrentFindCursor =>
        _navigation.CurrentCursor(VisibleRowAddress(), ViewModel.BytesPerRow, FileAView.UseWindowedScrolling);
    public IReadOnlyList<int> SelectedBaseSegmentIndexes => FileABlocks.SelectedItems
        .Cast<CompareMemoryBlockRow>().Select(row => Workspace.BlocksA.IndexOf(row)).Where(index => index >= 0).ToArray();
    public CompareViewMode[] ViewModes { get; }
    public event Action<uint, uint>? BaseRowActivated;
    public event EventHandler? BaseFillWithPatternRequested;
    public event EventHandler? BasePasteClipboardRequested;
    public event EventHandler? BaseDeleteRequested;
    public event Action<string>? StatusRequested;
    public event Action<string, Exception, ComparePanelSide, int, uint?>? InteractionFailed;

    private void ConfigureBaseContextMenu()
    {
        var menu = new ContextMenu();
        var fill = new MenuItem { Header = "Fill with Pattern..." };
        var paste = new MenuItem { Header = "Paste clipboard bytes" };
        var delete = new MenuItem { Header = "Delete selected bytes" };
        fill.Click += (_, _) => BaseFillWithPatternRequested?.Invoke(this, EventArgs.Empty);
        paste.Click += (_, _) => BasePasteClipboardRequested?.Invoke(this, EventArgs.Empty);
        delete.Click += (_, _) => BaseDeleteRequested?.Invoke(this, EventArgs.Empty);
        foreach (var item in new[] { fill, paste, delete })
        {
            item.Style = (Style)FindResource("HexContextMenuItemStyle");
            menu.Items.Add(item);
        }
        menu.Opened += (_, _) =>
        {
            var selected = FileAView.SelectionStart is not null && FileAView.SelectionEnd is not null;
            fill.IsEnabled = selected;
            paste.IsEnabled = selected;
            delete.IsEnabled = selected;
        };
        FileAView.ContextMenu = menu;
    }

    private HexViewControl SelectedPanel => _navigation.Side == ComparePanelSide.Base ? FileAView : FileBView;

    private void CopySelection_CanExecute(object sender, CanExecuteRoutedEventArgs e)
    {
        var panel = SelectedPanel;
        e.CanExecute = panel.SelectionStart is not null && panel.SelectionEnd is not null;
        e.Handled = true;
    }

    private void CopySelection_Executed(object sender, ExecutedRoutedEventArgs e)
    {
        var panel = SelectedPanel;
        var image = _navigation.Side == ComparePanelSide.Base ? ViewModel.FileA : ViewModel.FileB;
        SelectionClipboardText.TryCopy(image, panel.SelectionStart, panel.SelectionEnd, panel.ClipboardFormat);
        e.Handled = true;
    }

    private void ConfigurePanel(HexViewControl panel)
    {
        var grid = panel.ItemsGrid;
        grid.SelectionUnit = DataGridSelectionUnit.FullRow;
        grid.SelectionMode = DataGridSelectionMode.Single;
        grid.SelectionChanged += Panel_SelectionChanged;
        panel.ByteClickFailed += Panel_ByteClickFailed;
        panel.AddressRowSelected += Panel_AddressRowSelected;
        panel.WindowedByteDoubleClicked += Panel_WindowedByteDoubleClicked;
        panel.FirstVisibleRowChanged += Panel_FirstVisibleRowChanged;
        grid.AddHandler(ScrollViewer.ScrollChangedEvent, new ScrollChangedEventHandler(Grid_ScrollChanged));
        grid.AddHandler(UIElement.PreviewMouseLeftButtonDownEvent,
            new MouseButtonEventHandler(Panel_PreviewMouseLeftButtonDown), handledEventsToo: true);
    }

    private void Panel_AddressRowSelected(object? sender, AddressRowSelectedEventArgs e)
    {
        if (sender is not HexViewControl panel) return;
        var rowAddress = HexAddressParser.TryParse(e.Row.Address, out var address) ? address : (uint?)null;
        _navigation.SelectAddressRow(SideOf(panel), e.Row.IsGapMarker ? null : rowAddress);
        SelectAddressRow(e.RowIndex);
        AddressBox.Text = _navigation.AddressText;
    }

    private ComparePanelSide SideOf(object panel) => ReferenceEquals(panel, FileAView) ||
        ReferenceEquals(panel, FileAView.ItemsGrid) ? ComparePanelSide.Base : ComparePanelSide.Reference;

    private DataGrid OppositeGrid(ComparePanelSide side) => side == ComparePanelSide.Base
        ? FileBView.ItemsGrid : FileAView.ItemsGrid;

    private void Panel_ByteClickFailed(object? sender, ByteClickFailedEventArgs e)
    {
        if (sender is not HexViewControl panel) return;
        ReportInteractionFailure("single-click", SideOf(panel), panel.ItemsGrid.SelectedIndex,
            e.Address, e.Exception);
    }

    public void RefreshImage(MemoryImage previousImage, MemoryImage updatedImage)
    {
        Workspace.RefreshImage(previousImage, updatedImage);
    }

    public bool NavigateToAddress(uint address)
    {
        if (!_navigation.TryNavigate(address, out var rowIndex)) return false;
        ApplyNavigation(rowIndex);
        return true;
    }

    public void NavigateToFindMatch(ComparePanelSide side, uint start, uint end)
    {
        var found = _navigation.NavigateToFindMatch(side, start, end, out var rowIndex);
        AddressBox.Text = _navigation.AddressText;
        if (!found) return;
        SelectFindMatchRow(rowIndex);
    }

    private void Panel_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is not DataGrid grid || e.ClickCount is not (1 or 2)) return;

        var side = SideOf(grid);
        var rowIndex = -1;
        uint? address = null;
        uint? rowStart = null;
        try
        {
            var source = e.OriginalSource as DependencyObject;
            var row = FindAncestor<DataGridRow>(source);
            var cell = FindAncestor<DataGridCell>(source);
            if (row?.Item is not HexViewRow viewRow || cell is null) return;
            rowIndex = row.GetIndex();
            var isAddressCell = ReferenceEquals(cell.Column, grid.Columns[0]);
            var hasAddress = HexAddressParser.TryParse(viewRow.Address, out var rowStartAddress);
            if (hasAddress) rowStart = rowStartAddress;
            if (!isAddressCell)
            {
                if (viewRow.IsGapMarker) return;
                var byteRow = FindAncestor<HexByteRowControl>(source);
                if (byteRow is null || !ReferenceEquals(byteRow.Row, row.Item) ||
                    !byteRow.TryGetAddressAt(e.GetPosition(byteRow), out var hitAddress)) return;
                address = hitAddress;
            }

            _navigation.FocusSide(side);
            if (e.ClickCount == 1)
            {
                // Byte clicks are handled by HexViewControl, which retains shift-range selection.
                if (address is not null) return;
                e.Handled = true;
                return;
            }
            if (viewRow.IsGapMarker || !hasAddress) return;
            e.Handled = true;

            ActivateSelectedRow(side, rowIndex, rowStartAddress, address);
        }
        catch (Exception exception)
        {
            ReportInteractionFailure(e.ClickCount == 2 ? "double-click" : "single-click", side, rowIndex,
                address ?? rowStart, exception);
        }
    }

    private void SelectAddressRow(int rowIndex)
    {
        _syncingScroll = true;
        try
        {
            FileAView.SetSelectedRow(rowIndex);
            FileBView.SetSelectedRow(rowIndex);
            if (!FileAView.UseWindowedScrolling)
            {
                FileAView.ItemsGrid.SelectedIndex = -1;
                FileBView.ItemsGrid.SelectedIndex = -1;
            }
        }
        finally { _syncingScroll = false; }
    }

    private void Panel_WindowedByteDoubleClicked(object? sender, WindowedByteDoubleClickEventArgs e)
    {
        if (sender is not HexViewControl panel) return;
        var side = SideOf(panel);
        var rowIndex = -1;
        uint? address = null;
        try
        {
            if (e.Row.Row is not { IsGapMarker: false } row ||
                !HexAddressParser.TryParse(row.Address, out var rowStartAddress) ||
                !e.Row.TryGetAddressAt(e.Position, out var hitAddress) ||
                !ViewModel.TryNavigateToAddress(rowStartAddress, out rowIndex)) return;

            address = hitAddress;
            _navigation.FocusSide(side);
            e.MouseEvent.Handled = true;
            ActivateSelectedRow(side, rowIndex, rowStartAddress, hitAddress);
        }
        catch (Exception exception)
        {
            ReportInteractionFailure("double-click", side, rowIndex, address, exception);
        }
    }

    private void ActivateSelectedRow(ComparePanelSide side, int rowIndex, uint rowStartAddress, uint? hitAddress)
    {
        var activation = Workspace.ActivateRow(side, rowIndex, hitAddress);
        if (activation.Kind == CompareRowActivationKind.EditableRow && activation.Address is uint selected)
            BaseRowActivated?.Invoke(rowStartAddress, selected);
        else if (activation.StatusMessage is { } message)
            StatusRequested?.Invoke(message);
    }

    private static T? FindAncestor<T>(DependencyObject? source) where T : DependencyObject
    {
        for (var current = source; current is not null; current = current is Visual
            ? VisualTreeHelper.GetParent(current)
            : LogicalTreeHelper.GetParent(current))
        {
            if (current is T match) return match;
        }
        return null;
    }

    private void ApplyRange_Click(object sender, RoutedEventArgs e)
    {
        if (!HexAddressParser.TryParse(RangeStartBox.Text, out var start) || !HexAddressParser.TryParse(RangeEndBox.Text, out var end) || start > end)
        {
            MessageBox.Show(Window.GetWindow(this), "Enter a valid hexadecimal address range where Start is not greater than End.", "Invalid range", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        ViewModel.SetAddressRange(start, end);
        ViewModel.ViewMode = CompareViewMode.AddressRange;
    }

    private void Go_Click(object sender, RoutedEventArgs e)
    {
        if (!_navigation.TryGo(out var rowIndex))
        {
            MessageBox.Show(Window.GetWindow(this), "Enter an address in the displayed comparison range.", "Address not found", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        SelectRow(rowIndex);
    }

    private void NextDifference_Click(object sender, RoutedEventArgs e)
    {
        NavigateDifference(forward: true);
    }

    private void Block_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_syncingScroll || sender is not ListBox list || list.SelectedItem is not CompareMemoryBlockRow block) return;
        ViewModel.ViewMode = CompareViewMode.AllRegions;
        Navigate(block.StartAddress);
    }

    private void PreviousDifference_Click(object sender, RoutedEventArgs e)
    {
        NavigateDifference(forward: false);
    }

    private void ViewModel_BytesPerRowChanging()
    {
        _navigation.BeginRowWidthChange(VisibleRowAddress(), ViewModel.BytesPerRow, FileAView.UseWindowedScrolling);
    }

    private void ViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(CompareViewModel.BytesPerRow)) return;

        FileAView.UseWindowedScrolling = true;
        FileBView.UseWindowedScrolling = true;
        if (!_navigation.FinishRowWidthChange(out var rowIndex)) return;
        SelectRow(rowIndex);
    }

    private void NavigateDifference(bool forward)
    {
        uint? context = null;
        try
        {
            var visibleRowAddress = VisibleRowAddress();
            context = _navigation.DifferenceContext(visibleRowAddress, ViewModel.BytesPerRow, FileAView.UseWindowedScrolling);
            var result = _navigation.NavigateDifference(forward, visibleRowAddress, ViewModel.BytesPerRow,
                FileAView.UseWindowedScrolling);
            if (result.Kind == CompareDifferenceNavigationKind.InvalidAddress)
            {
                StatusRequested?.Invoke("Enter a valid hexadecimal address to navigate differences.");
                return;
            }
            if (result.Kind == CompareDifferenceNavigationKind.Found)
            {
                AddressBox.Text = _navigation.AddressText;
                ApplyNavigation(result.RowIndex);
                return;
            }

            StatusRequested?.Invoke(forward ? "No later differences were found." : "No earlier differences were found.");
        }
        catch (Exception exception)
        {
            ReportInteractionFailure(forward ? "Next Difference" : "Previous Difference",
                _navigation.Side, -1, context, exception);
        }
    }

    private uint? VisibleRowAddress()
    {
        var selected = FileAView.UseWindowedScrolling && FileAView.WindowRows.Count > 0
            ? FileAView.WindowRows[0].Row : FileAView.ItemsGrid.SelectedItem as HexViewRow;
        return selected is { } row && HexAddressParser.TryParse(row.Address, out var address) ? address : null;
    }

    private void Navigate(uint address)
    {
        var navigated = _navigation.Navigate(address, out var rowIndex);
        AddressBox.Text = _navigation.AddressText;
        ApplyNavigation(navigated ? rowIndex : -1);
    }

    private void ApplyNavigation(int rowIndex)
    {
        FileAView.SetSelectedRow(-1);
        FileBView.SetSelectedRow(-1);
        if (rowIndex >= 0) SelectRow(rowIndex);
    }

    private void SelectRow(int index)
    {
        if ((uint)index >= (uint)ViewModel.FileARows.Count) return;
        _syncingScroll = true;
        try
        {
            if (!FileAView.UseWindowedScrolling)
            {
                FileAView.ItemsGrid.SelectedIndex = index;
                FileBView.ItemsGrid.SelectedIndex = index;
            }
            FileAView.ScrollToRow(index);
            FileBView.ScrollToRow(index);
        }
        finally { _syncingScroll = false; }
    }

    private void SelectFindMatchRow(int index)
    {
        if ((uint)index >= (uint)ViewModel.FileARows.Count) return;
        _syncingScroll = true;
        try
        {
            if (!FileAView.UseWindowedScrolling)
            {
                FileAView.ItemsGrid.SelectedIndex = index;
                FileBView.ItemsGrid.SelectedIndex = index;
            }
            FileAView.ScrollToMatch(CompareFindScrollIndex(FileAView, index));
            FileBView.ScrollToMatch(CompareFindScrollIndex(FileBView, index));
        }
        finally { _syncingScroll = false; }
    }

    private int CompareFindScrollIndex(HexViewControl view, int matchIndex)
    {
        if (view.UseWindowedScrolling) return matchIndex;
        // The stock DataGrid exposes a one-row sliver before an exact logical item offset.
        // Offset the requested item so the match itself is the first rendered row.
        return Math.Min(matchIndex + 1, ViewModel.FileARows.Count - 1);
    }

    private void Panel_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_syncingScroll || sender is not DataGrid source || source.SelectedItem is not HexViewRow) return;
        var side = SideOf(source);
        try
        {
            _navigation.FocusSide(side);
            _syncingScroll = true;
            var target = OppositeGrid(side);
            if ((uint)source.SelectedIndex < (uint)target.Items.Count)
            {
                var targetRow = target.Items[source.SelectedIndex];
                target.SelectedIndex = source.SelectedIndex;
                target.ScrollIntoView(targetRow);
                if (source.SelectedItem is HexViewRow row && HexAddressParser.TryParse(row.Address, out var address))
                {
                    _navigation.ObserveGridRow(side, address);
                    AddressBox.Text = _navigation.AddressText;
                }
            }
        }
        catch (Exception exception)
        {
            var address = source.SelectedItem is HexViewRow row && HexAddressParser.TryParse(row.Address, out var parsed)
                ? parsed : (uint?)null;
            ReportInteractionFailure("selection", side, source.SelectedIndex, address, exception);
        }
        finally { _syncingScroll = false; }
    }

    private void ByteAddressSelected(object? sender, AddressRangeSelectedEventArgs e)
    {
        FileAView.SetSelectedRow(-1);
        FileBView.SetSelectedRow(-1);
        var side = SideOf(sender!);
        try
        {
            _navigation.SelectByte(side, e.EndAddress);
            AddressBox.Text = _navigation.AddressText;
        }
        catch (Exception exception) { ReportInteractionFailure("single-click", side, -1, e.EndAddress, exception); }
    }

    private void Grid_ScrollChanged(object sender, ScrollChangedEventArgs e)
    {
        if (_syncingScroll || sender is not DataGrid source || e.VerticalChange == 0) return;
        var side = SideOf(source);
        try
        {
            var target = OppositeGrid(side);
            var sourceScroll = FindScrollViewer(source);
            var targetScroll = FindScrollViewer(target);
            if (sourceScroll is null || targetScroll is null) return;
            _syncingScroll = true;
            targetScroll.ScrollToVerticalOffset(sourceScroll.VerticalOffset);
        }
        catch (Exception exception)
        {
            uint? address = HexAddressParser.TryParse(AddressBox.Text, out var parsed) ? parsed : null;
            ReportInteractionFailure("scroll", side, source.SelectedIndex, address, exception);
        }
        finally { _syncingScroll = false; }
    }

    private void Panel_FirstVisibleRowChanged(object? sender, EventArgs e)
    {
        if (_syncingScroll || sender is not HexViewControl source) return;
        try
        {
            var target = ReferenceEquals(source, FileAView) ? FileBView : FileAView;
            _syncingScroll = true;
            target.ScrollToRow(source.FirstVisibleRowIndex);
            var firstAddress = source.WindowRows.Count > 0 &&
                HexAddressParser.TryParse(source.WindowRows[0].Row.Address, out var address) ? address : (uint?)null;
            _navigation.ScrollToFirstRow(firstAddress);
            AddressBox.Text = _navigation.AddressText;
        }
        catch (Exception exception)
        {
            var side = SideOf(source);
            ReportInteractionFailure("scroll", side, source.FirstVisibleRowIndex,
                HexAddressParser.TryParse(AddressBox.Text, out var parsed) ? parsed : null, exception);
        }
        finally { _syncingScroll = false; }
    }

    private void ReportInteractionFailure(string operation, ComparePanelSide side, int rowIndex, uint? address, Exception exception)
    {
        var context = address is uint selected ? $"0x{selected:X8}" : $"row {rowIndex}";
        Trace.TraceError($"Compare {operation} failed: panel={side}, address={context}, " +
            $"exception={exception.GetType().Name}, hresult=0x{exception.HResult:X8}");
        try { InteractionFailed?.Invoke(operation, exception, side, rowIndex, address); }
        catch (Exception diagnosticException)
        {
            Trace.TraceError($"Compare {operation} diagnostic callback failed: panel={side}, address={context}, " +
                $"exception={diagnosticException.GetType().Name}, hresult=0x{diagnosticException.HResult:X8}");
        }
        try { StatusRequested?.Invoke($"Compare {side} {operation} failed at {context}."); }
        catch (Exception notificationException)
        {
            Trace.TraceError($"Compare {operation} status notification failed: panel={side}, address={context}, " +
                $"exception={notificationException.GetType().Name}, hresult=0x{notificationException.HResult:X8}");
        }
    }

    private static ScrollViewer? FindScrollViewer(DependencyObject parent)
    {
        for (var index = 0; index < VisualTreeHelper.GetChildrenCount(parent); index++)
        {
            var child = VisualTreeHelper.GetChild(parent, index);
            if (child is ScrollViewer viewer) return viewer;
            var nested = FindScrollViewer(child);
            if (nested is not null) return nested;
        }
        return null;
    }
}
