using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using HexEditor.Core;

namespace HexEditor.UI;

public partial class CompareWindow : Window
{
    private bool _syncingScroll;
    private MemoryImage _fileA;
    private MemoryImage _fileB;

    public CompareWindow(MemoryImage fileA, MemoryImage fileB, string nameA = "File A", string nameB = "File B")
    {
        InitializeComponent();
        _fileA = fileA;
        _fileB = fileB;
        ViewModel = new CompareViewModel(fileA, fileB);
        RowWidths = [8, 16, 32];
        ViewModes = Enum.GetValues<CompareViewMode>();
        DataContext = this;
        FileAView.ItemsGrid.SelectionChanged += Panel_SelectionChanged;
        FileBView.ItemsGrid.SelectionChanged += Panel_SelectionChanged;
        FileAView.ItemsGrid.AddHandler(ScrollViewer.ScrollChangedEvent, new ScrollChangedEventHandler(Grid_ScrollChanged));
        FileBView.ItemsGrid.AddHandler(ScrollViewer.ScrollChangedEvent, new ScrollChangedEventHandler(Grid_ScrollChanged));
        Title = $"Compare: {nameA} ↔ {nameB}";
    }

    public CompareViewModel ViewModel { get; }
    public int[] RowWidths { get; }
    public CompareViewMode[] ViewModes { get; }

    public void RefreshImage(MemoryImage previousImage, MemoryImage updatedImage)
    {
        var updateA = ReferenceEquals(_fileA, previousImage);
        var updateB = ReferenceEquals(_fileB, previousImage);
        if (!updateA && !updateB) return;
        if (updateA) _fileA = updatedImage;
        if (updateB) _fileB = updatedImage;
        ViewModel.Refresh(_fileA, _fileB);
    }

    private void ApplyRange_Click(object sender, RoutedEventArgs e)
    {
        if (!HexAddressParser.TryParse(RangeStartBox.Text, out var start) || !HexAddressParser.TryParse(RangeEndBox.Text, out var end) || start > end)
        {
            MessageBox.Show(this, "Enter a valid hexadecimal address range where Start is not greater than End.", "Invalid range", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        ViewModel.SetAddressRange(start, end);
        ViewModel.ViewMode = CompareViewMode.AddressRange;
    }

    private void Go_Click(object sender, RoutedEventArgs e)
    {
        if (!HexAddressParser.TryParse(AddressBox.Text, out var address) || !ViewModel.TryNavigateToAddress(address, out var rowIndex))
        {
            MessageBox.Show(this, "Enter an address in the displayed comparison range.", "Address not found", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        SelectRow(rowIndex);
    }

    private void NextDifference_Click(object sender, RoutedEventArgs e)
    {
        var current = SelectedAddress();
        if (current < uint.MaxValue && ViewModel.TryFindNextDifference(current + 1, out var address)) Navigate(address);
        else MessageBox.Show(this, "No later differences were found.", "Comparison", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void PreviousDifference_Click(object sender, RoutedEventArgs e)
    {
        var current = SelectedAddress();
        if (current > 0 && ViewModel.TryFindPreviousDifference(current - 1, out var address)) Navigate(address);
        else MessageBox.Show(this, "No earlier differences were found.", "Comparison", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private uint SelectedAddress()
    {
        if (FileAView.ItemsGrid.SelectedItem is HexViewRow row && HexAddressParser.TryParse(row.Address, out var address)) return address;
        return 0;
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
