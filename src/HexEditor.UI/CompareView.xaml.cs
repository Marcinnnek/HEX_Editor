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
    private ComparePanelSide _lastSelectedSide = ComparePanelSide.Base;
    private uint? _selectedByteAddress;
    private uint? _rowWidthChangeAddress;
    private bool _rowWidthChangeHadByteSelection;
    private ComparePanelSide _rowWidthChangeSide;

    public CompareView(CompareWorkspaceTabViewModel workspace)
    {
        InitializeComponent();
        CommandBindings.Add(new CommandBinding(ApplicationCommands.Copy, CopySelection_Executed, CopySelection_CanExecute));
        Workspace = workspace ?? throw new ArgumentNullException(nameof(workspace));
        ViewModel = workspace.Comparison;
        ViewModes = Enum.GetValues<CompareViewMode>();
        DataContext = this;
        DataObject.AddPastingHandler(RangeStartBox, AddressInput_Pasting);
        DataObject.AddPastingHandler(RangeEndBox, AddressInput_Pasting);
        DataObject.AddPastingHandler(AddressBox, AddressInput_Pasting);
        _rowWidthChangeSide = _lastSelectedSide;
        ViewModel.BytesPerRowChanging += ViewModel_BytesPerRowChanging;
        ViewModel.PropertyChanged += ViewModel_PropertyChanged;
        FileAView.UseWindowedScrolling = true;
        FileBView.UseWindowedScrolling = true;
        ConfigurePanel(FileAView);
        ConfigurePanel(FileBView);
    }

    public CompareWorkspaceTabViewModel Workspace { get; }
    public CompareViewModel ViewModel { get; }
    public (ComparePanelSide Side, uint Address)? CurrentFindCursor => SelectedAddress() is uint address
        ? (_lastSelectedSide, address)
        : null;
    public IReadOnlyList<int> SelectedBaseSegmentIndexes => FileABlocks.SelectedItems
        .Cast<CompareMemoryBlockRow>().Select(row => Workspace.BlocksA.IndexOf(row)).Where(index => index >= 0).ToArray();
    public CompareViewMode[] ViewModes { get; }
    public event Action<uint, uint>? BaseRowActivated;
    public event Action<string>? StatusRequested;
    public event Action<string, Exception, ComparePanelSide, int, uint?>? InteractionFailed;

    private HexViewControl SelectedPanel => _lastSelectedSide == ComparePanelSide.Base ? FileAView : FileBView;

    private void CopySelection_CanExecute(object sender, CanExecuteRoutedEventArgs e)
    {
        var panel = SelectedPanel;
        e.CanExecute = panel.SelectionStart is not null && panel.SelectionEnd is not null;
        e.Handled = true;
    }

    private void CopySelection_Executed(object sender, ExecutedRoutedEventArgs e)
    {
        var panel = SelectedPanel;
        var image = _lastSelectedSide == ComparePanelSide.Base ? ViewModel.FileA : ViewModel.FileB;
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
        _lastSelectedSide = SideOf(panel);
        var rowAddress = HexAddressParser.TryParse(e.Row.Address, out var address) ? address : (uint?)null;
        SelectAddressRow(e.RowIndex, e.Row, rowAddress);
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
        if (!ViewModel.TryNavigateToAddress(address, out var rowIndex)) return false;
        Navigate(address);
        return rowIndex >= 0;
    }

    public void NavigateToFindMatch(ComparePanelSide side, uint start, uint end)
    {
        _lastSelectedSide = side;
        _selectedByteAddress = start;
        ViewModel.ClearFindMatch();
        ViewModel.SetFindMatch(side == ComparePanelSide.Base, start, end);
        AddressBox.Text = start.ToString("X8");
        if (!ViewModel.TryNavigateToAddress(start, out var rowIndex)) return;
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

            _lastSelectedSide = side;
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

    private void SelectAddressRow(int rowIndex, HexViewRow row, uint? rowStart)
    {
        _syncingScroll = true;
        try
        {
            _selectedByteAddress = null;
            FileAView.SetSelectedRow(rowIndex);
            FileBView.SetSelectedRow(rowIndex);
            if (!FileAView.UseWindowedScrolling)
            {
                FileAView.ItemsGrid.SelectedIndex = -1;
                FileBView.ItemsGrid.SelectedIndex = -1;
            }
            if (!row.IsGapMarker && rowStart is uint address)
                AddressBox.Text = address.ToString("X8");
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
            _lastSelectedSide = side;
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

    private void AddressInput_PreviewTextInput(object sender, TextCompositionEventArgs e)
    {
        if (sender is not TextBox box) return;
        var proposed = box.Text.Remove(box.SelectionStart, box.SelectionLength)
            .Insert(box.SelectionStart, e.Text);
        e.Handled = !HexAddressParser.IsValidPartialInput(proposed);
    }

    private static void AddressInput_Pasting(object sender, DataObjectPastingEventArgs e)
    {
        if (sender is not TextBox box ||
            !e.SourceDataObject.GetDataPresent(DataFormats.UnicodeText, true) ||
            e.SourceDataObject.GetData(DataFormats.UnicodeText, true) is not string pasted)
        {
            e.CancelCommand();
            return;
        }

        var proposed = box.Text.Remove(box.SelectionStart, box.SelectionLength)
            .Insert(box.SelectionStart, pasted);
        if (!HexAddressParser.IsValidPartialInput(proposed)) e.CancelCommand();
    }

    private void Go_Click(object sender, RoutedEventArgs e)
    {
        if (!HexAddressParser.TryParse(AddressBox.Text, out var address) || !ViewModel.TryNavigateToAddress(address, out var rowIndex))
        {
            MessageBox.Show(Window.GetWindow(this), "Enter an address in the displayed comparison range.", "Address not found", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        _selectedByteAddress = address;
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
        _rowWidthChangeAddress = SelectedAddress();
        _rowWidthChangeHadByteSelection = _selectedByteAddress is not null;
        _rowWidthChangeSide = _lastSelectedSide;
    }

    private void ViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(CompareViewModel.BytesPerRow)) return;

        var address = _rowWidthChangeAddress;
        var hadByteSelection = _rowWidthChangeHadByteSelection;
        _lastSelectedSide = _rowWidthChangeSide;
        _rowWidthChangeAddress = null;
        _rowWidthChangeHadByteSelection = false;

        FileAView.UseWindowedScrolling = true;
        FileBView.UseWindowedScrolling = true;
        if (address is not uint selectedAddress || !ViewModel.TryNavigateToAddress(selectedAddress, out var rowIndex)) return;
        if (hadByteSelection) _selectedByteAddress = selectedAddress;
        SelectRow(rowIndex);
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
        if (FileAView.UseWindowedScrolling && _selectedByteAddress is uint selectedByte)
            return selectedByte;
        var selected = FileAView.UseWindowedScrolling && FileAView.WindowRows.Count > 0
            ? FileAView.WindowRows[0].Row : FileAView.ItemsGrid.SelectedItem as HexViewRow;
        if (selected is { } row && HexAddressParser.TryParse(row.Address, out var rowAddress))
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
        FileAView.SetSelectedRow(-1);
        FileBView.SetSelectedRow(-1);
        _selectedByteAddress = address;
        AddressBox.Text = address.ToString("X8");
        if (ViewModel.TryNavigateToAddress(address, out var rowIndex)) SelectRow(rowIndex);
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
            _lastSelectedSide = side;
            _syncingScroll = true;
            var target = OppositeGrid(side);
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
        FileAView.SetSelectedRow(-1);
        FileBView.SetSelectedRow(-1);
        var side = SideOf(sender!);
        _lastSelectedSide = side;
        try
        {
            _selectedByteAddress = e.EndAddress;
            AddressBox.Text = e.EndAddress.ToString("X8");
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
            _selectedByteAddress = null;
            var target = ReferenceEquals(source, FileAView) ? FileBView : FileAView;
            _syncingScroll = true;
            target.ScrollToRow(source.FirstVisibleRowIndex);
            if (source.WindowRows.Count > 0 && HexAddressParser.TryParse(source.WindowRows[0].Row.Address, out var address))
                AddressBox.Text = address.ToString("X8");
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
