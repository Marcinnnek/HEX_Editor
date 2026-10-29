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
        Closing += MainWindow_Closing;
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
    {
        text = text.Trim();
        if (text.StartsWith("0x", StringComparison.OrdinalIgnoreCase)) text = text[2..];
        if (!uint.TryParse(text, NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out var address))
            throw new ArgumentException("Enter a 32-bit hexadecimal address.");
        return address;
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
