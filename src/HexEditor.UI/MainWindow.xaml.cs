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
using Wpf.Ui.Appearance;
using WindowBackdropType = Wpf.Ui.Controls.WindowBackdropType;
using MessageBox = HexEditor.UI.ThemedMessageBox;

namespace HexEditor.UI;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : Window
{
    private readonly AppearanceSettings _appearanceSettings = new();
    private readonly ExportSettings _exportSettings = new();
    private readonly UserDataDiagnostics _diagnostics = new();
    private bool _watchingSystemTheme;
    private readonly ImageEditWorkflow _imageEdits;
    private readonly ChecksumWorkflow _checksums;
    private readonly ComparisonImageLoader _comparisonImageLoader;
    private readonly CompareCurrentImageWorkflow _compareWorkflow;
    private readonly MemoryImageFileWorkflow _files;
    private CancellationTokenSource? _fileOperationCancellation;
    private uint? _selectionStart;
    private uint? _selectionEnd;

    public MainWindow()
    {
        InitializeComponent();
        ViewModel = new MainViewModel();
        _imageEdits = new ImageEditWorkflow(ViewModel);
        _checksums = new ChecksumWorkflow(ViewModel);
        _comparisonImageLoader = new ComparisonImageLoader(ViewModel);
        _compareWorkflow = new CompareCurrentImageWorkflow(ViewModel);
        _files = new MemoryImageFileWorkflow(ViewModel);
        HexRowsView.ItemsGrid.AddHandler(Control.MouseDoubleClickEvent, new MouseButtonEventHandler(EditorRow_DoubleClick), handledEventsToo: true);
        DataContext = this;
        Closing += MainWindow_Closing;
        Loaded += (_, _) => ApplyAppearance();
        ApplyAppearance();
    }

    private void AutomaticAppearance_Click(object sender, RoutedEventArgs e) => SetAppearance(AppearanceMode.Automatic);
    private void LightAppearance_Click(object sender, RoutedEventArgs e) => SetAppearance(AppearanceMode.Light);
    private void DarkAppearance_Click(object sender, RoutedEventArgs e) => SetAppearance(AppearanceMode.Dark);

