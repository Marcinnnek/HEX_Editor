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
using HexEditor.Core;
using HexEditor.Formats;
using HexEditor.Tools;

namespace HexEditor.UI;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : Window
{
    private readonly List<CompareWindow> _comparisonWindows = [];

    public MainWindow()
    {
        InitializeComponent();
        ViewModel = new MainViewModel();
        ViewModel.ImageChanged += ViewModel_ImageChanged;
        DataContext = this;
        Closing += MainWindow_Closing;
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
            var imageA = LoadImageForMerge(pathA);
            var imageB = LoadImageForMerge(pathB);
            var window = new CompareWindow(imageA.Image, imageB.Image, System.IO.Path.GetFileName(pathA), System.IO.Path.GetFileName(pathB)) { Owner = this };
            _comparisonWindows.Add(window);
            window.Closed += (_, _) => _comparisonWindows.Remove(window);
            window.Show();
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or FormatException)
        {
            MessageBox.Show(this, exception.Message, "Unable to compare files", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private LoadedMemoryImage LoadImageForMerge(string path)
    {
        if (PathsEqual(ViewModel.SourcePath, path))
            return new LoadedMemoryImage(ViewModel.CurrentImage, ViewModel.SourcePath!, ViewModel.SourceFormat);

        var service = new MemoryImageFileService();
        using var stream = File.OpenRead(path);
        var preview = service.PrepareImport(stream, HexEditor.Core.MemoryImage.Empty, path);
        var imported = preview.Apply(OverlapDecision.OverwriteIncoming) ?? throw new InvalidOperationException("The selected image could not be imported.");
        var format = imported.InputFormat == "Intel HEX" ? MemoryImageExportFormat.IntelHex : MemoryImageExportFormat.SRecord;
        return new LoadedMemoryImage(imported.Image, path, format);
    }

    private void MergeImages_Click(object sender, RoutedEventArgs e)
    {
        var destinationDialog = new Microsoft.Win32.OpenFileDialog
        {
            Title = "Select the destination image",
            Filter = "Firmware images (*.hex;*.ihx;*.s19;*.s28;*.s37;*.srec;*.mot)|*.hex;*.ihx;*.s19;*.s28;*.s37;*.srec;*.mot|All files (*.*)|*.*",
            CheckFileExists = true
        };
        if (destinationDialog.ShowDialog(this) != true) return;
        var sourceDialog = new Microsoft.Win32.OpenFileDialog
        {
            Title = "Select the source image to merge",
            Filter = destinationDialog.Filter,
            CheckFileExists = true,
            InitialDirectory = System.IO.Path.GetDirectoryName(destinationDialog.FileName)
        };
        if (sourceDialog.ShowDialog(this) != true) return;

        try
        {
            var imageA = LoadImageForMerge(destinationDialog.FileName);
            var imageB = LoadImageForMerge(sourceDialog.FileName);
            var dialog = new MergeWindow(imageA.Image, imageB.Image,
                System.IO.Path.GetFileName(destinationDialog.FileName),
                System.IO.Path.GetFileName(sourceDialog.FileName)) { Owner = this };
            if (dialog.ShowDialog() != true) return;

            var destination = ReferenceEquals(dialog.DestinationImage, imageA.Image) ? imageA : imageB;
            if (!ReferenceEquals(ViewModel.CurrentImage, destination.Image))
            {
                if (!ConfirmCloseImage()) return;
                ViewModel.LoadImage(destination.Image, destination.Path, destination.Format);
            }

            ViewModel.ApplyMerge(dialog.Preview, dialog.DefaultChoice, dialog.RangeOverrides);
            Title = ViewModel.WindowTitle;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or FormatException or ArgumentException or InvalidOperationException)
        {
            MessageBox.Show(this, exception.Message, "Unable to merge images", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void ViewModel_ImageChanged(HexEditor.Core.MemoryImage previousImage, HexEditor.Core.MemoryImage updatedImage)
    {
        foreach (var comparison in _comparisonWindows.ToArray())
            comparison.RefreshImage(previousImage, updatedImage);
    }

    private static bool PathsEqual(string? first, string second) =>
        !string.IsNullOrWhiteSpace(first) && string.Equals(System.IO.Path.GetFullPath(first), System.IO.Path.GetFullPath(second), StringComparison.OrdinalIgnoreCase);

    private sealed record LoadedMemoryImage(HexEditor.Core.MemoryImage Image, string? Path, MemoryImageExportFormat? Format);

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

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (ViewModel.SourcePath is null || ViewModel.SourceFormat is null) { SaveAs_Click(sender, e); return; }
            ViewModel.Save();
            Title = ViewModel.WindowTitle;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException or InvalidOperationException)
        {
            MessageBox.Show(this, exception.Message, "Unable to save file", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void SaveAs_Click(object sender, RoutedEventArgs e)
    {
        TrySaveAs();
    }

    private bool TrySaveAs()
    {
        var dialog = new Microsoft.Win32.SaveFileDialog
        {
            Title = "Save memory image as",
            Filter = "Intel HEX (*.hex)|*.hex|Motorola S-Record (*.s19)|*.s19",
            FilterIndex = ViewModel.SourceFormat == MemoryImageExportFormat.SRecord ? 2 : 1,
            AddExtension = true,
            OverwritePrompt = false
        };
        if (dialog.ShowDialog(this) != true) return false;
        var format = dialog.FilterIndex == 2 ? MemoryImageExportFormat.SRecord : MemoryImageExportFormat.IntelHex;
        var overwrite = false;
        if (File.Exists(dialog.FileName))
        {
            var answer = MessageBox.Show(this, $"Replace the existing file?\n\n{dialog.FileName}", "Confirm overwrite", MessageBoxButton.YesNo, MessageBoxImage.Warning);
            if (answer != MessageBoxResult.Yes) return false;
            overwrite = true;
        }
        try
        {
            ViewModel.SaveAs(dialog.FileName, format, overwrite);
            Title = ViewModel.WindowTitle;
            return true;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException)
        {
            MessageBox.Show(this, exception.Message, "Unable to save file", MessageBoxButton.OK, MessageBoxImage.Error);
            return false;
        }
    }

    private void CloseImage_Click(object sender, RoutedEventArgs e)
    {
        if (!ConfirmCloseImage()) return;
        ViewModel.CloseImage();
        Title = ViewModel.WindowTitle;
    }

    private void Undo_Click(object sender, RoutedEventArgs e)
    {
        ViewModel.Undo();
        Title = ViewModel.WindowTitle;
    }

    private void Redo_Click(object sender, RoutedEventArgs e)
    {
        ViewModel.Redo();
        Title = ViewModel.WindowTitle;
    }

    private void EditInsert_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var address = ParseAddress(RangeStartInput.Text);
            var bytes = ParsePattern(PatternInput.Text);
            var preview = ViewModel.PreviewInsert(address, bytes);
            var decision = preview.OverlapCount == 0 ? OverlapDecision.OverwriteIncoming : AskOverlap(preview.OverlapCount, "insert/replace");
            ViewModel.InsertBytes(address, bytes, decision);
            Title = ViewModel.WindowTitle;
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException or OverflowException)
        {
            ShowEditError(exception);
        }
    }

    private void Fill_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            ViewModel.FillRange(ParseAddress(RangeStartInput.Text), ParseAddress(RangeEndInput.Text), ParsePattern(PatternInput.Text));
            Title = ViewModel.WindowTitle;
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException or OverflowException)
        {
            ShowEditError(exception);
        }
    }

    private void CalculateChecksum_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var start = ParseAddress(RangeStartInput.Text);
            var end = ParseAddress(RangeEndInput.Text);
            var width = int.Parse(((ComboBoxItem)ChecksumWidthInput.SelectedItem).Content.ToString()!, CultureInfo.InvariantCulture);
            var missingTreatment = MissingTreatmentInput.SelectedIndex switch
            {
                0 => MissingAddressTreatment.Reject,
                1 => MissingAddressTreatment.FillByte,
                _ => throw new ArgumentException("Choose how missing addresses are treated.")
            };
            byte? fillByte = missingTreatment == MissingAddressTreatment.FillByte
                ? byte.Parse(HoleFillByteInput.Text.Trim().Replace("0x", string.Empty, StringComparison.OrdinalIgnoreCase), NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture)
                : null;
            var byteOrder = ChecksumByteOrderInput.SelectedIndex switch
            {
                0 => ChecksumByteOrder.BigEndian,
                1 => ChecksumByteOrder.LittleEndian,
                _ => throw new ArgumentException("Choose the byte order.")
            };
            var algorithm = ((ComboBoxItem)ChecksumAlgorithmInput.SelectedItem).Content.ToString();
            var request = algorithm switch
            {
                "Additive" => ChecksumRequest.Additive(start, end, width, missingTreatment, fillByte, byteOrder),
                "CRC-16/CCITT-FALSE" => ChecksumRequest.Crc16CcittFalse(start, end, missingTreatment, fillByte, byteOrder),
                "CRC-32/ISO-HDLC" => ChecksumRequest.Crc32IsoHdlc(start, end, missingTreatment, fillByte, byteOrder),
                "Custom CRC" => ChecksumRequest.CustomCrc(
                    start, end, width,
                    ParseHexValue(ChecksumPolynomialInput.Text),
                    ParseHexValue(ChecksumInitialInput.Text),
                    ReflectInputCheckBox.IsChecked == true,
                    ReflectOutputCheckBox.IsChecked == true,
                    ParseHexValue(ChecksumFinalXorInput.Text),
                    missingTreatment, fillByte, byteOrder),
                _ => throw new ArgumentException("Choose a checksum algorithm.")
            };
            ViewModel.CalculateChecksum(request);
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException or FormatException or OverflowException)
        {
            MessageBox.Show(this, exception.Message, "Unable to calculate checksum", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void WriteChecksum_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var address = ParseAddress(ChecksumWriteAddressInput.Text);
            var result = ViewModel.ChecksumResult ?? throw new InvalidOperationException("Calculate a checksum before writing a result.");
            var collisions = Enumerable.Range(0, result.WidthBits / 8)
                .Count(offset => ViewModel.CurrentImage.TryGetByte(checked(address + (uint)offset), out _));
            var decision = collisions == 0 ? (OverlapDecision?)null : AskOverlap(collisions, "checksum write");
            if (decision == OverlapDecision.Cancel) return;
            ViewModel.WriteChecksumResult(address, decision);
            Title = ViewModel.WindowTitle;
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException or FormatException or OverflowException)
        {
            MessageBox.Show(this, exception.Message, "Unable to write checksum", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void Copy_Click(object sender, RoutedEventArgs e) => RunRangeEdit(() => ViewModel.CopyRange(ParseAddress(RangeStartInput.Text), ParseAddress(RangeEndInput.Text)));
    private void Cut_Click(object sender, RoutedEventArgs e) => RunRangeEdit(() => ViewModel.CutRange(ParseAddress(RangeStartInput.Text), ParseAddress(RangeEndInput.Text)));
    private void Delete_Click(object sender, RoutedEventArgs e) => RunRangeEdit(() => ViewModel.DeleteRange(ParseAddress(RangeStartInput.Text), ParseAddress(RangeEndInput.Text)));

    private void Paste_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var address = ParseAddress(RangeStartInput.Text);
            var collisions = ViewModel.GetPasteOverlapCount(address);
            var decision = collisions == 0 ? OverlapDecision.OverwriteIncoming : AskOverlap(collisions, "paste");
            ViewModel.PasteAt(address, decision);
            Title = ViewModel.WindowTitle;
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException or OverflowException)
        {
            ShowEditError(exception);
        }
    }

    private void RunRangeEdit(Action action)
    {
        try { action(); Title = ViewModel.WindowTitle; }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException or OverflowException) { ShowEditError(exception); }
    }

    private OverlapDecision AskOverlap(int count, string operation)
    {
        var choice = MessageBox.Show(this,
            $"{operation} covers {count:N0} occupied byte(s).\n\nYes: replace overlapping bytes\nNo: keep existing bytes\nCancel: cancel this operation",
            "Overlapping data", MessageBoxButton.YesNoCancel, MessageBoxImage.Warning);
        return choice switch
        {
            MessageBoxResult.Yes => OverlapDecision.OverwriteIncoming,
            MessageBoxResult.No => OverlapDecision.KeepExisting,
            _ => OverlapDecision.Cancel
        };
    }

    private static byte[] ParsePattern(string text)
    {
        var parts = text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0 || parts.Any(part => part.Length != 2 || !byte.TryParse(part, NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out _)))
            throw new ArgumentException("Enter one or more two-digit hexadecimal bytes separated by spaces.");
        return parts.Select(part => byte.Parse(part, NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture)).ToArray();
    }

