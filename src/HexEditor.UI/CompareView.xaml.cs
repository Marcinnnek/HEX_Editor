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

    public CompareView(CompareWorkspaceTabViewModel workspace)
    {
        InitializeComponent();
        Workspace = workspace ?? throw new ArgumentNullException(nameof(workspace));
        ViewModel = workspace.Comparison;
        RowWidths = [8, 16, 32];
        ViewModes = Enum.GetValues<CompareViewMode>();
        DataContext = this;
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
    public event Action<Exception, ComparePanelSide, int, uint?>? ActivationFailed;

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
        if (e.ClickCount != 2 || sender is not DataGrid grid) return;
        e.Handled = true;

        var side = ReferenceEquals(grid, FileAView.ItemsGrid) ? ComparePanelSide.Base : ComparePanelSide.Reference;
        var rowIndex = -1;
        uint? address = null;
        try
        {
            uint? rowStartAddress = null;
            if (side == ComparePanelSide.Base)
            {
                var source = e.OriginalSource as DependencyObject;
                var row = FindAncestor<DataGridRow>(source);
                var cell = FindAncestor<DataGridCell>(source);
                if (row?.Item is not HexViewRow || cell is null) return;
                rowIndex = row.GetIndex();
                if (!HexAddressParser.TryParse(((HexViewRow)row.Item).Address, out var parsedRowStart)) return;
                rowStartAddress = parsedRowStart;

                if (!ReferenceEquals(cell.Column, grid.Columns[0]))
                {
                    var byteRow = FindAncestor<HexByteRowControl>(source);
                    if (byteRow is null || !ReferenceEquals(byteRow.Row, row.Item) ||
                        !byteRow.TryGetAddressAt(e.GetPosition(byteRow), out var hitAddress)) return;
                    address = hitAddress;
                }
            }

            var activation = Workspace.ActivateRow(side, rowIndex, address);
            if (activation.Kind == CompareRowActivationKind.EditableRow && activation.Address is uint selected && rowStartAddress is uint rowStart)
                BaseRowActivated?.Invoke(rowStart, selected);
            else if (activation.StatusMessage is { } message)
                StatusRequested?.Invoke(message);
        }
        catch (Exception exception)
        {
            var context = address is uint selected ? $"0x{selected:X8}" : "unknown address";
            Trace.TraceError($"Compare row activation failed: side={side}, rowIndex={rowIndex}, address={context}, " +
                $"exception={exception.GetType().Name}, hresult=0x{exception.HResult:X8}");
            try { ActivationFailed?.Invoke(exception, side, rowIndex, address); }
            catch (Exception diagnosticException)
            {
                Trace.TraceError($"Compare row diagnostic callback failed: side={side}, rowIndex={rowIndex}, " +
                    $"exception={diagnosticException.GetType().Name}, hresult=0x{diagnosticException.HResult:X8}");
            }
            try { StatusRequested?.Invoke($"Unable to activate Compare row at {context}."); }
            catch (Exception notificationException)
            {
                Trace.TraceError($"Compare row failure notification failed: side={side}, address={context}, " +
                    $"exception={notificationException.GetType().Name}, hresult=0x{notificationException.HResult:X8}");
            }
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
        var selected = SelectedAddress();
        var hasAddressText = !string.IsNullOrWhiteSpace(AddressBox.Text);
        var hasValidAnchor = HexAddressParser.TryParse(AddressBox.Text, out var parsedAddress);
        if (selected is null && hasAddressText && !hasValidAnchor)
        {
            StatusRequested?.Invoke("Enter a valid hexadecimal address to navigate differences.");
            return;
        }
        uint? anchor = hasValidAnchor ? parsedAddress : null;
        try
        {
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
            var operation = forward ? "Next Difference" : "Previous Difference";
            var context = selected ?? anchor;
            var addressText = context is uint value ? $"0x{value:X8}" : "no address";
            Trace.TraceError($"Compare {operation} navigation failed from {addressText}: " +
                $"exception={exception.GetType().Name}, hresult=0x{exception.HResult:X8}");
            try { StatusRequested?.Invoke($"Unable to navigate to the {operation.ToLowerInvariant()}."); }
            catch (Exception notificationException)
            {
                Trace.TraceError($"Compare {operation} status notification failed: " +
                    $"exception={notificationException.GetType().Name}, hresult=0x{notificationException.HResult:X8}");
            }
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
        FileAView.ItemsGrid.SelectedIndex = index;
        FileBView.ItemsGrid.SelectedIndex = index;
        FileAView.ItemsGrid.ScrollIntoView(FileAView.ItemsGrid.Items[index]);
        FileBView.ItemsGrid.ScrollIntoView(FileBView.ItemsGrid.Items[index]);
        _syncingScroll = false;
    }

    private void Panel_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_syncingScroll || sender is not DataGrid source || source.SelectedItem is not HexViewRow) return;
        _syncingScroll = true;
        var target = ReferenceEquals(source, FileAView.ItemsGrid) ? FileBView.ItemsGrid : FileAView.ItemsGrid;
        if ((uint)source.SelectedIndex < (uint)target.Items.Count)
        {
            var targetRow = target.Items[source.SelectedIndex];
            target.SelectedIndex = source.SelectedIndex;
            target.ScrollIntoView(targetRow);
        }
        _syncingScroll = false;
    }

    private void ByteAddressSelected(object? sender, AddressRangeSelectedEventArgs e)
    {
        Navigate(e.EndAddress);
    }

    private void Grid_ScrollChanged(object sender, ScrollChangedEventArgs e)
    {
        if (_syncingScroll || sender is not DataGrid source || e.VerticalChange == 0) return;
        var target = ReferenceEquals(source, FileAView.ItemsGrid) ? FileBView.ItemsGrid : FileAView.ItemsGrid;
        var sourceScroll = FindScrollViewer(source);
        var targetScroll = FindScrollViewer(target);
        if (sourceScroll is null || targetScroll is null) return;
        _syncingScroll = true;
        targetScroll.ScrollToVerticalOffset(sourceScroll.VerticalOffset);
        _syncingScroll = false;
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
