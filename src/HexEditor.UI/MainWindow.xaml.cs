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
}