    private static uint ParseAddress(string text)
        => HexAddressParser.Parse(text);

    private static ulong ParseHexValue(string text)
    {
        text = text.Trim();
        if (text.StartsWith("0x", StringComparison.OrdinalIgnoreCase)) text = text[2..];
        if (!ulong.TryParse(text, NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out var value))
            throw new ArgumentException("Enter a hexadecimal checksum parameter.");
        return value;
    }

    private void ShowEditError(Exception exception) => MessageBox.Show(this, exception.Message, "Unable to edit image", MessageBoxButton.OK, MessageBoxImage.Error);

    private void MainWindow_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        e.Cancel = !ConfirmCloseImage();
    }

    private bool ConfirmCloseImage()
    {
        if (!ViewModel.IsModified) return true;
        var choice = MessageBox.Show(this, "Save changes to this memory image before closing?", "Unsaved changes", MessageBoxButton.YesNoCancel, MessageBoxImage.Warning);
        if (choice == MessageBoxResult.Cancel) return false;
        if (choice == MessageBoxResult.No) return true;
        if (ViewModel.SourcePath is null || ViewModel.SourceFormat is null) return TrySaveAs();
        try
        {
            ViewModel.Save();
            Title = ViewModel.WindowTitle;
            return true;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException or InvalidOperationException)
        {
            MessageBox.Show(this, exception.Message, "Unable to save file", MessageBoxButton.OK, MessageBoxImage.Error);
            return false;
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

    private void RelocateSegment_Click(object sender, RoutedEventArgs e)
    {
        if (SegmentsGrid.SelectedIndex < 0)
        {
            MessageBox.Show(this, "Select an occupied segment to relocate.", "No segment selected",
                MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var text = AddressInput.Text.Trim();
        if (text.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
        {
            text = text[2..];
        }

        if (!uint.TryParse(text, NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out var destination))
        {
            MessageBox.Show(this, "Enter the destination base as an absolute 32-bit hexadecimal address in the Address field.",
                "Invalid destination", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        try
        {
            var preview = ViewModel.PrepareRelocation(SegmentsGrid.SelectedIndex, destination);
            var decision = RelocationOverlapDecision.Overwrite;
            if (preview.Overlaps.Count > 0)
            {
                var identicalCount = preview.Overlaps.Count(overlap => overlap.ValuesAreIdentical);
                var differentCount = preview.Overlaps.Count - identicalCount;
                var addresses = string.Join(", ", preview.Overlaps.Take(8).Select(overlap => $"0x{overlap.Address:X8}"));
                if (preview.Overlaps.Count > 8)
                {
                    addresses += $", and {preview.Overlaps.Count - 8:N0} more";
                }

                var message = $"The destination overlaps {preview.Overlaps.Count:N0} occupied byte(s) " +
                              $"({identicalCount:N0} identical, {differentCount:N0} different).\n\n" +
                              $"Affected addresses: {addresses}\n\n" +
                              "Yes: overwrite destination bytes with the relocated segment\n" +
                              "No: keep existing destination bytes\n" +
                              "Cancel: cancel relocation";
                var choice = MessageBox.Show(this, message, "Overlapping destination data",
                    MessageBoxButton.YesNoCancel, MessageBoxImage.Warning);
                decision = choice switch
                {
                    MessageBoxResult.Yes => RelocationOverlapDecision.Overwrite,
                    MessageBoxResult.No => RelocationOverlapDecision.KeepExisting,
                    _ => RelocationOverlapDecision.Cancel
                };
            }

            if (ViewModel.ApplyRelocation(preview, decision) && ViewModel.TryNavigateToAddress(destination, out var rowIndex))
            {
                SelectRow(rowIndex);
            }
        }
        catch (ArgumentOutOfRangeException exception)
        {
            MessageBox.Show(this, exception.Message, "Invalid relocation", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void SegmentsGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (SegmentsGrid.SelectedIndex >= 0 &&
            ViewModel.TryNavigateToSegment(SegmentsGrid.SelectedIndex, out var rowIndex))
        {
            SelectRow(rowIndex);
        }
    }

    private void HexRowsGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (HexRowsGrid.SelectedItem is not HexViewRow { IsGapMarker: false } row ||
            !HexAddressParser.TryParse(row.Address, out var start))
        {
            ViewModel.ClearSelection();
            return;
        }

        var end = (uint)Math.Min(uint.MaxValue, (ulong)start + (uint)ViewModel.ImageView.BytesPerRow - 1);
        ViewModel.SetSelection(start, end);
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
