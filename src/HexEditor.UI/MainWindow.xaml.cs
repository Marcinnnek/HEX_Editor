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
using System.Threading;
using System.Threading.Tasks;
using HexEditor.Core;
using HexEditor.Formats;
using HexEditor.Tools;

namespace HexEditor.UI;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : Window
{
    private readonly List<CompareView> _comparisonViews = [];
    private readonly ImageEditWorkflow _imageEdits;
    private readonly ChecksumWorkflow _checksums;
    private readonly ComparisonImageLoader _comparisonImageLoader;
    private readonly CompareCurrentImageWorkflow _compareWorkflow;
    private readonly MemoryImageFileWorkflow _files;
    private CancellationTokenSource? _fileOperationCancellation;

    public MainWindow()
    {
        InitializeComponent();
        ViewModel = new MainViewModel();
        _imageEdits = new ImageEditWorkflow(ViewModel);
        _checksums = new ChecksumWorkflow(ViewModel);
        _comparisonImageLoader = new ComparisonImageLoader(ViewModel);
        _compareWorkflow = new CompareCurrentImageWorkflow(ViewModel);
        _files = new MemoryImageFileWorkflow(ViewModel);
        ViewModel.ImageChanged += ViewModel_ImageChanged;
        DataContext = this;
        Closing += MainWindow_Closing;
    }

    public MainViewModel ViewModel { get; }

    private async Task<T> RunFileOperationAsync<T>(Func<CancellationToken, IProgress<MemoryImageFileProgress>, Task<T>> operation)
    {
        if (_fileOperationCancellation is not null) throw new InvalidOperationException("A file operation is already running.");
        using var cancellation = new CancellationTokenSource();
        _fileOperationCancellation = cancellation;
        FileOperationPanel.Visibility = Visibility.Visible;
        FileOperationProgress.IsIndeterminate = true;
        FileOperationText.Text = "Working...";
        var progress = new Progress<MemoryImageFileProgress>(update =>
        {
            FileOperationText.Text = update.Total is { } total
                ? $"{update.Stage}: {update.Completed:N0} / {total:N0} bytes"
                : $"{update.Stage}: {update.Completed:N0}";
            FileOperationProgress.IsIndeterminate = update.Total is null or 0;
            if (update.Total is > 0)
            {
                FileOperationProgress.Minimum = 0;
                FileOperationProgress.Maximum = update.Total.Value;
                FileOperationProgress.Value = Math.Min(update.Completed, update.Total.Value);
            }
        });
        try
        {
            return await operation(cancellation.Token, progress);
        }
        finally
        {
            _fileOperationCancellation = null;
            FileOperationPanel.Visibility = Visibility.Collapsed;
            FileOperationProgress.IsIndeterminate = true;
        }
    }

    private async Task RunFileOperationAsync(Func<CancellationToken, IProgress<MemoryImageFileProgress>, Task> operation) =>
        await RunFileOperationAsync(async (token, progress) =>
        {
            await operation(token, progress);
            return true;
        });

    private void CancelFileOperation_Click(object sender, RoutedEventArgs e)
    {
        FileOperationText.Text = "Cancelling...";
        _fileOperationCancellation?.Cancel();
    }

    private void RowWidth8_Click(object sender, RoutedEventArgs e) => ViewModel.ImageView.BytesPerRow = 8;
    private void RowWidth16_Click(object sender, RoutedEventArgs e) => ViewModel.ImageView.BytesPerRow = 16;
    private void RowWidth32_Click(object sender, RoutedEventArgs e) => ViewModel.ImageView.BytesPerRow = 32;

    private async void CompareImages_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var comparison = await RunFileOperationAsync((token, progress) => _compareWorkflow.StartAsync(
                (title, directory) =>
                {
                    var dialog = FirmwareImageDialog.CreateOpen(title, directory);
                    return dialog.ShowDialog(this) == true ? dialog.FileName : null;
                },
                () =>
                {
                    WorkspaceTabs.SelectedIndex = 0;
                    Title = ViewModel.WindowTitle;
                }, token, progress));
            if (comparison is null) return;

