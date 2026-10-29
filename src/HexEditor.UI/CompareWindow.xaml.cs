using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using HexEditor.Core;

namespace HexEditor.UI;

public partial class CompareWindow : Window
{
    private bool _syncingScroll;

    public CompareWindow(MemoryImage fileA, MemoryImage fileB, string nameA = "File A", string nameB = "File B")
    {
        InitializeComponent();
        ViewModel = new CompareViewModel(fileA, fileB);
        RowWidths = [8, 16, 32];
        ViewModes = Enum.GetValues<CompareViewMode>();
        DataContext = this;
        Title = $"Compare: {nameA} ↔ {nameB}";
    }

    public CompareViewModel ViewModel { get; }
    public int[] RowWidths { get; }
    public CompareViewMode[] ViewModes { get; }

    private void Ascii_Changed(object sender, RoutedEventArgs e)
    {
        if (AsciiAColumn is not null)
        {
            AsciiAColumn.Visibility = ViewModel.ShowAscii ? Visibility.Visible : Visibility.Collapsed;
            AsciiBColumn.Visibility = ViewModel.ShowAscii ? Visibility.Visible : Visibility.Collapsed;
        }
    }

    private void ApplyRange_Click(object sender, RoutedEventArgs e)
    {
        if (!TryParseAddress(RangeStartBox.Text, out var start) || !TryParseAddress(RangeEndBox.Text, out var end) || start > end)
        {
            MessageBox.Show(this, "Enter a valid hexadecimal address range where Start is not greater than End.", "Invalid range", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        ViewModel.SetAddressRange(start, end);
        ViewModel.ViewMode = CompareViewMode.AddressRange;
    }

    private void Go_Click(object sender, RoutedEventArgs e)
    {
        if (!TryParseAddress(AddressBox.Text, out var address) || !ViewModel.TryNavigateToAddress(address, out var rowIndex))
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
        if (FileAGrid.SelectedItem is CompareViewRow row && uint.TryParse(row.Address, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var address)) return address;
        return 0;
    }

    private void Navigate(uint address)
    {
        AddressBox.Text = address.ToString("X8");
        if (ViewModel.TryNavigateToAddress(address, out var rowIndex)) SelectRow(rowIndex);
    }

    private void SelectRow(int index)
    {
        if ((uint)index >= (uint)ViewModel.Rows.Count) return;
        var row = ViewModel.Rows[index];
        _syncingScroll = true;
        FileAGrid.SelectedItem = row;
        FileBGrid.SelectedItem = row;
        FileAGrid.ScrollIntoView(row);
        FileBGrid.ScrollIntoView(row);
        _syncingScroll = false;
    }

    private void Panel_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_syncingScroll || sender is not DataGrid source || source.SelectedItem is not CompareViewRow row) return;
        _syncingScroll = true;
        var target = ReferenceEquals(source, FileAGrid) ? FileBGrid : FileAGrid;
        target.SelectedItem = row;
        target.ScrollIntoView(row);
        _syncingScroll = false;
    }

    private void ByteCell_Click(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: CompareByteCell cell })
        {
            Navigate(cell.Address);
        }
    }

    private void Grid_ScrollChanged(object sender, ScrollChangedEventArgs e)
    {
        if (_syncingScroll || sender is not DataGrid source || e.VerticalChange == 0) return;
        var target = ReferenceEquals(source, FileAGrid) ? FileBGrid : FileAGrid;
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

    private static bool TryParseAddress(string text, out uint address)
    {
        text = text.Trim();
        if (text.StartsWith("0x", StringComparison.OrdinalIgnoreCase)) text = text[2..];
        return uint.TryParse(text, NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out address);
    }
}