    private void Preferences_Click(object sender, RoutedEventArgs e)
    {
        try { new PreferencesWindow { Owner = this }.ShowDialog(); }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            RecordError("Save grid spacing preferences", exception);
            MessageBox.Show(this, exception.Message, "Unable to save preferences", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void SetAppearance(AppearanceMode mode)
    {
        try
        {
            _appearanceSettings.SetMode(mode);
            ApplyAppearance();
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            RecordError("Save appearance", exception);
            MessageBox.Show(this, exception.Message, "Unable to save appearance", MessageBoxButton.OK, MessageBoxImage.Error);
            ApplyAppearance();
        }
    }

    private void ApplyAppearance()
    {
        var mode = _appearanceSettings.Mode;
        AutomaticAppearanceItem.IsChecked = mode == AppearanceMode.Automatic;
        LightAppearanceItem.IsChecked = mode == AppearanceMode.Light;
        DarkAppearanceItem.IsChecked = mode == AppearanceMode.Dark;

        if (mode == AppearanceMode.Automatic)
        {
            ApplicationThemeManager.ApplySystemTheme();
            if (IsLoaded && !_watchingSystemTheme)
            {
                SystemThemeWatcher.Watch(this, WindowBackdropType.None);
                _watchingSystemTheme = true;
            }
        }
        else
        {
            if (_watchingSystemTheme)
            {
                SystemThemeWatcher.UnWatch(this);
                _watchingSystemTheme = false;
            }
            ApplicationThemeManager.Apply(mode == AppearanceMode.Light ? ApplicationTheme.Light : ApplicationTheme.Dark,
                WindowBackdropType.None);
        }
    }

    public MainViewModel ViewModel { get; }

    private void EditorRow_DoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (e.OriginalSource is not DependencyObject source) return;
        var gridRow = FindVisualAncestor<DataGridRow>(source);
        var byteRow = FindVisualAncestor<HexByteRowControl>(source);
        if (gridRow?.Item is not HexViewRow row || row.IsGapMarker || byteRow is null ||
            !byteRow.TryGetAddressAt(e.GetPosition(byteRow), out var address) ||
            !HexAddressParser.TryParse(row.Address, out var rowAddress) || !ViewModel.CurrentImage.TryGetByte(address, out _)) return;
        e.Handled = true;
        var edit = ViewModel.Workspace.BeginEditRow(rowAddress, address, ViewModel.ImageView.BytesPerRow);
        var dialog = new EditRowWindow(edit) { Owner = this };
        dialog.ShowDialog();
    }

    private static T? FindVisualAncestor<T>(DependencyObject source) where T : DependencyObject
    {
        for (var current = source; current is not null; current = VisualTreeHelper.GetParent(current))
            if (current is T match) return match;
        return null;
    }

    private void RecordError(string operation, Exception exception)
    {
        var status = ViewModel.EditorStatus;
        var end = status.CurrentAddress is { } start && status.SelectionLength > 1 &&
                  (ulong)start + (ulong)status.SelectionLength - 1 <= uint.MaxValue
            ? (uint?)(start + (uint)status.SelectionLength - 1)
            : null;
        _diagnostics.RecordError(operation, exception, status.CurrentAddress, end);
    }

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

    private void RowWidth8_Click(object sender, RoutedEventArgs e) => ViewModel.ActiveBytesPerRow = 8;
    private void RowWidth16_Click(object sender, RoutedEventArgs e) => ViewModel.ActiveBytesPerRow = 16;
    private void RowWidth32_Click(object sender, RoutedEventArgs e) => ViewModel.ActiveBytesPerRow = 32;

    private void WorkspaceTabs_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!ReferenceEquals(e.Source, WorkspaceTabs)) return;
        ViewModel.ActivateComparison((WorkspaceTabs.SelectedContent as CompareView)?.Workspace);
    }

    private async void CompareImages_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var baseDocument = (WorkspaceTabs.SelectedContent as CompareView)?.Workspace.BaseDocument;
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
                }, token, progress, baseDocument));
            if (comparison is null) return;

            var view = new CompareView(comparison);
            view.StatusRequested += ViewModel.ReportCompareInteraction;
            view.InteractionFailed += (operation, exception, side, rowIndex, address) =>
            {
                _diagnostics.RecordError($"Compare {side} {operation} row {rowIndex}", exception, address, address);
            };
            view.BaseRowActivated += (rowStartAddress, address) =>
            {
                if (comparison.BaseDocument is not { } baseDocument) return;
                try
                {
                    var edit = ViewModel.Workspace.BeginEditRow(baseDocument, rowStartAddress, address,
                        comparison.Comparison.BytesPerRow);
                    var dialog = new EditRowWindow(edit) { Owner = this };
                    dialog.ShowDialog();
                }
                catch (Exception exception)
                {
                    _diagnostics.RecordError("Compare row activation", exception, address, address);
                    ViewModel.ReportCompareInteraction($"Unable to open the editor for Compare address 0x{address:X8}.");
                }
            };
            var tab = new TabItem { Content = view };
            tab.Header = CreateCompareTabHeader(comparison, tab);
            ViewModel.Workspace.RegisterComparisonView(comparison);
            WorkspaceTabs.Items.Add(tab);
            WorkspaceTabs.SelectedItem = tab;
        }
        catch (OperationCanceledException) { ViewModel.ReportFileOperationCancelled("Compare"); }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or FormatException)
        {
            RecordError("Compare", exception);
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
            if (!_files.ApplyMerge(destination, dialog.Preview, dialog.DefaultChoice, dialog.RangeOverrides,
                    () => ConfirmCloseImage(ActiveDocument))) return;
            Title = ViewModel.WindowTitle;
        }
        catch (OperationCanceledException) { ViewModel.ReportFileOperationCancelled("Merge"); }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or FormatException or ArgumentException or InvalidOperationException)
        {
            RecordError("Merge", exception);
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
            RecordError("Open", exception);
            MessageBox.Show(this, exception.Message, "Unable to open file", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private async void Save_Click(object sender, RoutedEventArgs e)
    {
        var document = ActiveDocument;
        try
        {
            if (document.SourcePath is null || document.SourceFormat is null) { await TrySaveAsAsync(document); return; }
            await RunFileOperationAsync((token, progress) => document.SaveAsync(token, progress));
            RefreshSavedDocumentTabs(document);
        }
        catch (OperationCanceledException) { ViewModel.ReportFileOperationCancelled("Save"); }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException or InvalidOperationException)
        {
            RecordError("Save", exception);
            MessageBox.Show(this, exception.Message, "Unable to save file", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private async void SaveAs_Click(object sender, RoutedEventArgs e)
    {
        await TrySaveAsAsync(ActiveDocument);
    }

    private EditorDocument ActiveDocument => ViewModel.ActiveDocument;

    private void MoreCommands_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement element && element.ContextMenu is { } menu)
        {
            menu.PlacementTarget = element;
            menu.IsOpen = true;
        }
    }

    private void PrimaryToolBarTray_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        var width = e.NewSize.Width;
        ViewToolBar.Visibility = width < 900 ? Visibility.Collapsed : Visibility.Visible;
        HistoryToolBar.Visibility = width < 760 ? Visibility.Collapsed : Visibility.Visible;
        CompareToolBar.Visibility = width < 650 ? Visibility.Collapsed : Visibility.Visible;
    }

    private void MainWindow_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if ((Keyboard.Modifiers & ModifierKeys.Control) == 0) return;
        var key = e.Key == Key.System ? e.SystemKey : e.Key;
        var typing = Keyboard.FocusedElement is System.Windows.Controls.Primitives.TextBoxBase;
        if (typing && key is not (Key.O or Key.S or Key.W)) return;

        switch (key)
        {
            case Key.O when (Keyboard.Modifiers & ModifierKeys.Shift) == 0:
                OpenImage_Click(this, new RoutedEventArgs());
                break;
            case Key.S when (Keyboard.Modifiers & ModifierKeys.Shift) != 0:
                SaveAs_Click(this, new RoutedEventArgs());
                break;
            case Key.S:
                Save_Click(this, new RoutedEventArgs());
                break;
            case Key.W:
                CloseImage_Click(this, new RoutedEventArgs());
                break;
            case Key.Z:
                Undo_Click(this, new RoutedEventArgs());
                break;
            case Key.Y:
                Redo_Click(this, new RoutedEventArgs());
                break;
            case Key.G:
                AddressInput.Focus();
                AddressInput.SelectAll();
                break;
            default:
                return;
        }
        e.Handled = true;
    }

    private async Task<bool> TrySaveAsAsync(EditorDocument document)
    {
        if (!TryGetSaveAsSelection(document, out var selection)) return false;
        try
        {
            await RunFileOperationAsync((token, progress) => document.SaveAsAsync(selection.Path, selection.Format, selection.Overwrite,
                token, progress));
            ViewModel.RefreshActiveCommandState();
            RefreshSavedDocumentTabs(document);
            return true;
        }
        catch (OperationCanceledException) { ViewModel.ReportFileOperationCancelled("Save"); return false; }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException)
        {
            RecordError("Save as", exception);
            MessageBox.Show(this, exception.Message, "Unable to save file", MessageBoxButton.OK, MessageBoxImage.Error);
            return false;
        }
    }

    private bool TrySaveAs(EditorDocument document)
    {
        if (!TryGetSaveAsSelection(document, out var selection)) return false;
        try
        {
            document.SaveAs(selection.Path, selection.Format, selection.Overwrite);
            ViewModel.RefreshActiveCommandState();
            RefreshSavedDocumentTabs(document);
            return true;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException)
        {
            RecordError("Save as", exception);
            MessageBox.Show(this, exception.Message, "Unable to save file", MessageBoxButton.OK, MessageBoxImage.Error);
            return false;
        }
    }

    private bool TryGetSaveAsSelection(EditorDocument document, out SaveAsSelection selection)
    {
        selection = default;
        var dialog = new Microsoft.Win32.SaveFileDialog
        {
            Title = "Save memory image as",
            Filter = "Intel HEX (*.hex)|*.hex|Motorola S-Record (*.s19)|*.s19",
            FilterIndex = document.SourceFormat == MemoryImageExportFormat.SRecord ? 2 : 1,
            FileName = document.SourcePath is null ? string.Empty : System.IO.Path.GetFileName(document.SourcePath),
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

    private object CreateCompareTabHeader(CompareWorkspaceTabViewModel comparison, TabItem tab)
    {
        var header = new StackPanel { Orientation = Orientation.Horizontal };
        header.Children.Add(new TextBlock { Text = $"Compare: {comparison.NameA} ↔ {comparison.NameB}", VerticalAlignment = VerticalAlignment.Center });
        var close = new Button { Content = "×", Padding = new Thickness(5, 0, 5, 0), Margin = new Thickness(8, 0, 0, 0), ToolTip = "Close this comparison view" };
        if (tab is not null) close.Click += (_, _) => CloseCompareView(tab);
        header.Children.Add(close);
        return header;
    }

    private void CloseCompareView(TabItem tab)
    {
        if (tab.Content is not CompareView view || view.Workspace.BaseDocument is not { } document) return;
        if (ViewModel.Workspace.IsLastView(document, view.Workspace) && !ConfirmCloseImage(document)) return;
        ViewModel.Workspace.CloseView(view.Workspace);
        var wasSelected = ReferenceEquals(WorkspaceTabs.SelectedItem, tab);
        WorkspaceTabs.Items.Remove(tab);
        if (wasSelected && WorkspaceTabs.SelectedItem is null && WorkspaceTabs.Items.Count > 0)
            WorkspaceTabs.SelectedIndex = 0;
    }

    private void CloseDocumentViews(EditorDocument document)
    {
        var views = ViewModel.Workspace.CloseImage(document);
        foreach (var tab in WorkspaceTabs.Items.OfType<TabItem>().Where(item => item.Content is CompareView view &&
                     views.Contains(view.Workspace)).ToArray())
            WorkspaceTabs.Items.Remove(tab);
    }

    private void CloseImage_Click(object sender, RoutedEventArgs e)
    {
        var document = ActiveDocument;
        if (!ConfirmCloseImage(document)) return;
        CloseDocumentViews(document);
        if (ReferenceEquals(document, ViewModel.Workspace.Document)) ViewModel.CloseImage();
        Title = ViewModel.WindowTitle;
    }

    private void RefreshSavedDocumentTabs(EditorDocument document)
    {
        foreach (var tab in WorkspaceTabs.Items.OfType<TabItem>())
        {
            if (tab.Content is not CompareView view || !ReferenceEquals(view.Workspace.BaseDocument, document)) continue;
            tab.Header = CreateCompareTabHeader(view.Workspace, tab);
        }
        if (ReferenceEquals(document, ViewModel.Workspace.Document)) Title = ViewModel.WindowTitle;
    }

    private void Undo_Click(object sender, RoutedEventArgs e)
    {
        ViewModel.Undo();
        if (ReferenceEquals(ActiveDocument, ViewModel.Workspace.Document)) Title = ViewModel.WindowTitle;
    }

    private void Redo_Click(object sender, RoutedEventArgs e)
    {
        ViewModel.Redo();
        if (ReferenceEquals(ActiveDocument, ViewModel.Workspace.Document)) Title = ViewModel.WindowTitle;
    }

    private void EditInsert_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var (start, end) = GetSelectionDefaults();
            var dialog = new RangeEditDialog(RangeEditDialogKind.Insert, start, end) { Owner = this };
            if (dialog.ShowDialog() != true || dialog.Result is not { } input) return;
            _imageEdits.Insert(input.Start, input.Pattern!, AskOverlap);
            Title = ViewModel.WindowTitle;
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException or OverflowException)
        {
            RecordError("Insert", exception);
            ShowEditError(exception);
        }
    }

    private void Fill_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var (start, end) = GetSelectionDefaults();
            var dialog = new RangeEditDialog(RangeEditDialogKind.Fill, start, end) { Owner = this };
            if (dialog.ShowDialog() != true || dialog.Result is not { } input) return;
            ViewModel.FillRange(input.Start, input.End, input.Pattern!);
            Title = ViewModel.WindowTitle;
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException or OverflowException)
        {
            RecordError("Fill", exception);
            ShowEditError(exception);
        }
    }

    private void CalculateChecksum_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var dialog = new ChecksumDialog(ViewModel, AskOverlap) { Owner = this };
            dialog.ShowDialog();
            Title = ViewModel.WindowTitle;
        }
        catch (Exception exception)
        {
            RecordError("Open checksum dialog", exception);
            MessageBox.Show(this, exception.Message, "Unable to open checksum dialog", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void Copy_Click(object sender, RoutedEventArgs e) => RunSelectedRangeEdit((start, end) => _imageEdits.Copy(start, end));
    private void Cut_Click(object sender, RoutedEventArgs e) => RunSelectedRangeEdit((start, end) => _imageEdits.Cut(start, end));
    private void Delete_Click(object sender, RoutedEventArgs e) => RunSelectedRangeEdit((start, end) => _imageEdits.Delete(start, end));

    private void Paste_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var (address, _) = GetSelectionDefaults();
            _imageEdits.Paste(address, AskOverlap);
            Title = ViewModel.WindowTitle;
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException or OverflowException)
        {
            RecordError("Paste", exception);
            ShowEditError(exception);
        }
    }

    private void RunRangeEdit(Action action)
    {
        try { action(); Title = ViewModel.WindowTitle; }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException or OverflowException)
        {
            RecordError("Edit selection", exception);
            ShowEditError(exception);
        }
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
        e.Cancel = !ConfirmCloseImage(ActiveDocument);
    }

    private bool ConfirmCloseImage(EditorDocument document)
    {
        if (!document.IsModified) return true;
        var choice = MessageBox.Show(this, "Save changes to this memory image before closing?", "Unsaved changes", MessageBoxButton.YesNoCancel, MessageBoxImage.Warning);
        if (choice == MessageBoxResult.Cancel) return false;
        if (choice == MessageBoxResult.No) return true;
        if (document.SourcePath is null || document.SourceFormat is null) return TrySaveAs(document);
        try
        {
            document.Save();
            RefreshSavedDocumentTabs(document);
            return true;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException or InvalidOperationException)
        {
            RecordError("Save before close", exception);
            MessageBox.Show(this, exception.Message, "Unable to save file", MessageBoxButton.OK, MessageBoxImage.Error);
            return false;
        }
    }

    private void GoToAddress_Click(object sender, RoutedEventArgs e)
    {
        if (!HexAddressParser.TryParse(AddressInput.Text, out var address))
        {
            MessageBox.Show(this, "Enter an absolute 32-bit hexadecimal address in the image's displayed range.",
                "Address not found", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        if (WorkspaceTabs.SelectedContent is CompareView compareView)
        {
            if (!compareView.NavigateToAddress(address))
            {
                MessageBox.Show(this, "Enter an absolute 32-bit hexadecimal address in the comparison range.", "Address not found", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            return;
        }
        if (!ViewModel.TryNavigateToAddress(address, out var rowIndex))
        {
            MessageBox.Show(this, "Enter an absolute 32-bit hexadecimal address in the image's displayed range.", "Address not found", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        SelectRow(rowIndex);
        ViewModel.EditorStatus.SetSelection(address, address);
        _selectionStart = _selectionEnd = address;
    }

    private void SelectRange_Click(object sender, RoutedEventArgs e)
    {
        var (start, end) = GetSelectionDefaults();
        var dialog = new RangeEditDialog(RangeEditDialogKind.SelectRange, start, end) { Owner = this };
        if (dialog.ShowDialog() != true || dialog.Result is not { } result) return;
        ApplySelection(result.Start, result.End);
    }

    private async void ExportImage_Click(object sender, RoutedEventArgs e) => await ExportToFileAsync(ExportScope.EntireImage);
    private async void ExportSegments_Click(object sender, RoutedEventArgs e) => await ExportToFileAsync(ExportScope.SelectedSegments);
    private async void ExportRange_Click(object sender, RoutedEventArgs e) => await ExportToFileAsync(ExportScope.AddressRange);

    private async Task ExportToFileAsync(ExportScope initialScope)
    {
        var compareView = WorkspaceTabs.SelectedContent as CompareView;
        var document = compareView?.Workspace.BaseDocument ?? ViewModel.Workspace.Document;
        var exportSegments = document.CurrentImage.Segments.Select(segment => new MemorySegmentRow(
            segment.StartAddress.ToString("X8"), segment.EndAddress.ToString("X8"), segment.ByteCount)).ToArray();
        var selectedSegments = compareView is null
            ? SegmentsGrid.SelectedItems.Cast<MemorySegmentRow>().Select(ViewModel.Segments.IndexOf).ToArray()
            : compareView.SelectedBaseSegmentIndexes;
        var dialog = new ExportOptionsDialog(exportSegments, selectedSegments,
            _exportSettings.MaxDataBytesPerRecord, initialScope) { Owner = this };
        if (dialog.ShowDialog() != true || dialog.Options is not { } options) return;

        try
        {
            _exportSettings.SetMaxDataBytesPerRecord(options.MaxDataBytesPerRecord);
            await RunFileOperationAsync((token, progress) => Task.Run(() =>
                _files.Export(options.Path, (output, operationToken, operationProgress) =>
                {
                    switch (options.Scope)
                    {
                        case ExportScope.EntireImage:
                            _files.ExportImage(options.Format, options.MaxDataBytesPerRecord, output, operationToken, operationProgress, document);
                            break;
                        case ExportScope.SelectedSegments:
                            _files.ExportSegments(options.SegmentIndexes, options.Format, options.MaxDataBytesPerRecord, output, operationToken, operationProgress, document);
                            break;
                        case ExportScope.AddressRange:
                            _files.ExportAddressRange(options.RangeStart, options.RangeEnd, options.Format, options.MaxDataBytesPerRecord, output, operationToken, operationProgress, document);
                            break;
                        default:
                            throw new ArgumentOutOfRangeException(nameof(options.Scope));
                    }
                }, token, progress), token));
            ViewModel.ReportExported(System.IO.Path.GetFileName(options.Path));
        }
        catch (OperationCanceledException) { ViewModel.ReportFileOperationCancelled("Export"); }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException or InvalidOperationException)
        {
            RecordError("Export", exception);
            MessageBox.Show(this, exception.Message, "Unable to export selected data", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void RelocateSegment_Click(object sender, RoutedEventArgs e)
    {
        var segmentIndex = SegmentsGrid.SelectedIndex;
        if ((uint)segmentIndex >= (uint)ViewModel.CurrentImage.Segments.Count)
        {
            MessageBox.Show(this, "Select an occupied segment to relocate.", "No segment selected",
                MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var segment = ViewModel.CurrentImage.Segments[segmentIndex];
        var dialog = new RelocateSegmentDialog(segment.StartAddress, segment.EndAddress, segment.ByteCount) { Owner = this };
        if (dialog.ShowDialog() != true) return;

        try
        {
            if (_imageEdits.Relocate(segmentIndex, dialog.DestinationAddress, AskRelocationOverlap) &&
                ViewModel.TryNavigateToAddress(dialog.DestinationAddress, out var rowIndex))
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
                      "Overwrite these occupied destination bytes? Declining cancels the relocation.";
        var choice = ShowYesNoCancelWarning(message, "Overlapping destination data");
        return choice switch
        {
            YesNoCancelChoice.Yes => RelocationOverlapDecision.Overwrite,
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
        ViewModel.SelectedSegmentIndex = SegmentsGrid.SelectedIndex;
        if (SegmentsGrid.SelectedIndex >= 0 &&
            ViewModel.TryNavigateToSegment(SegmentsGrid.SelectedIndex, out var rowIndex))
        {
            SelectRow(rowIndex);
        }
    }

    private void SegmentsGrid_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (e.OriginalSource is not DependencyObject source ||
            ItemsControl.ContainerFromElement(SegmentsGrid, source) is not DataGridRow)
        {
            return;
        }

        RelocateSegment_Click(sender, e);
    }

    private void HexView_AddressRangeSelected(object? sender, AddressRangeSelectedEventArgs e)
    {
        ApplySelection(e.StartAddress, e.EndAddress);
    }

    private (uint Start, uint End) GetSelectionDefaults()
    {
        var address = ViewModel.EditorStatus.CurrentAddress ?? 0;
        return (_selectionStart ?? address, _selectionEnd ?? address);
    }

    private void ApplySelection(uint start, uint end)
    {
        _selectionStart = start;
        _selectionEnd = end;
        ViewModel.EditorStatus.SetSelection(start, end);
        if (ViewModel.ImageView.TryNavigateToAddress(start, out var rowIndex)) SelectRow(rowIndex);
    }

    private void RunSelectedRangeEdit(Action<uint, uint> action)
    {
        var (start, end) = GetSelectionDefaults();
        var dialog = new RangeEditDialog(RangeEditDialogKind.SelectRange, start, end) { Owner = this };
        if (dialog.ShowDialog() != true || dialog.Result is not { } range) return;
        RunRangeEdit(() => action(range.Start, range.End));
    }

    private void SelectRow(int rowIndex)
    {
        HexRowsView.ScrollToRow(rowIndex);
    }
}
