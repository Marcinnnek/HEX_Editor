using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using HexEditor.Core;
using HexEditor.Compare;

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
    }

    public CompareWorkspaceTabViewModel Workspace { get; }
    public CompareViewModel ViewModel { get; }
    public int[] RowWidths { get; }
    public CompareViewMode[] ViewModes { get; }

    public void RefreshImage(MemoryImage previousImage, MemoryImage updatedImage)
    {
        Workspace.RefreshImage(previousImage, updatedImage);
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
        if (ViewModel.TryFindNextDifferenceFromSelection(SelectedAddress(), out var address)) Navigate(address);
        else MessageBox.Show(Window.GetWindow(this), "No later differences were found.", "Comparison", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void Block_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_syncingScroll || sender is not DataGrid grid || grid.SelectedItem is not CompareMemoryBlockRow block) return;
        ViewModel.ViewMode = CompareViewMode.AllRegions;
        Navigate(block.StartAddress);
    }

    private void PreviousDifference_Click(object sender, RoutedEventArgs e)
    {
        if (ViewModel.TryFindPreviousDifferenceFromSelection(SelectedAddress(), out var address)) Navigate(address);
        else MessageBox.Show(Window.GetWindow(this), "No earlier differences were found.", "Comparison", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private uint? SelectedAddress()
    {
        if (FileAView.ItemsGrid.SelectedItem is HexViewRow row && HexAddressParser.TryParse(row.Address, out var address)) return address;
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
