using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Globalization;
using HexEditor.Formats;

namespace HexEditor.UI;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        ViewModel = new MainViewModel();
        DataContext = this;
    }

    public MainViewModel ViewModel { get; }

    private void CompareImages_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Title = "Select the first firmware image",
            Filter = "Firmware images (*.hex;*.ihx;*.s19;*.s28;*.s37;*.srec;*.mot)|*.hex;*.ihx;*.s19;*.s28;*.s37;*.srec;*.mot|All files (*.*)|*.*",
            CheckFileExists = true
        };
        if (dialog.ShowDialog(this) != true) return;
        var pathA = dialog.FileName;
        dialog.Title = "Select the second firmware image";
        if (dialog.ShowDialog(this) != true) return;
        var pathB = dialog.FileName;

        try
        {
            var fileA = LoadImage(pathA);
            var fileB = LoadImage(pathB);
            new CompareWindow(fileA, fileB, System.IO.Path.GetFileName(pathA), System.IO.Path.GetFileName(pathB)) { Owner = this }.Show();
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or FormatException)
        {
            MessageBox.Show(this, exception.Message, "Unable to compare files", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private static HexEditor.Core.MemoryImage LoadImage(string path)
    {
        var service = new MemoryImageFileService();
        using var stream = File.OpenRead(path);
        var preview = service.PrepareImport(stream, HexEditor.Core.MemoryImage.Empty, path);
        return preview.Apply(OverlapDecision.OverwriteIncoming)?.Image ?? HexEditor.Core.MemoryImage.Empty;
    }

    private void OpenImage_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Title = "Open HEX or S-Record file",
            Filter = "Firmware images (*.hex;*.ihx;*.s19;*.s28;*.s37;*.srec;*.mot)|*.hex;*.ihx;*.s19;*.s28;*.s37;*.srec;*.mot|All files (*.*)|*.*",
            CheckFileExists = true
        };

        if (dialog.ShowDialog(this) != true)
        {
            return;
        }

        try
        {
            var preview = ViewModel.PrepareOpen(dialog.FileName);
            var decision = OverlapDecision.OverwriteIncoming;
            if (preview.OverlapSummary.TotalByteCount > 0)
            {
                var summary = preview.OverlapSummary;
                var affectedRanges = string.Join(
                    ", ",
                    summary.Ranges.Take(8).Select(range =>
                        $"0x{range.StartAddress:X8}-0x{range.EndAddress:X8}"));
                if (summary.Ranges.Count > 8)
                {
                    affectedRanges += $", and {summary.Ranges.Count - 8:N0} more";
                }

                var message = $"This import overlaps {summary.TotalByteCount:N0} byte(s) " +
                              $"({summary.IdenticalByteCount:N0} identical, {summary.DifferentByteCount:N0} different) " +
                              $"across {summary.Ranges.Count:N0} address range(s).\n\n" +
                              $"Affected ranges: {affectedRanges}\n\n" +
                              "Yes: overwrite with incoming data\nNo: keep existing data\nCancel: cancel import";
                var choice = MessageBox.Show(
                    this,
                    message,
                    "Overlapping data",
                    MessageBoxButton.YesNoCancel,
                    MessageBoxImage.Warning);
                decision = choice switch
                {
                    MessageBoxResult.Yes => OverlapDecision.OverwriteIncoming,
                    MessageBoxResult.No => OverlapDecision.KeepExisting,
                    _ => OverlapDecision.Cancel
                };
            }

            ViewModel.ApplyImport(preview, decision);
            Title = ViewModel.WindowTitle;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or FormatException)
        {
            MessageBox.Show(this, exception.Message, "Unable to open file", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void GoToAddress_Click(object sender, RoutedEventArgs e)
    {
        var text = AddressInput.Text.Trim();
        if (text.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
        {
            text = text[2..];
        }

        if (!uint.TryParse(text, NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out var address) ||
            !ViewModel.TryNavigateToAddress(address, out var rowIndex))
        {
            MessageBox.Show(this, "Enter an absolute 32-bit hexadecimal address in the image's displayed range.",
                "Address not found", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        SelectRow(rowIndex);
    }

    private void SegmentsGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (SegmentsGrid.SelectedIndex >= 0 &&
            ViewModel.TryNavigateToSegment(SegmentsGrid.SelectedIndex, out var rowIndex))
        {
            SelectRow(rowIndex);
        }
    }

    private void AsciiToggle_Changed(object sender, RoutedEventArgs e)
    {
        if (AsciiColumn is not null)
        {
            AsciiColumn.Visibility = ViewModel.ImageView.ShowAscii ? Visibility.Visible : Visibility.Collapsed;
        }
    }

    private void SelectRow(int rowIndex)
    {
        if ((uint)rowIndex >= (uint)HexRowsGrid.Items.Count)
        {
            return;
        }

        var row = HexRowsGrid.Items[rowIndex];
        HexRowsGrid.SelectedItem = row;
        HexRowsGrid.ScrollIntoView(row);
    }
}