            var view = new CompareView(comparison);
            view.StatusRequested += ViewModel.ReportCompareInteraction;
            view.BaseRowActivated += address =>
                ViewModel.ReportCompareInteraction($"Base row at 0x{address:X8} selected.");
            _comparisonViews.Add(view);
            var tab = new TabItem { Header = $"Compare: {comparison.NameA} ↔ {comparison.NameB}", Content = view };
            WorkspaceTabs.Items.Add(tab);
            WorkspaceTabs.SelectedItem = tab;
        }
        catch (OperationCanceledException) { ViewModel.ReportFileOperationCancelled("Compare"); }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or FormatException)
        {
            MessageBox.Show(this, exception.Message, "Unable to compare files", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private async void MergeImages_Click(object sender, RoutedEventArgs e)
    {
        var destinationDialog = FirmwareImageDialog.CreateOpen("Select the destination image");
        if (destinationDialog.ShowDialog(this) != true) return;
        var sourceDialog = FirmwareImageDialog.CreateOpen(
            "Select the source image to merge",
            System.IO.Path.GetDirectoryName(destinationDialog.FileName));
        if (sourceDialog.ShowDialog(this) != true) return;

        try
        {
            var (imageA, imageB) = await LoadComparisonImagesAsync(destinationDialog.FileName, sourceDialog.FileName);
            var dialog = new MergeWindow(imageA.Image, imageB.Image,
                System.IO.Path.GetFileName(destinationDialog.FileName),
                System.IO.Path.GetFileName(sourceDialog.FileName)) { Owner = this };
            if (dialog.ShowDialog() != true) return;

            var destination = ReferenceEquals(dialog.DestinationImage, imageA.Image) ? imageA : imageB;
            if (!_files.ApplyMerge(destination, dialog.Preview, dialog.DefaultChoice, dialog.RangeOverrides, ConfirmCloseImage)) return;
            Title = ViewModel.WindowTitle;
        }
        catch (OperationCanceledException) { ViewModel.ReportFileOperationCancelled("Merge"); }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or FormatException or ArgumentException or InvalidOperationException)
        {
            MessageBox.Show(this, exception.Message, "Unable to merge images", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private Task<(LoadedMemoryImage First, LoadedMemoryImage Second)> LoadComparisonImagesAsync(string firstPath, string secondPath) =>
        RunFileOperationAsync(async (token, progress) =>
        {
            var first = await _comparisonImageLoader.LoadAsync(firstPath, token, AddProgressStage(progress, "Image 1 of 2"));
            var second = await _comparisonImageLoader.LoadAsync(secondPath, token, AddProgressStage(progress, "Image 2 of 2"));
            return (first, second);
        });

    private static IProgress<MemoryImageFileProgress> AddProgressStage(
        IProgress<MemoryImageFileProgress> progress, string stage) =>
        new Progress<MemoryImageFileProgress>(update =>
            progress.Report(update with { Stage = $"{stage}: {update.Stage}" }));

    private void ViewModel_ImageChanged(HexEditor.Core.MemoryImage previousImage, HexEditor.Core.MemoryImage updatedImage)
    {
        foreach (var comparison in _comparisonViews.ToArray())
            comparison.Workspace.RefreshDocument(ViewModel.SourcePath, updatedImage);
    }

    private async void OpenImage_Click(object sender, RoutedEventArgs e)
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
            var preview = await RunFileOperationAsync((token, progress) => _files.PrepareOpenAsync(dialog.FileName, token, progress));
            ViewModel.ApplyImport(preview, ChooseImportOverlap(preview));
            Title = ViewModel.WindowTitle;
        }
        catch (OperationCanceledException) { ViewModel.ReportFileOperationCancelled("Open"); }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or FormatException)
        {
            MessageBox.Show(this, exception.Message, "Unable to open file", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private async void Save_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (ViewModel.SourcePath is null || ViewModel.SourceFormat is null) { await TrySaveAsAsync(); return; }
            await RunFileOperationAsync((token, progress) => _files.SaveAsync(token, progress));
            Title = ViewModel.WindowTitle;
        }
        catch (OperationCanceledException) { ViewModel.ReportFileOperationCancelled("Save"); }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException or InvalidOperationException)
        {
            MessageBox.Show(this, exception.Message, "Unable to save file", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private async void SaveAs_Click(object sender, RoutedEventArgs e)
    {
        await TrySaveAsAsync();
    }

    private async Task<bool> TrySaveAsAsync()
    {
        if (!TryGetSaveAsSelection(out var selection)) return false;
        try
        {
            await RunFileOperationAsync((token, progress) => _files.SaveAsAsync(selection.Path, selection.Format, selection.Overwrite,
                token, progress));
            Title = ViewModel.WindowTitle;
            return true;
        }
        catch (OperationCanceledException) { ViewModel.ReportFileOperationCancelled("Save"); return false; }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException)
        {
            MessageBox.Show(this, exception.Message, "Unable to save file", MessageBoxButton.OK, MessageBoxImage.Error);
            return false;
        }
    }

    private bool TrySaveAs()
    {
        if (!TryGetSaveAsSelection(out var selection)) return false;
        try
        {
            _files.SaveAs(selection.Path, selection.Format, selection.Overwrite);
            Title = ViewModel.WindowTitle;
            return true;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException)
        {
            MessageBox.Show(this, exception.Message, "Unable to save file", MessageBoxButton.OK, MessageBoxImage.Error);
            return false;
        }
    }

    private bool TryGetSaveAsSelection(out SaveAsSelection selection)
    {
        selection = default;
        var dialog = new Microsoft.Win32.SaveFileDialog
        {
            Title = "Save memory image as",
            Filter = "Intel HEX (*.hex)|*.hex|Motorola S-Record (*.s19)|*.s19",
            FilterIndex = ViewModel.SourceFormat == MemoryImageExportFormat.SRecord ? 2 : 1,
            AddExtension = true,
            OverwritePrompt = false
        };
        if (dialog.ShowDialog(this) != true) return false;

        var overwrite = false;
        if (File.Exists(dialog.FileName))
        {
            var answer = MessageBox.Show(this, $"Replace the existing file?\n\n{dialog.FileName}", "Confirm overwrite", MessageBoxButton.YesNo, MessageBoxImage.Warning);
            if (answer != MessageBoxResult.Yes) return false;
            overwrite = true;
        }

        selection = new SaveAsSelection(dialog.FileName,
            dialog.FilterIndex == 2 ? MemoryImageExportFormat.SRecord : MemoryImageExportFormat.IntelHex, overwrite);
        return true;
    }

    private readonly record struct SaveAsSelection(string Path, MemoryImageExportFormat Format, bool Overwrite);

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
            _imageEdits.Insert(address, bytes, AskOverlap);
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
            var request = BuildChecksumRequest();
            _checksums.Calculate(request);
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException or FormatException or OverflowException)
        {
            MessageBox.Show(this, exception.Message, "Unable to calculate checksum", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private ChecksumRequest BuildChecksumRequest()
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
        var algorithm = ((ComboBoxItem)ChecksumAlgorithmInput.SelectedItem).Content.ToString()!;
        return algorithm switch
        {
            "Additive" => ChecksumRequest.Additive(start, end, width, missingTreatment, fillByte, byteOrder),
            "CRC-16/CCITT-FALSE" => ChecksumRequest.Crc16CcittFalse(start, end, missingTreatment, fillByte, byteOrder),
            "CRC-32/ISO-HDLC" => ChecksumRequest.Crc32IsoHdlc(start, end, missingTreatment, fillByte, byteOrder),
            "Custom CRC" => ChecksumRequest.CustomCrc(start, end, width,
                ParseHexChecksumValue(ChecksumPolynomialInput.Text), ParseHexChecksumValue(ChecksumInitialInput.Text),
                ReflectInputCheckBox.IsChecked == true, ReflectOutputCheckBox.IsChecked == true,
                ParseHexChecksumValue(ChecksumFinalXorInput.Text), missingTreatment, fillByte, byteOrder),
            _ => throw new ArgumentException("Choose a checksum algorithm.")
        };
    }

    private static ulong ParseHexChecksumValue(string text)
    {
        text = text.Trim();
        if (text.StartsWith("0x", StringComparison.OrdinalIgnoreCase)) text = text[2..];
        if (!ulong.TryParse(text, NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out var value))
            throw new ArgumentException("Enter a hexadecimal checksum parameter.");
        return value;
    }

    private void WriteChecksum_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var address = ParseAddress(ChecksumWriteAddressInput.Text);
            _checksums.Write(address, AskOverlap);
            Title = ViewModel.WindowTitle;
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException or FormatException or OverflowException)
        {
            MessageBox.Show(this, exception.Message, "Unable to write checksum", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void Copy_Click(object sender, RoutedEventArgs e) => RunRangeEdit(() => _imageEdits.Copy(ParseAddress(RangeStartInput.Text), ParseAddress(RangeEndInput.Text)));
    private void Cut_Click(object sender, RoutedEventArgs e) => RunRangeEdit(() => _imageEdits.Cut(ParseAddress(RangeStartInput.Text), ParseAddress(RangeEndInput.Text)));
    private void Delete_Click(object sender, RoutedEventArgs e) => RunRangeEdit(() => _imageEdits.Delete(ParseAddress(RangeStartInput.Text), ParseAddress(RangeEndInput.Text)));

    private void Paste_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var address = ParseAddress(RangeStartInput.Text);
            _imageEdits.Paste(address, AskOverlap);
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
        var choice = ShowYesNoCancelWarning(
            $"{operation} covers {count:N0} occupied byte(s).\n\nYes: replace overlapping bytes\nNo: keep existing bytes\nCancel: cancel this operation",
            "Overlapping data");
        return choice switch
        {
            YesNoCancelChoice.Yes => OverlapDecision.OverwriteIncoming,
            YesNoCancelChoice.No => OverlapDecision.KeepExisting,
            _ => OverlapDecision.Cancel
        };
    }

    private OverlapDecision ChooseImportOverlap(MemoryImageImportPreview preview)
    {
        var summary = preview.OverlapSummary;
        if (summary.TotalByteCount == 0) return OverlapDecision.OverwriteIncoming;
        var affectedRanges = string.Join(", ", summary.Ranges.Take(8).Select(range =>
            $"0x{range.StartAddress:X8}-0x{range.EndAddress:X8}"));
        if (summary.Ranges.Count > 8) affectedRanges += $", and {summary.Ranges.Count - 8:N0} more";
        var message = $"This import overlaps {summary.TotalByteCount:N0} byte(s) " +
                      $"({summary.IdenticalByteCount:N0} identical, {summary.DifferentByteCount:N0} different) " +
                      $"across {summary.Ranges.Count:N0} address range(s).\n\n" +
                      $"Affected ranges: {affectedRanges}\n\n" +
                      "Yes: overwrite with incoming data\nNo: keep existing data\nCancel: cancel import";
        var choice = ShowYesNoCancelWarning(message, "Overlapping data");
        return choice switch
        {
            YesNoCancelChoice.Yes => OverlapDecision.OverwriteIncoming,
            YesNoCancelChoice.No => OverlapDecision.KeepExisting,
            _ => OverlapDecision.Cancel
        };
    }

    private YesNoCancelChoice ShowYesNoCancelWarning(string message, string title)
        => MessageBox.Show(this, message, title, MessageBoxButton.YesNoCancel, MessageBoxImage.Warning) switch
        {
            MessageBoxResult.Yes => YesNoCancelChoice.Yes,
            MessageBoxResult.No => YesNoCancelChoice.No,
            _ => YesNoCancelChoice.Cancel
        };

    private static byte[] ParsePattern(string text)
    {
        var parts = text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0 || parts.Any(part => part.Length != 2 || !byte.TryParse(part, NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out _)))
            throw new ArgumentException("Enter one or more two-digit hexadecimal bytes separated by spaces.");
        return parts.Select(part => byte.Parse(part, NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture)).ToArray();
    }

    private static uint ParseAddress(string text)
        => HexAddressParser.Parse(text);

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
            _files.Save();
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
        if (!HexAddressParser.TryParse(AddressInput.Text, out var address) ||
            !ViewModel.TryNavigateToAddress(address, out var rowIndex))
        {
            MessageBox.Show(this, "Enter an absolute 32-bit hexadecimal address in the image's displayed range.",
                "Address not found", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        SelectRow(rowIndex);
        ViewModel.EditorStatus.SetSelection(address, address);
        RangeStartInput.Text = address.ToString("X8");
        RangeEndInput.Text = address.ToString("X8");
    }

    private void SelectRange_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var start = HexAddressParser.Parse(RangeStartInput.Text);
            var end = HexAddressParser.Parse(RangeEndInput.Text);
            ViewModel.EditorStatus.SetSelection(start, end);
            if (ViewModel.ImageView.TryNavigateToAddress(start, out var rowIndex)) SelectRow(rowIndex);
        }
        catch (ArgumentException exception)
        {
            MessageBox.Show(this, exception.Message, "Invalid selection range", MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }

    private async void ExportSegments_Click(object sender, RoutedEventArgs e)
    {
        var segmentIndexes = SegmentsGrid.SelectedItems
            .Cast<MemorySegmentRow>()
            .Select(ViewModel.Segments.IndexOf)
            .ToArray();
        await ExportToFileAsync((format, payloadSize, output, token, progress) =>
            _files.ExportSegments(segmentIndexes, format, payloadSize, output, token, progress));
    }

    private async void ExportRange_Click(object sender, RoutedEventArgs e)
    {
        if (!HexAddressParser.TryParse(ExportRangeStartInput.Text, out var startAddress) ||
            !HexAddressParser.TryParse(ExportRangeEndInput.Text, out var endAddress))
        {
            MessageBox.Show(this, "Enter both range bounds as 32-bit hexadecimal addresses (for example, 0x08000000).",
                "Invalid export range", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        await ExportToFileAsync((format, payloadSize, output, token, progress) =>
            _files.ExportAddressRange(startAddress, endAddress, format, payloadSize, output, token, progress));
    }

    private async Task ExportToFileAsync(Action<MemoryImageExportFormat, int, Stream, CancellationToken, IProgress<MemoryImageFileProgress>?> export)
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
            await RunFileOperationAsync((token, progress) => Task.Run(() =>
                _files.Export(dialog.FileName, (output, operationToken, operationProgress) =>
                    export(format, payloadSize, output, operationToken, operationProgress), token, progress), token));
            ViewModel.ReportExported(System.IO.Path.GetFileName(dialog.FileName));
        }
        catch (OperationCanceledException) { ViewModel.ReportFileOperationCancelled("Export"); }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException or InvalidOperationException)
        {
            MessageBox.Show(this, exception.Message, "Unable to export selected data", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void RelocateSegment_Click(object sender, RoutedEventArgs e)
    {
        if (SegmentsGrid.SelectedIndex < 0)
        {
            MessageBox.Show(this, "Select an occupied segment to relocate.", "No segment selected",
                MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        if (!HexAddressParser.TryParse(AddressInput.Text, out var destination))
        {
            MessageBox.Show(this, "Enter the destination base as an absolute 32-bit hexadecimal address in the Address field.",
                "Invalid destination", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        try
        {
            if (_imageEdits.Relocate(SegmentsGrid.SelectedIndex, destination, AskRelocationOverlap) &&
                ViewModel.TryNavigateToAddress(destination, out var rowIndex))
            {
                SelectRow(rowIndex);
            }
        }
        catch (ArgumentOutOfRangeException exception)
        {
            MessageBox.Show(this, exception.Message, "Invalid relocation", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private RelocationOverlapDecision AskRelocationOverlap(MemoryRelocationPreview preview)
    {
        var identicalCount = preview.Overlaps.Count(overlap => overlap.ValuesAreIdentical);
        var differentCount = preview.Overlaps.Count - identicalCount;
        var addresses = string.Join(", ", preview.Overlaps.Take(8).Select(overlap => $"0x{overlap.Address:X8}"));
        if (preview.Overlaps.Count > 8) addresses += $", and {preview.Overlaps.Count - 8:N0} more";
        var message = $"The destination overlaps {preview.Overlaps.Count:N0} occupied byte(s) " +
                      $"({identicalCount:N0} identical, {differentCount:N0} different).\n\n" +
                      $"Affected addresses: {addresses}\n\n" +
                      "Yes: overwrite destination bytes with the relocated segment\n" +
                      "No: keep existing destination bytes\n" +
                      "Cancel: cancel relocation";
        var choice = ShowYesNoCancelWarning(message, "Overlapping destination data");
        return choice switch
        {
            YesNoCancelChoice.Yes => RelocationOverlapDecision.Overwrite,
            YesNoCancelChoice.No => RelocationOverlapDecision.KeepExisting,
            _ => RelocationOverlapDecision.Cancel
        };
    }

    private enum YesNoCancelChoice
    {
        Yes,
        No,
        Cancel
    }

    private void SegmentsGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (SegmentsGrid.SelectedIndex >= 0 &&
            ViewModel.TryNavigateToSegment(SegmentsGrid.SelectedIndex, out var rowIndex))
        {
            SelectRow(rowIndex);
        }
    }

    private void HexView_AddressRangeSelected(object? sender, AddressRangeSelectedEventArgs e)
    {
        ViewModel.EditorStatus.SetSelection(e.StartAddress, e.EndAddress);
        RangeStartInput.Text = e.StartAddress.ToString("X8");
        RangeEndInput.Text = e.EndAddress.ToString("X8");
    }

    private void SelectRow(int rowIndex)
    {
        HexRowsView.ScrollToRow(rowIndex);
    }
}
