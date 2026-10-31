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
using MessageBox = HexEditor.UI.ThemedMessageBox;

namespace HexEditor.UI;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : Window
{
    private readonly AppearanceSettings _appearanceSettings = new();
    private readonly WindowsAppearanceSource _windowsAppearance;
    private readonly AppearanceSession _appearanceSession;
    private readonly ExportSettings _exportSettings = new();
    private readonly UserDataDiagnostics _diagnostics = new();
    private readonly ImageEditWorkflow _imageEdits;
    private readonly ChecksumWorkflow _checksums;
    private readonly ComparisonImageLoader _comparisonImageLoader;
    private readonly CompareCurrentImageWorkflow _compareWorkflow;
    private readonly MemoryImageFileWorkflow _files;
    private CancellationTokenSource? _fileOperationCancellation;
    private CancellationTokenSource? _findCancellation;
    private FindBytesWindow? _findWindow;
    private FindPatternMode? _lastFindMode;
    private string? _lastFindDraft;
    private int _findGeneration;
    private (ComparePanelSide Side, uint Start, uint End)? _compareFindMatch;
    private uint? _selectionStart;
    private uint? _selectionEnd;
    private bool _suppressSegmentNavigation;

    public MainWindow()
    {
        InitializeComponent();
        ViewModel = new MainViewModel();
        ViewModel.ImageChanged += (_, _) => InvalidateFind("Image changed. Search again.");
        ViewModel.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(MainViewModel.WindowTitle)) Title = ViewModel.WindowTitle;
        };
        _imageEdits = new ImageEditWorkflow(ViewModel);
        _checksums = new ChecksumWorkflow(ViewModel);
        _comparisonImageLoader = new ComparisonImageLoader(ViewModel);
        _compareWorkflow = new CompareCurrentImageWorkflow(ViewModel);
        _files = new MemoryImageFileWorkflow(ViewModel);
        HexRowsView.ItemsGrid.AddHandler(Control.MouseDoubleClickEvent, new MouseButtonEventHandler(EditorRow_DoubleClick), handledEventsToo: true);
        HexRowsView.WindowedByteDoubleClicked += EditorWindowedByte_DoubleClick;
        HexRowsView.AddressRowSelected += Editor_AddressRowSelected;
        DataContext = this;
        Closing += MainWindow_Closing;
        _windowsAppearance = new WindowsAppearanceSource(this);
        _appearanceSession = new AppearanceSession(_appearanceSettings, _windowsAppearance);
        _appearanceSession.ThemeChanged += _ => ApplyAppearance();
        Loaded += (_, _) => _appearanceSession.Refresh();
        Closed += (_, _) =>
        {
            _appearanceSession.Dispose();
            _windowsAppearance.Dispose();
        };
        ApplyAppearance();
    }

    private void Preferences_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var choices = new PreferencesSession(_appearanceSession, GridSpacingSettings.Current);
            if (new PreferencesWindow(choices) { Owner = this }.ShowDialog() != true) return;
            choices.Save();
            ApplyAppearance();
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or AggregateException)
        {
            RecordError("Save preferences", exception);
            MessageBox.Show(this, exception.Message, "Unable to save preferences", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void ApplyAppearance()
    {
        ApplicationAppearance.Apply(_appearanceSession.EffectiveTheme);
    }

    public MainViewModel ViewModel { get; }

    private void EditorRow_DoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (!ViewModel.CanEdit) return;
        if (e.OriginalSource is not DependencyObject source) return;
        var gridRow = FindVisualAncestor<DataGridRow>(source);
        var byteRow = FindVisualAncestor<HexByteRowControl>(source);
        var row = byteRow?.Row ?? gridRow?.Item as HexViewRow;
        if (row is null || row.IsGapMarker || byteRow is null ||
            !byteRow.TryGetAddressAt(e.GetPosition(byteRow), out var address) ||
            !HexAddressParser.TryParse(row.Address, out var rowAddress) || !ViewModel.CurrentImage.TryGetByte(address, out _)) return;
        e.Handled = true;
        var edit = ViewModel.Workspace.BeginEditRow(rowAddress, address, ViewModel.ImageView.BytesPerRow);
        var dialog = new EditRowWindow(edit) { Owner = this };
        dialog.ShowDialog();
    }

    private void EditorWindowedByte_DoubleClick(object? sender, WindowedByteDoubleClickEventArgs e)
    {
        if (!ViewModel.CanEdit) return;
        var byteRow = e.Row;
        if (byteRow.Row is not { IsGapMarker: false } row ||
            !byteRow.TryGetAddressAt(e.Position, out var address) ||
            !HexAddressParser.TryParse(row.Address, out var rowAddress) ||
            !ViewModel.CurrentImage.TryGetByte(address, out _)) return;
        e.MouseEvent.Handled = true;
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

    private async Task<T> RunFileOperationAsync<T>(Func<CancellationToken, IProgress<MemoryImageFileProgress>, Task<T>> operation,
        IProgress<MemoryImageFileProgress>? progressObserver = null)
    {
        if (_fileOperationCancellation is not null) throw new InvalidOperationException("A file operation is already running.");
        using var cancellation = new CancellationTokenSource();
        _fileOperationCancellation = cancellation;
        FileOperationPanel.Visibility = Visibility.Visible;
        FileOperationProgress.IsIndeterminate = true;
        FileOperationText.Text = "Working...";
        var progress = new InlineProgress<MemoryImageFileProgress>(update =>
        {
            progressObserver?.Report(update);
            void UpdateProgress()
            {
                var unit = update.Stage.Contains("fill", StringComparison.OrdinalIgnoreCase) ? "addresses" : "bytes";
                FileOperationText.Text = update.Total is { } total
                    ? $"{update.Stage}: {update.Completed:N0} / {total:N0} {unit}"
                    : $"{update.Stage}: {update.Completed:N0}";
                FileOperationProgress.IsIndeterminate = update.Total is null or 0;
                if (update.Total is > 0)
                {
                    FileOperationProgress.Minimum = 0;
                    FileOperationProgress.Maximum = update.Total.Value;
                    FileOperationProgress.Value = Math.Min(update.Completed, update.Total.Value);
                }
            }
            if (Dispatcher.CheckAccess()) UpdateProgress();
            else Dispatcher.BeginInvoke(new Action(UpdateProgress));
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
        InvalidateFind("Active view changed. Search again.");
        ViewModel.ActivateComparison((WorkspaceTabs.SelectedContent as CompareView)?.Workspace);
    }

    private async void CompareImages_Click(object sender, RoutedEventArgs e)
    {
        if (!ViewModel.CanStartOpenCompareOrMerge) return;
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
                }, token, progress, baseDocument,
                mismatch => ThemedMessageBox.ShowOpenAnyway(this, mismatch.Message, "Filename format mismatch")));
            if (comparison is null) return;

            var view = new CompareView(comparison);
            view.StatusRequested += ViewModel.ReportCompareInteraction;
            view.InteractionFailed += (operation, exception, side, rowIndex, address) =>
            {
                _diagnostics.RecordError($"Compare {side} {operation} row {rowIndex}", exception, address, address);
            };
            view.BaseRowActivated += (rowStartAddress, address) =>
            {
                if (!ViewModel.CanEdit) return;
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
        if (!ViewModel.CanStartOpenCompareOrMerge) return;
        try
        {
            var editorDocument = ViewModel.Workspace.Document;
            var editorHasImage = editorDocument.CurrentImage.OccupiedByteCount > 0;
            string? destinationPath = null;
            string sourcePath;
            if (editorHasImage)
            {
                var sourceDialog = FirmwareImageDialog.CreateOpen(
                    "Select the source image to merge",
                    editorDocument.SourcePath is null ? null : System.IO.Path.GetDirectoryName(editorDocument.SourcePath));
                if (sourceDialog.ShowDialog(this) != true) return;
                sourcePath = sourceDialog.FileName;
            }
            else
            {
                var destinationDialog = FirmwareImageDialog.CreateOpen("Select the destination image");
                if (destinationDialog.ShowDialog(this) != true) return;
                destinationPath = destinationDialog.FileName;
                var sourceDialog = FirmwareImageDialog.CreateOpen("Select the source image to merge",
                    System.IO.Path.GetDirectoryName(destinationPath));
                if (sourceDialog.ShowDialog(this) != true) return;
                sourcePath = sourceDialog.FileName;
            }

            var diskImages = editorHasImage
                ? (First: (LoadedMemoryImage?)null, Second: await LoadMergeImageAsync(sourcePath, "Source image"))
                : await LoadMergeImagesAsync(destinationPath!, sourcePath);
            // Always read the selected source from disk. This intentionally handles a source path
            // equal to the Editor path as the saved version rather than its in-memory document.
            var imageA = editorHasImage
                ? new LoadedMemoryImage(editorDocument.CurrentImage, editorDocument.SourcePath, editorDocument.SourceFormat)
                : diskImages.First!;
            var imageB = diskImages.Second;
            var destinationName = editorHasImage
                ? (editorDocument.SourcePath is null ? "Untitled" : System.IO.Path.GetFileName(editorDocument.SourcePath) ?? "Editor")
                : System.IO.Path.GetFileName(destinationPath!) ?? "Destination";
            var sourceName = System.IO.Path.GetFileName(sourcePath) ?? sourcePath;
            var dialog = new MergeWindow(imageA.Image, imageB.Image,
                destinationName, sourceName, fixedDestination: editorHasImage) { Owner = this };
            if (dialog.ShowDialog() != true) return;

            var destination = ReferenceEquals(dialog.DestinationImage, imageA.Image) ? imageA : imageB;
            if (!_files.ApplyMerge(destination, dialog.Preview, dialog.DefaultChoice, dialog.RangeOverrides,
                    () => ConfirmCloseImage(ViewModel.Workspace.Document))) return;
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

    private Task<LoadedMemoryImage> LoadMergeImageAsync(string path, string stage) =>
        RunFileOperationAsync((token, progress) => _comparisonImageLoader.LoadReferenceAsync(
            path, token, AddProgressStage(progress, stage)));

    private Task<(LoadedMemoryImage First, LoadedMemoryImage Second)> LoadMergeImagesAsync(string firstPath, string secondPath) =>
        RunFileOperationAsync(async (token, progress) =>
        {
            var first = await _comparisonImageLoader.LoadReferenceAsync(firstPath, token, AddProgressStage(progress, "Destination image"));
            var second = await _comparisonImageLoader.LoadReferenceAsync(secondPath, token, AddProgressStage(progress, "Source image"));
            return (first, second);
        });

    private static IProgress<MemoryImageFileProgress> AddProgressStage(
        IProgress<MemoryImageFileProgress> progress, string stage) =>
        new Progress<MemoryImageFileProgress>(update =>
            progress.Report(update with { Stage = $"{stage}: {update.Stage}" }));

    /// <summary>Loads a replacement through the window's visible progress and cancellation controls.</summary>
    public Task<MemoryImageImportPreview> LoadOpenReplacementAsync(string path,
        IProgress<MemoryImageFileProgress>? progressObserver = null) =>
        RunFileOperationAsync((token, progress) => ViewModel.PrepareOpenReplacementAsync(path, token, progress), progressObserver);

    private async void OpenImage_Click(object sender, RoutedEventArgs e)
    {
        if (!ViewModel.CanStartOpenCompareOrMerge) return;
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

        var replacedDocument = ViewModel.Workspace.Document;
        if (!ConfirmCloseImage(replacedDocument)) return;

        try
        {
            var preview = await LoadOpenReplacementAsync(dialog.FileName);
            if (!ViewModel.ApplyOpenImport(preview, mismatch =>
                    ThemedMessageBox.ShowOpenAnyway(this, mismatch.Message, "Filename format mismatch")))
                return;
            CloseDocumentViews(replacedDocument);
            ViewModel.ActivateEditor();
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
        if (!ViewModel.CanEdit) return;
        var document = ActiveDocument;
        try
        {
            if (document.SourcePath is null || document.SourceFormat is null) { await TrySaveAsAsync(document); return; }
            await RunFileOperationAsync((token, progress) => document.SaveAsync(token, progress));
            RefreshSavedDocumentTabs(document);
            InvalidateFind("Image saved. Search again.");
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
        if (!ViewModel.CanEdit) return;
        await TrySaveAsAsync(ActiveDocument);
    }

    private EditorDocument ActiveDocument => ViewModel.ActiveDocument;

    private void PrimaryToolBarTray_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        var width = e.NewSize.Width;
        ViewToolBar.Visibility = width < 900 ? Visibility.Collapsed : Visibility.Visible;
        HistoryToolBar.Visibility = width < 760 ? Visibility.Collapsed : Visibility.Visible;
        CompareToolBar.Visibility = width < 650 ? Visibility.Collapsed : Visibility.Visible;
    }

    private void MainWindow_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        var key = e.Key == Key.System ? e.SystemKey : e.Key;
        if (key == Key.F3 && (Keyboard.Modifiers & (ModifierKeys.Control | ModifierKeys.Alt)) == 0)
        {
            RepeatLastFind((Keyboard.Modifiers & ModifierKeys.Shift) != 0);
            e.Handled = true;
            return;
        }
        if ((Keyboard.Modifiers & ModifierKeys.Control) == 0) return;
        var typing = Keyboard.FocusedElement is System.Windows.Controls.Primitives.TextBoxBase;
        if (typing && key is not (Key.O or Key.S or Key.W or Key.G or Key.F)) return;

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
                GoToAddressDialog_Click(this, new RoutedEventArgs());
                break;
            case Key.F:
                FindBytes_Click(this, new RoutedEventArgs());
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
            InvalidateFind("Image saved. Search again.");
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
            InvalidateFind("Image saved. Search again.");
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
        if (!ViewModel.CanEdit) return;
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
        if (!ViewModel.CanEdit) return;
        var document = ActiveDocument;
        if (!ConfirmCloseImage(document)) return;
        CloseDocumentViews(document);
        if (ReferenceEquals(document, ViewModel.Workspace.Document)) ViewModel.CloseImage();
        Title = ViewModel.WindowTitle;
    }

    private void RefreshSavedDocumentTabs(EditorDocument document)
    {
        if (ReferenceEquals(document, ViewModel.Workspace.Document)) ViewModel.RefreshEditedByteMarkers();
        foreach (var tab in WorkspaceTabs.Items.OfType<TabItem>())
        {
            if (tab.Content is not CompareView view || !ReferenceEquals(view.Workspace.BaseDocument, document)) continue;
            tab.Header = CreateCompareTabHeader(view.Workspace, tab);
        }
        if (ReferenceEquals(document, ViewModel.Workspace.Document)) Title = ViewModel.WindowTitle;
    }

    private void Undo_Click(object sender, RoutedEventArgs e)
    {
        if (!ViewModel.CanEdit) return;
        ViewModel.Undo();
        if (ReferenceEquals(ActiveDocument, ViewModel.Workspace.Document)) Title = ViewModel.WindowTitle;
    }

    private void Redo_Click(object sender, RoutedEventArgs e)
    {
        if (!ViewModel.CanEdit) return;
        ViewModel.Redo();
        if (ReferenceEquals(ActiveDocument, ViewModel.Workspace.Document)) Title = ViewModel.WindowTitle;
    }

    private void EditInsert_Click(object sender, RoutedEventArgs e)
    {
        if (!ViewModel.CanEdit) return;
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

    private async void Fill_Click(object sender, RoutedEventArgs e)
    {
        if (!ViewModel.CanEdit) return;
        try
        {
            var (start, end) = GetSelectionDefaults();
            var dialog = new RangeEditDialog(RangeEditDialogKind.Fill, start, end, ViewModel.GetFillAllRegion()) { Owner = this };
            if (dialog.ShowDialog() != true || dialog.Result is not { } input) return;
            await RunFileOperationAsync((token, progress) => ViewModel.FillRangeAsync(
                input.Start, input.End, input.Pattern!, input.FillPolicy, token, progress));
            Title = ViewModel.WindowTitle;
        }
        catch (OperationCanceledException) { ViewModel.ReportFileOperationCancelled("Fill"); }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException or OverflowException)
        {
            RecordError("Fill", exception);
            ShowEditError(exception);
        }
    }

    private void CalculateChecksum_Click(object sender, RoutedEventArgs e)
    {
        if (!ViewModel.CanEdit) return;
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

    private void Copy_Click(object sender, RoutedEventArgs e) { if (ViewModel.CanEdit) RunSelectedRangeEdit((start, end) => _imageEdits.Copy(start, end)); }
    private void Cut_Click(object sender, RoutedEventArgs e) { if (ViewModel.CanEdit) RunSelectedRangeEdit((start, end) => _imageEdits.Cut(start, end)); }
    private void Delete_Click(object sender, RoutedEventArgs e) { if (ViewModel.CanEdit) RunSelectedRangeEdit((start, end) => _imageEdits.Delete(start, end)); }

    private void Paste_Click(object sender, RoutedEventArgs e)
    {
        if (!ViewModel.CanEdit) return;
        try
        {
            var (address, _) = GetSelectionDefaults();
            _imageEdits.Paste(address, AskPasteOverlap);
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
        if (!ViewModel.CanEdit) return;
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

    private OverlapDecision AskPasteOverlap(long count, string operation)
    {
        var result = ThemedMessageBox.Show(this,
            $"This {operation} overlaps {count:N0} occupied byte(s). Keep existing values or overwrite them?",
            "Resolve overlap", MessageBoxButton.YesNoCancel, MessageBoxImage.Warning);
        return result switch
        {
            MessageBoxResult.Yes => OverlapDecision.KeepExisting,
            MessageBoxResult.No => OverlapDecision.OverwriteIncoming,
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
        e.Cancel = !DirtyDocumentCloseWorkflow.ConfirmAll(ViewModel.Workspace, ConfirmCloseImage);
    }

    private bool ConfirmCloseImage(EditorDocument document)
    {
        if (!document.IsModified) return true;
        var imageName = string.IsNullOrWhiteSpace(document.SourcePath)
            ? "Untitled image"
            : System.IO.Path.GetFileName(document.SourcePath);
        var choice = SaveChangesDialog.Show(this, imageName);
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

    private void GoToAddressDialog_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new GoToAddressDialog { Owner = this };
        if (dialog.ShowDialog() != true || dialog.Address is not { } address) return;
        NavigateToAddress(address);
    }

    private void FindBytes_Click(object sender, RoutedEventArgs e)
    {
        if (_findWindow is { IsVisible: true } existing)
        {
            existing.Activate();
            existing.PatternInput.Focus();
            return;
        }

        var window = new FindBytesWindow { Owner = this };
        if (_lastFindMode is { } mode && _lastFindDraft is { } draft)
            window.SetDraft(mode, draft);
        window.FindRequested += FindWindow_FindRequested;
        window.CancelRequested += (_, _) => CancelFind();
        window.Closed += (_, _) =>
        {
            CancelFind();
            _findWindow = null;
            ViewModel.ImageView.ClearFindMatch();
            foreach (var compare in WorkspaceTabs.Items.OfType<TabItem>().Select(item => item.Content).OfType<CompareView>())
                compare.ViewModel.ClearFindMatch();
        };
        _findWindow = window;
        window.Show();
        window.Activate();
        window.PatternInput.Focus();
        window.PatternInput.SelectAll();
    }

    private async void FindWindow_FindRequested(object? sender, FindRequestedEventArgs e)
    {
        var window = _findWindow;
        var compareView = WorkspaceTabs.SelectedContent as CompareView;
        if (!ViewModel.ActiveIsEditor && compareView is null) return;
        if (_findCancellation is not null)
        {
            CancelFind();
            return;
        }

        var selectionStart = ViewModel.EditorStatus.CurrentAddress;
        uint? selectionEnd = selectionStart is uint start && ViewModel.EditorStatus.SelectionLength > 0 &&
            (ulong)start + (ulong)ViewModel.EditorStatus.SelectionLength - 1 <= uint.MaxValue
            ? (uint)((ulong)start + (ulong)ViewModel.EditorStatus.SelectionLength - 1)
            : null;

        using var cancellation = new CancellationTokenSource();
        _findCancellation = cancellation;
        var generation = ++_findGeneration;
        var searchedDocument = ViewModel.Workspace.Document;
        var searchedImage = searchedDocument.CurrentImage;
        var searchedCompare = compareView?.Workspace;
        window?.SetSearching(true);
        var progress = new InlineProgress<(ulong Completed, ulong Total)>(update =>
        {
            if (window is null) return;
            _ = Dispatcher.InvokeAsync(() =>
            {
                if (ReferenceEquals(_findCancellation, cancellation) && !cancellation.IsCancellationRequested)
                    window.SetProgress(update.Completed, update.Total);
            });
        });
        try
        {
            if (searchedCompare is not null)
            {
                var current = _compareFindMatch is { } last
                    ? (last.Side, last.Start)
                    : compareView!.CurrentFindCursor;
                var forwardAInclusive = current is { } position && ComparePanelSide.Base > position.Side;
                var forwardBInclusive = current is { } positionB && ComparePanelSide.Reference > positionB.Side;
                var previousAInclusive = current is { } positionC && ComparePanelSide.Base < positionC.Side;
                var previousBInclusive = current is { } positionD && ComparePanelSide.Reference < positionD.Side;
                var scan = await Task.Run(() =>
                {
                    var a = EditorFindSearch.FindCandidate(searchedCompare.Comparison.FileA, e.Draft, e.Mode,
                        current?.Item2, e.Forward, e.Forward ? forwardAInclusive : previousAInclusive, cancellation.Token, progress);
                    var b = EditorFindSearch.FindCandidate(searchedCompare.Comparison.FileB, e.Draft, e.Mode,
                        current?.Item2, e.Forward, e.Forward ? forwardBInclusive : previousBInclusive, cancellation.Token, progress);
                    var eligible = new List<(ComparePanelSide Side, uint Start, uint End)>();
                    if (a.IsValidPattern && a.MatchAddress is uint addressA) eligible.Add((ComparePanelSide.Base, addressA, a.MatchEndAddress!.Value));
                    if (b.IsValidPattern && b.MatchAddress is uint addressB) eligible.Add((ComparePanelSide.Reference, addressB, b.MatchEndAddress!.Value));
                    if (a.IsValidPattern && eligible.Count == 0 && current is not null)
                    {
                        a = EditorFindSearch.FindCandidate(searchedCompare.Comparison.FileA, e.Draft, e.Mode, null,
                            e.Forward, inclusive: false, cancellation.Token, progress);
                        b = EditorFindSearch.FindCandidate(searchedCompare.Comparison.FileB, e.Draft, e.Mode, null,
                            e.Forward, inclusive: false, cancellation.Token, progress);
                        if (a.MatchAddress is uint wrappedA) eligible.Add((ComparePanelSide.Base, wrappedA, a.MatchEndAddress!.Value));
                        if (b.MatchAddress is uint wrappedB) eligible.Add((ComparePanelSide.Reference, wrappedB, b.MatchEndAddress!.Value));
                    }
                    var selected = e.Forward
                        ? eligible.OrderBy(match => match.Start).ThenBy(match => match.Side).Select(match => ((ComparePanelSide Side, uint Start, uint End)?)match).FirstOrDefault()
                        : eligible.OrderByDescending(match => match.Start).ThenBy(match => match.Side).Select(match => ((ComparePanelSide Side, uint Start, uint End)?)match).FirstOrDefault();
                    return (A: a, B: b, Match: selected);
                }, cancellation.Token);
                await RunOnUiAsync(() => ApplyCompareFindResult(window, e, scan.A, scan.B, scan.Match, searchedCompare, generation));
            }
            else
            {
                var result = e.Forward
                    ? await ViewModel.Workspace.FindNextAsync(e.Draft, e.Mode, selectionStart, selectionEnd, cancellation.Token, progress)
                    : await ViewModel.Workspace.FindPreviousAsync(e.Draft, e.Mode, selectionStart, selectionEnd, cancellation.Token, progress);
                await RunOnUiAsync(() => ApplyFindResult(window, e, result, searchedDocument, searchedImage, generation));
            }
        }
        catch (OperationCanceledException)
        {
            await RunOnUiAsync(() => SetFindStatus(window, "Search cancelled."));
        }
        finally
        {
            await RunOnUiAsync(() =>
            {
                if (ReferenceEquals(_findCancellation, cancellation)) _findCancellation = null;
                window?.SetSearching(false);
            });
        }
    }

    private void RepeatLastFind(bool previous)
    {
        if (!ViewModel.ActiveIsEditor && WorkspaceTabs.SelectedContent is not CompareView) return;
        if (_findWindow is { IsVisible: true } window)
        {
            FindWindow_FindRequested(window, new FindRequestedEventArgs(window.Mode, window.CurrentDraft, !previous));
            return;
        }
        if (_lastFindMode is not { } mode || _lastFindDraft is not { } draft) return;
        FindWindow_FindRequested(null, new FindRequestedEventArgs(mode, draft, !previous));
    }

    private void InvalidateFind(string status)
    {
        CancelFind();
        ViewModel.ImageView.ClearFindMatch();
            foreach (var compare in WorkspaceTabs.Items.OfType<TabItem>().Select(item => item.Content).OfType<CompareView>())
                compare.ViewModel.ClearFindMatch();
        _findWindow?.SetStatus(status);
    }

    private void ApplyCompareFindResult(FindBytesWindow? window, FindRequestedEventArgs request,
        FindSearchResult resultA, FindSearchResult resultB, (ComparePanelSide Side, uint Start, uint End)? match,
        CompareWorkspaceTabViewModel comparison, int generation)
    {
        if (generation != _findGeneration || !ReferenceEquals((WorkspaceTabs.SelectedContent as CompareView)?.Workspace, comparison)) return;
        if (!resultA.IsValidPattern) { SetFindStatus(window, resultA.Message ?? "Invalid pattern."); return; }
        _lastFindMode = request.Mode;
        _lastFindDraft = request.Draft;
        if (match is not { } result) { SetFindStatus(window, resultB.Message ?? resultA.Message ?? "No match."); return; }
        _compareFindMatch = result;
        (WorkspaceTabs.SelectedContent as CompareView)?.NavigateToFindMatch(result.Side, result.Start, result.End);
        SetFindStatus(window, $"Found in {(result.Side == ComparePanelSide.Base ? "A" : "B")} at 0x{result.Start:X8}–0x{result.End:X8}.");
    }

    private void CancelFind()
    {
        _findGeneration++;
        _findCancellation?.Cancel();
    }

    private void SetFindStatus(FindBytesWindow? window, string message)
    {
        if (window is { IsVisible: true }) window.SetStatus(message);
        else ViewModel.ReportCompareInteraction(message);
    }

    private void ApplyFindResult(FindBytesWindow? window, FindRequestedEventArgs request, FindSearchResult result,
        EditorDocument document, MemoryImage image, int generation)
    {
        if (generation != _findGeneration || !ReferenceEquals(document, ViewModel.Workspace.Document) ||
            !ReferenceEquals(image, document.CurrentImage)) return;
        if (!result.IsValidPattern)
        {
            SetFindStatus(window, result.Message ?? "Invalid pattern.");
            return;
        }

        _lastFindMode = request.Mode;
        _lastFindDraft = request.Draft;
        if (result.MatchAddress is not uint matchStart || result.MatchEndAddress is not uint matchEnd)
        {
            SetFindStatus(window, result.Message ?? "No match.");
            return;
        }

        ViewModel.ImageView.SetFindMatch(matchStart, matchEnd);
        ApplySelection(matchStart, matchEnd, scrollToMatch: true);
        SetFindStatus(window, $"Found at 0x{matchStart:X8}–0x{matchEnd:X8}.");
    }

    private Task RunOnUiAsync(Action action)
    {
        if (Dispatcher.CheckAccess())
        {
            action();
            return Task.CompletedTask;
        }
        return Dispatcher.InvokeAsync(action).Task;
    }

    private sealed class InlineProgress<T>(Action<T> report) : IProgress<T>
    {
        public void Report(T value) => report(value);
    }

    private void NavigateToAddress(uint address)
    {
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
        var exportSegments = document.CurrentImage.Ranges.Select((range, index) => new MemorySegmentRow(
            range.StartAddress.ToString("X8"), range.EndAddress.ToString("X8"), checked((long)range.ByteCount), index + 1)).ToArray();
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
        if (!ViewModel.CanEdit) return;
        var rangeIndex = SegmentsGrid.SelectedIndex;
        if ((uint)rangeIndex >= (uint)ViewModel.CurrentImage.Blocks.Count)
        {
            MessageBox.Show(this, "Select an occupied segment to relocate.", "No segment selected",
                MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var block = ViewModel.CurrentImage.Blocks[rangeIndex];
        var dialog = new RelocateSegmentDialog(block.StartAddress, block.EndAddress, checked((long)block.ByteCount),
            canDelete: SegmentsGrid.SelectedItems.Count == 1) { Owner = this };
        if (dialog.ShowDialog() != true) return;

        if (dialog.DeleteRequested)
        {
            var nextSegmentIndex = ViewModel.Workspace.DeleteBlock(rangeIndex);
            _suppressSegmentNavigation = true;
            try
            {
                SegmentsGrid.SelectedIndex = nextSegmentIndex;
                ViewModel.SelectedSegmentIndex = nextSegmentIndex;
            }
            finally { _suppressSegmentNavigation = false; }
            return;
        }

        try
        {
            if (_imageEdits.Relocate(rangeIndex, dialog.DestinationAddress, AskRelocationOverlap) &&
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
        if (!_suppressSegmentNavigation && SegmentsGrid.SelectedIndex >= 0 &&
            ViewModel.TryNavigateToSegment(SegmentsGrid.SelectedIndex, out var rowIndex))
        {
            SelectRow(rowIndex);
        }
    }

    private void SegmentsGrid_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (e.OriginalSource is not DependencyObject source ||
            ItemsControl.ContainerFromElement(SegmentsGrid, source) is not ListBoxItem)
        {
            return;
        }

        RelocateSegment_Click(sender, e);
    }

    private void HexView_AddressRangeSelected(object? sender, AddressRangeSelectedEventArgs e)
    {
        HexRowsView.SetSelectedRow(-1);
        SetSelection(e.StartAddress, e.EndAddress);
    }

    private void Editor_AddressRowSelected(object? sender, AddressRowSelectedEventArgs e)
    {
        if (e.Row.IsGapMarker)
        {
            var currentAddress = ViewModel.EditorStatus.CurrentAddress;
            _selectionStart = _selectionEnd = currentAddress;
            if (currentAddress is { } selectedAddress)
                ViewModel.EditorStatus.SetSelection(selectedAddress, selectedAddress);
            return;
        }
        if (!HexAddressParser.TryParse(e.Row.Address, out var address)) return;
        _selectionStart = _selectionEnd = address;
        ViewModel.EditorStatus.SetSelection(address, address);
    }

    private (uint Start, uint End) GetSelectionDefaults()
    {
        var address = ViewModel.EditorStatus.CurrentAddress ?? 0;
        return (_selectionStart ?? address, _selectionEnd ?? address);
    }

    private void ApplySelection(uint start, uint end, bool scrollToMatch = false)
    {
        SetSelection(start, end);
        if (!ViewModel.ImageView.TryNavigateToAddress(start, out var rowIndex)) return;
        if (scrollToMatch) HexRowsView.ScrollToMatch(rowIndex);
        else SelectRow(rowIndex);
    }

    private void SetSelection(uint start, uint end)
    {
        HexRowsView.SetSelectedRow(-1);
        _selectionStart = start;
        _selectionEnd = end;
        ViewModel.EditorStatus.SetSelection(start, end);
    }

    private void RunSelectedRangeEdit(Action<uint, uint> action)
    {
        if (!ViewModel.CanEdit) return;
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
