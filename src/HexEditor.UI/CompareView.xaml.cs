using System.Diagnostics;
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
    private ComparePanelSide _lastSelectedSide = ComparePanelSide.Base;

    public CompareView(CompareWorkspaceTabViewModel workspace)
    {
        InitializeComponent();
        Workspace = workspace ?? throw new ArgumentNullException(nameof(workspace));
        ViewModel = workspace.Comparison;
        RowWidths = [8, 16, 32];
        ViewModes = Enum.GetValues<CompareViewMode>();
        DataContext = this;
        FileAView.ItemsGrid.SelectionUnit = DataGridSelectionUnit.FullRow;
        FileBView.ItemsGrid.SelectionUnit = DataGridSelectionUnit.FullRow;
        FileAView.ItemsGrid.SelectionMode = DataGridSelectionMode.Single;
        FileBView.ItemsGrid.SelectionMode = DataGridSelectionMode.Single;
        FileAView.ItemsGrid.SelectionChanged += Panel_SelectionChanged;
        FileBView.ItemsGrid.SelectionChanged += Panel_SelectionChanged;
        FileAView.ItemsGrid.AddHandler(ScrollViewer.ScrollChangedEvent, new ScrollChangedEventHandler(Grid_ScrollChanged));
        FileBView.ItemsGrid.AddHandler(ScrollViewer.ScrollChangedEvent, new ScrollChangedEventHandler(Grid_ScrollChanged));
        FileAView.ItemsGrid.AddHandler(UIElement.PreviewMouseLeftButtonDownEvent,
            new MouseButtonEventHandler(Panel_PreviewMouseLeftButtonDown), handledEventsToo: true);
        FileBView.ItemsGrid.AddHandler(UIElement.PreviewMouseLeftButtonDownEvent,
            new MouseButtonEventHandler(Panel_PreviewMouseLeftButtonDown), handledEventsToo: true);
    }

    public CompareWorkspaceTabViewModel Workspace { get; }
    public CompareViewModel ViewModel { get; }
    public IReadOnlyList<int> SelectedBaseSegmentIndexes => FileABlocks.SelectedItems
        .Cast<CompareMemoryBlockRow>().Select(row => Workspace.BlocksA.IndexOf(row)).Where(index => index >= 0).ToArray();
    public int[] RowWidths { get; }
    public CompareViewMode[] ViewModes { get; }
    public event Action<uint, uint>? BaseRowActivated;
    public event Action<string>? StatusRequested;
    public event Action<string, Exception, ComparePanelSide, int, uint?>? InteractionFailed;

    public void RefreshImage(MemoryImage previousImage, MemoryImage updatedImage)
    {
        Workspace.RefreshImage(previousImage, updatedImage);
    }

    public bool NavigateToAddress(uint address)
    {
        if (!ViewModel.TryNavigateToAddress(address, out var rowIndex)) return false;
        Navigate(address);
        return rowIndex >= 0;
    }

    private void Panel_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is not DataGrid grid || e.ClickCount is not (1 or 2)) return;

        var side = ReferenceEquals(grid, FileAView.ItemsGrid) ? ComparePanelSide.Base : ComparePanelSide.Reference;
        var rowIndex = -1;
        uint? address = null;
        uint? rowStart = null;
        try
        {
            var source = e.OriginalSource as DependencyObject;
            var row = FindAncestor<DataGridRow>(source);
            var cell = FindAncestor<DataGridCell>(source);
            if (row?.Item is not HexViewRow viewRow || cell is null || viewRow.IsGapMarker ||
                !HexAddressParser.TryParse(viewRow.Address, out var rowStartAddress)) return;
            rowStart = rowStartAddress;
            rowIndex = row.GetIndex();
            if (!ReferenceEquals(cell.Column, grid.Columns[0]))
            {
                var byteRow = FindAncestor<HexByteRowControl>(source);
                if (byteRow is null || !ReferenceEquals(byteRow.Row, row.Item) ||
                    !byteRow.TryGetAddressAt(e.GetPosition(byteRow), out var hitAddress)) return;
                address = hitAddress;
            }

            _lastSelectedSide = side;
            if (e.ClickCount == 1)
            {
                // Byte clicks are handled by HexViewControl, which retains shift-range selection.
                if (address is not null) return;
                e.Handled = true;
                Navigate(rowStartAddress);
                return;
            }
            e.Handled = true;

            var activation = Workspace.ActivateRow(side, rowIndex, address);
            if (activation.Kind == CompareRowActivationKind.EditableRow && activation.Address is uint selected)
                BaseRowActivated?.Invoke(rowStartAddress, selected);
            else if (activation.StatusMessage is { } message)
                StatusRequested?.Invoke(message);
        }
        catch (Exception exception)
        {
            ReportInteractionFailure(e.ClickCount == 2 ? "double-click" : "single-click", side, rowIndex,
                address ?? rowStart, exception);
        }
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
        if (!HexAddressParser.TryParse(AddressBox.Text, out var address) || !ViewModel.TryNavigateToAddress(address, out var rowIndex))
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
        if (_syncingScroll || sender is not DataGrid grid || grid.SelectedItem is not CompareMemoryBlockRow block) return;
        ViewModel.ViewMode = CompareViewMode.AllRegions;
        Navigate(block.StartAddress);
    }

    private void PreviousDifference_Click(object sender, RoutedEventArgs e)
    {
        NavigateDifference(forward: false);
    }

    private void NavigateDifference(bool forward)
    {
        uint? context = null;
        try
        {
            var selected = SelectedAddress();
            var hasAddressText = !string.IsNullOrWhiteSpace(AddressBox.Text);
            var hasValidAnchor = HexAddressParser.TryParse(AddressBox.Text, out var parsedAddress);
            if (selected is null && hasAddressText && !hasValidAnchor)
            {
                StatusRequested?.Invoke("Enter a valid hexadecimal address to navigate differences.");
                return;
            }
            uint? anchor = hasValidAnchor ? parsedAddress : null;
            context = selected ?? anchor;
            var found = forward
                ? ViewModel.TryFindNextDifferenceFromAddress(selected, anchor, out var address)
                : ViewModel.TryFindPreviousDifferenceFromAddress(selected, anchor, out address);
            if (found)
            {
                Navigate(address);
                return;
            }

            StatusRequested?.Invoke(forward ? "No later differences were found." : "No earlier differences were found.");
        }
        catch (Exception exception)
        {
            ReportInteractionFailure(forward ? "Next Difference" : "Previous Difference",
                _lastSelectedSide, -1, context, exception);
        }
    }

    private uint? SelectedAddress()
    {
        if (FileAView.ItemsGrid.SelectedItem is HexViewRow row && HexAddressParser.TryParse(row.Address, out var rowAddress))
        {
            if (HexAddressParser.TryParse(AddressBox.Text, out var byteAddress) &&
                (ulong)byteAddress >= rowAddress && (ulong)byteAddress < (ulong)rowAddress + (uint)ViewModel.BytesPerRow)
                return byteAddress;
            return rowAddress;
        }
        return null;
    }

    private void Navigate(uint address)
    {
        AddressBox.Text = address.ToString("X8");
        if (ViewModel.TryNavigateToAddress(address, out var rowIndex)) SelectRow(rowIndex);
    }

    private void SelectRow(int index)
    {
        if ((uint)index >= (uint)ViewModel.FileARows.Count) return;
        _syncingScroll = true;
        try
        {
            FileAView.ItemsGrid.SelectedIndex = index;
            FileBView.ItemsGrid.SelectedIndex = index;
            FileAView.ItemsGrid.ScrollIntoView(FileAView.ItemsGrid.Items[index]);
            FileBView.ItemsGrid.ScrollIntoView(FileBView.ItemsGrid.Items[index]);
        }
        finally { _syncingScroll = false; }
    }

    private void Panel_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_syncingScroll || sender is not DataGrid source || source.SelectedItem is not HexViewRow) return;
        var side = ReferenceEquals(source, FileAView.ItemsGrid) ? ComparePanelSide.Base : ComparePanelSide.Reference;
        try
        {
            _lastSelectedSide = side;
            _syncingScroll = true;
            var target = side == ComparePanelSide.Base ? FileBView.ItemsGrid : FileAView.ItemsGrid;
            if ((uint)source.SelectedIndex < (uint)target.Items.Count)
            {
                var targetRow = target.Items[source.SelectedIndex];
                target.SelectedIndex = source.SelectedIndex;
                target.ScrollIntoView(targetRow);
                if (source.SelectedItem is HexViewRow row && HexAddressParser.TryParse(row.Address, out var address))
                    AddressBox.Text = address.ToString("X8");
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
        var side = ReferenceEquals(sender, FileAView) ? ComparePanelSide.Base : ComparePanelSide.Reference;
        _lastSelectedSide = side;
        try { Navigate(e.EndAddress); }
        catch (Exception exception) { ReportInteractionFailure("single-click", side, -1, e.EndAddress, exception); }
    }

    private void Grid_ScrollChanged(object sender, ScrollChangedEventArgs e)
    {
        if (_syncingScroll || sender is not DataGrid source || e.VerticalChange == 0) return;
        var side = ReferenceEquals(source, FileAView.ItemsGrid) ? ComparePanelSide.Base : ComparePanelSide.Reference;
        try
        {
            var target = side == ComparePanelSide.Base ? FileBView.ItemsGrid : FileAView.ItemsGrid;
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
