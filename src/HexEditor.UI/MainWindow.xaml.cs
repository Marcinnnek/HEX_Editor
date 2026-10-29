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

    private void ExportSegments_Click(object sender, RoutedEventArgs e)
    {
        var segmentIndexes = SegmentsGrid.SelectedItems
            .Cast<MemorySegmentRow>()
            .Select(ViewModel.Segments.IndexOf)
            .ToArray();
        ExportToFile((format, payloadSize, output) => ViewModel.ExportSegments(segmentIndexes, format, payloadSize, output));
    }

    private void ExportRange_Click(object sender, RoutedEventArgs e)
    {
        if (!TryParseExportAddress(ExportRangeStartInput.Text, out var startAddress) ||
            !TryParseExportAddress(ExportRangeEndInput.Text, out var endAddress))
        {
            MessageBox.Show(this, "Enter both range bounds as 32-bit hexadecimal addresses (for example, 0x08000000).",
                "Invalid export range", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        ExportToFile((format, payloadSize, output) =>
            ViewModel.ExportAddressRange(startAddress, endAddress, format, payloadSize, output));
    }

    private void ExportToFile(Action<MemoryImageExportFormat, int, Stream> export)
    {
        if (ExportFormatInput.SelectedItem is not MemoryImageExportFormat format ||
            ExportPayloadInput.SelectedItem is not int payloadSize)
        {
            MessageBox.Show(this, "Choose an output format and a record payload size of 8, 16, or 32 bytes.",
                "Export options required", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var isIntelHex = format == MemoryImageExportFormat.IntelHex;
        var dialog = new Microsoft.Win32.SaveFileDialog
        {
            Title = "Export selected memory data",
            Filter = isIntelHex
                ? "Intel HEX (*.hex)|*.hex|All files (*.*)|*.*"
                : "Motorola S-Record (*.srec)|*.srec|All files (*.*)|*.*",
            DefaultExt = isIntelHex ? ".hex" : ".srec",
            AddExtension = true,
            OverwritePrompt = true
        };

        if (dialog.ShowDialog(this) != true)
        {
            return;
        }

        try
        {
            using var output = File.Create(dialog.FileName);
            export(format, payloadSize, output);
            ViewModel.ReportExported(System.IO.Path.GetFileName(dialog.FileName));
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException or InvalidOperationException)
        {
            MessageBox.Show(this, exception.Message, "Unable to export selected data", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private static bool TryParseExportAddress(string value, out uint address)
    {
        var text = value.Trim();
        if (text.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
        {
            text = text[2..];
        }

        return uint.TryParse(text, NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out address);
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
