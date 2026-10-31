using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using HexEditor.Core;
using HexEditor.Formats;
using HexEditor.Tools;

namespace HexEditor.UI;

public sealed class MainViewModel : INotifyPropertyChanged
{
    private readonly MemoryImageFileService _fileService = new();
    private HexImageViewModel _imageView = new(MemoryImage.Empty);
    private string _statusMessage = "Open an Intel HEX or Motorola S-Record file to inspect its occupied memory segments.";
    private string _windowTitle = "HEX Editor";
    private CompareViewModel? _activeComparison;
    private EditorDocument? _activeComparisonDocument;
    private CompareWorkspaceTabViewModel? _activeComparisonWorkspace;
    private int _selectedSegmentIndex = -1;
    private bool _isOpenReplacementLoading;

    public MainViewModel(MemoryImage? image = null)
    {
        Workspace.ImageChanged += OnEditingImageChanged;
        EditorStatus.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(EditorStatusViewModel.SelectionLength))
                Raise(nameof(ActiveHasSelection), nameof(ActiveCanPaste));
        };
        if (image is not null)
        {
            Workspace.OpenImage(image, null, null);
            RefreshImage();
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    public event Action<MemoryImage, MemoryImage>? ImageChanged;

    public EditorStatusViewModel EditorStatus { get; } = new();
    public ChecksumViewModel Checksum { get; } = new();
    public EditorDocumentWorkspace Workspace { get; } = new();
    public ImageEditingSession Editing => Workspace.Document.Editing;

    public ObservableCollection<MemorySegmentRow> Segments { get; } = new();
    public int[] RowWidths { get; } = [8, 16, 32];
    public int[] ExportPayloadSizes { get; } = [8, 16, 32];
    public MemoryImageExportFormat[] ExportFormats { get; } = Enum.GetValues<MemoryImageExportFormat>();

    public MemoryImage CurrentImage => Workspace.Document.CurrentImage;
    /// <summary>The editable base image for the selected workspace tab.</summary>
    public EditorDocument ActiveDocument => _activeComparisonDocument ?? Workspace.Document;
    public MemoryImage ActiveImage => ActiveDocument.CurrentImage;
    public bool ActiveHasImage => ActiveImage.OccupiedByteCount > 0;
    public bool ActiveIsEditor => _activeComparison is null;
    public bool IsOpenReplacementLoading => _isOpenReplacementLoading;
    public bool CanEdit => !_isOpenReplacementLoading;
    public bool CanStartOpenCompareOrMerge => !_isOpenReplacementLoading;
    public bool ActiveCanSave => CanEdit && !string.IsNullOrWhiteSpace(ActiveDocument.SourcePath) && ActiveDocument.SourceFormat is not null;
    public bool ActiveCanSaveAs => CanEdit && (ActiveHasImage || !string.IsNullOrWhiteSpace(ActiveDocument.SourcePath));
    public bool ActiveCanCloseImage => CanEdit && (ActiveHasImage || !string.IsNullOrWhiteSpace(ActiveDocument.SourcePath) || ActiveDocument.IsModified);
    public bool ActiveHasSelection => _activeComparison is null && EditorStatus.SelectionLength > 0;
    public bool ActiveCanPaste => CanEdit && _activeComparison is null && ActiveHasSelection && Editing.HasClipboard;
    public bool ActiveCanRelocate => CanEdit && _activeComparison is null &&
        (uint)_selectedSegmentIndex < (uint)ActiveImage.Blocks.Count;
    public int SelectedSegmentIndex
    {
        get => _selectedSegmentIndex;
        set
        {
            if (_selectedSegmentIndex == value) return;
            _selectedSegmentIndex = value;
            Raise(nameof(SelectedSegmentIndex), nameof(ActiveCanRelocate));
        }
    }
    public int ActiveBytesPerRow
    {
        get => _activeComparison?.BytesPerRow ?? ImageView.BytesPerRow;
        set { if (_activeComparison is not null) _activeComparison.BytesPerRow = value; else ImageView.BytesPerRow = value; Raise(nameof(ActiveBytesPerRow)); }
    }
    public bool ActiveShowAscii
    {
        get => _activeComparison?.ShowAscii ?? ImageView.ShowAscii;
        set { if (_activeComparison is not null) _activeComparison.ShowAscii = value; else ImageView.ShowAscii = value; Raise(nameof(ActiveShowAscii)); }
    }
    public void ActivateEditor()
    {
        SetActiveComparisonDocument(null);
        if (_activeComparisonWorkspace is not null)
            _activeComparisonWorkspace.PropertyChanged -= OnActiveComparisonWorkspacePropertyChanged;
        _activeComparisonWorkspace = null;
        _activeComparison = null;
        RefreshTitle();
        Raise(nameof(ActiveDocument), nameof(ActiveImage), nameof(ActiveHasImage), nameof(ActiveCanSave),
            nameof(ActiveCanSaveAs), nameof(ActiveCanCloseImage), nameof(ActiveCanRelocate), nameof(ActiveHasSelection),
            nameof(ActiveCanPaste), nameof(ActiveBytesPerRow), nameof(ActiveShowAscii), nameof(ActiveIsEditor));
    }
    public void ActivateComparison(CompareWorkspaceTabViewModel? comparison)
    {
        if (_activeComparisonWorkspace is not null)
            _activeComparisonWorkspace.PropertyChanged -= OnActiveComparisonWorkspacePropertyChanged;
        _activeComparisonWorkspace = comparison;
        if (_activeComparisonWorkspace is not null)
            _activeComparisonWorkspace.PropertyChanged += OnActiveComparisonWorkspacePropertyChanged;
        ActivateComparison(comparison?.Comparison, comparison?.BaseDocument);
    }
    public void ActivateComparison(CompareViewModel? comparison)
    {
        if (_activeComparisonWorkspace is not null)
            _activeComparisonWorkspace.PropertyChanged -= OnActiveComparisonWorkspacePropertyChanged;
        _activeComparisonWorkspace = null;
        ActivateComparison(comparison, null);
    }
    private void ActivateComparison(CompareViewModel? comparison, EditorDocument? document)
    {
        if (_activeComparison is not null) _activeComparison.PropertyChanged -= OnActiveComparisonPropertyChanged;
        _activeComparison = comparison;
        if (_activeComparison is not null) _activeComparison.PropertyChanged += OnActiveComparisonPropertyChanged;
        SetActiveComparisonDocument(document);
        RefreshTitle();
        Raise(nameof(ActiveDocument), nameof(ActiveImage), nameof(ActiveHasImage), nameof(ActiveCanSave),
            nameof(ActiveCanSaveAs), nameof(ActiveCanCloseImage), nameof(ActiveCanRelocate), nameof(ActiveHasSelection),
            nameof(ActiveCanPaste), nameof(ActiveBytesPerRow), nameof(ActiveShowAscii), nameof(ActiveIsEditor));
    }
    private void SetActiveComparisonDocument(EditorDocument? document)
    {
        if (_activeComparisonDocument is not null) _activeComparisonDocument.PropertyChanged -= OnActiveDocumentPropertyChanged;
        _activeComparisonDocument = document;
        if (_activeComparisonDocument is not null) _activeComparisonDocument.PropertyChanged += OnActiveDocumentPropertyChanged;
        Raise(nameof(ActiveCanUndo), nameof(ActiveCanRedo), nameof(ActiveHasImage), nameof(ActiveCanSave),
            nameof(ActiveCanSaveAs), nameof(ActiveCanCloseImage), nameof(ActiveCanRelocate), nameof(ActiveDocument), nameof(ActiveImage));
    }
    private void OnActiveDocumentPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(EditorDocument.CanUndo) or nameof(EditorDocument.CanRedo))
            Raise(nameof(ActiveCanUndo), nameof(ActiveCanRedo));
        if (e.PropertyName is nameof(EditorDocument.CurrentImage) or nameof(EditorDocument.SourcePath) or
            nameof(EditorDocument.SourceFormat) or nameof(EditorDocument.IsModified))
        {
            RefreshTitle();
            Raise(nameof(ActiveImage), nameof(ActiveHasImage), nameof(ActiveCanSave), nameof(ActiveCanSaveAs),
                nameof(ActiveCanCloseImage), nameof(ActiveCanRelocate));
        }
    }
    private void OnActiveComparisonWorkspacePropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(CompareWorkspaceTabViewModel.NameA)) RefreshTitle();
    }
    private void OnActiveComparisonPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(CompareViewModel.BytesPerRow)) Raise(nameof(ActiveBytesPerRow));
        if (e.PropertyName == nameof(CompareViewModel.ShowAscii)) Raise(nameof(ActiveShowAscii));
    }
    public bool TryNavigateActiveToAddress(uint address, out int rowIndex) => _activeComparison is null
        ? TryNavigateToAddress(address, out rowIndex)
        : _activeComparison.TryNavigateToAddress(address, out rowIndex);
    public bool IsModified => Workspace.Document.IsModified;
    public bool CanUndo => Workspace.Document.CanUndo;
    public bool CanRedo => Workspace.Document.CanRedo;
    public bool ActiveCanUndo => CanEdit && ActiveDocument.CanUndo;
    public bool ActiveCanRedo => CanEdit && ActiveDocument.CanRedo;
    public string? SourcePath => Workspace.Document.SourcePath;
    public MemoryImageExportFormat? SourceFormat => Workspace.Document.SourceFormat;
    public MemoryImageFormatMetadata? SourceFormatMetadata => Workspace.Document.SourceFormatMetadata;

    public HexImageViewModel ImageView
    {
        get => _imageView;
        private set => SetProperty(ref _imageView, value);
    }

    public string StatusMessage { get => _statusMessage; private set => SetProperty(ref _statusMessage, value); }
    public string WindowTitle { get => _windowTitle; private set => SetProperty(ref _windowTitle, value); }
    public MemoryImageImportPreview PrepareOpen(string path)
        => Workspace.PrepareOpen(path);

    public Task<MemoryImageImportPreview> PrepareOpenAsync(string path, CancellationToken cancellationToken,
        IProgress<MemoryImageFileProgress>? progress)
        => Workspace.PrepareOpenAsync(path, cancellationToken, progress);

    /// <summary>Prepares an Editor replacement while publishing a read-only loading state to the workspace UI.</summary>
    public async Task<MemoryImageImportPreview> PrepareOpenReplacementAsync(string path, CancellationToken cancellationToken,
        IProgress<MemoryImageFileProgress>? progress)
    {
        if (_isOpenReplacementLoading) throw new InvalidOperationException("An Editor replacement is already loading.");
        SetOpenReplacementLoading(true);
        try
        {
            return await PrepareOpenAsync(path, cancellationToken, progress);
        }
        finally
        {
            SetOpenReplacementLoading(false);
        }
    }

    public void ApplyImport(MemoryImageImportPreview preview, OverlapDecision decision)
    {
        ArgumentNullException.ThrowIfNull(preview);
        var previousImage = CurrentImage;
        if (!Workspace.ApplyImport(preview, decision)) return;

        Checksum.Clear();
        RefreshImage();
        RefreshEditorStatus();
        RefreshTitle();
        StatusMessage = DescribeImage(preview.InputFormat);
        Raise(nameof(CurrentImage), nameof(IsModified), nameof(CanUndo), nameof(CanRedo), nameof(SourcePath), nameof(SourceFormat), nameof(SourceFormatMetadata),
            nameof(ActiveImage), nameof(ActiveHasImage), nameof(ActiveCanSave), nameof(ActiveCanSaveAs), nameof(ActiveCanCloseImage), nameof(ActiveCanRelocate));
        if (!ReferenceEquals(previousImage, CurrentImage)) NotifyImageChanged(previousImage, CurrentImage);
    }

    public bool ApplyOpenImport(MemoryImageImportPreview preview, Func<MemoryImageFormatMismatch, bool> confirmMismatch)
    {
        ArgumentNullException.ThrowIfNull(preview);
        ArgumentNullException.ThrowIfNull(confirmMismatch);
        if (preview.FormatMismatch is { } mismatch && !confirmMismatch(mismatch)) return false;
        ApplyImport(preview, OverlapDecision.OverwriteIncoming);
        return true;
    }

    public void LoadImage(MemoryImage image, string? sourcePath, MemoryImageExportFormat? format)
    {
        ArgumentNullException.ThrowIfNull(image);
        var previousImage = CurrentImage;
        Workspace.OpenImage(image, sourcePath, format);
        Checksum.Clear();
        RefreshImage();
        RefreshEditorStatus();
        RefreshTitle();
        StatusMessage = DescribeImage(format?.ToString());
        Raise(nameof(CurrentImage), nameof(IsModified), nameof(CanUndo), nameof(CanRedo), nameof(SourcePath), nameof(SourceFormat), nameof(SourceFormatMetadata),
            nameof(ActiveImage), nameof(ActiveHasImage), nameof(ActiveCanSave), nameof(ActiveCanSaveAs), nameof(ActiveCanCloseImage), nameof(ActiveCanRelocate));
        if (!ReferenceEquals(previousImage, image)) NotifyImageChanged(previousImage, image);
    }

    public void CloseImage() => LoadImage(MemoryImage.Empty, null, null);

    public void EditByte(uint address, byte value, OverlapDecision? overlapDecision = null) => Workspace.EditByte(address, value, overlapDecision);
    public void InsertBytes(uint address, IReadOnlyList<byte> bytes, OverlapDecision? overlapDecision = null) => Editing.InsertBytes(address, bytes, overlapDecision);
    public MemoryImageMergePreview PreviewMerge(MemoryImage source) => Editing.PreviewMerge(source);
    public void ApplyMerge(MemoryImageMergePreview preview, MemoryImageMergeChoice? defaultChoice,
        IEnumerable<MemoryImageMergeRangeOverride>? rangeOverrides = null) => Editing.ApplyMerge(preview, defaultChoice, rangeOverrides);
    public InsertPreview PreviewInsert(uint address, IReadOnlyList<byte> bytes) => Editing.PreviewInsert(address, bytes);
    public void CopyRange(uint startAddress, uint endAddress)
    {
        var copiedBytes = Editing.CopyRange(startAddress, endAddress);
        Raise(nameof(ActiveCanPaste));
        StatusMessage = $"Copied {copiedBytes:N0} occupied byte(s), preserving address gaps.";
    }
    public void CutRange(uint startAddress, uint endAddress)
    {
        Editing.CutRange(startAddress, endAddress);
        Raise(nameof(ActiveCanPaste));
    }
    public void PasteAt(uint address, OverlapDecision? overlapDecision = null) => Editing.PasteAt(address, overlapDecision);
    public long GetPasteOverlapCount(uint address) => Editing.GetPasteOverlapCount(address);
    public void DeleteRange(uint startAddress, uint endAddress) => Editing.DeleteRange(startAddress, endAddress);
    public (uint Start, uint End)? GetFillAllRegion()
    {
        if (ActiveImage.Ranges.Count == 0) return null;
        return (ActiveImage.Ranges[0].StartAddress, ActiveImage.Ranges[^1].EndAddress);
    }
    public void FillRange(uint startAddress, uint endAddress, IReadOnlyList<byte> pattern,
        FillPolicy policy = FillPolicy.KeepExisting) => Editing.FillRange(startAddress, endAddress, pattern, policy);
    public Task FillRangeAsync(uint startAddress, uint endAddress, IReadOnlyList<byte> pattern, FillPolicy policy,
        CancellationToken cancellationToken, IProgress<MemoryImageFileProgress>? progress) =>
        Editing.FillRangeAsync(startAddress, endAddress, pattern, policy, cancellationToken, progress);

    public ChecksumCalculationResult CalculateChecksum(ChecksumRequest request)
    {
        var result = Checksum.Calculate(CurrentImage, request);
        StatusMessage = $"{result.AlgorithmName}: {result.FormattedValue} over 0x{result.StartAddress:X8}-0x{result.EndAddress:X8}.";
        return result;
    }

    public void WriteChecksumResult(uint address, OverlapDecision? overlapDecision = null)
    {
        var result = Checksum.GetWritableResult();
        Editing.InsertBytes(address, result.ToBytes(), overlapDecision);
    }

    public bool Undo()
    {
        return (_activeComparisonDocument ?? Workspace.Document).Editing.Undo();
    }

    public bool Redo()
    {
        return (_activeComparisonDocument ?? Workspace.Document).Editing.Redo();
    }

    public void Save()
    {
        ActiveDocument.Save();
        CompleteSave(ActiveDocument);
    }

    public async Task SaveAsync(CancellationToken cancellationToken, IProgress<MemoryImageFileProgress>? progress)
    {
        var document = ActiveDocument;
        await document.SaveAsync(cancellationToken, progress);
        if (ReferenceEquals(ActiveDocument, document)) CompleteSave(document);
    }

    public async Task SaveAsAsync(string path, MemoryImageExportFormat format, bool overwriteExisting,
        CancellationToken cancellationToken, IProgress<MemoryImageFileProgress>? progress)
    {
        var document = ActiveDocument;
        await document.SaveAsAsync(path, format, overwriteExisting, cancellationToken, progress);
        if (ReferenceEquals(ActiveDocument, document)) CompleteSave(document);
    }

    public void SaveAs(string path, MemoryImageExportFormat format, bool overwriteExisting)
    {
        ActiveDocument.SaveAs(path, format, overwriteExisting);
        CompleteSave(ActiveDocument);
    }

    private void CompleteSave(EditorDocument document)
    {
        if (ReferenceEquals(document, Workspace.Document)) RefreshImage(clearSelection: false);
        if (ReferenceEquals(document, Workspace.Document)) RefreshTitle();
        StatusMessage = $"Saved {document.CurrentImage.OccupiedByteCount:N0} bytes as {document.SourceFormat}.";
        if (ReferenceEquals(document, Workspace.Document)) RefreshEditorStatus();
        Raise(nameof(SourcePath), nameof(SourceFormat), nameof(SourceFormatMetadata), nameof(IsModified),
            nameof(ActiveCanSave), nameof(ActiveHasImage), nameof(ActiveCanCloseImage));
    }

    public bool TryNavigateToAddress(uint address, out int rowIndex) => ImageView.TryNavigateToAddress(address, out rowIndex);
    public void RefreshEditedByteMarkers()
    {
        if (ReferenceEquals(ActiveDocument, Workspace.Document)) RefreshImage(clearSelection: false);
    }
    public bool TryNavigateToSegment(int segmentIndex, out int rowIndex) => ImageView.TryNavigateToSegment(segmentIndex, out rowIndex);

    public void ExportSegments(IEnumerable<int> segmentIndexes, MemoryImageExportFormat format, int maxDataBytesPerRecord, Stream output) =>
        _fileService.ExportSegments(CurrentImage, segmentIndexes, format, maxDataBytesPerRecord, output, SourceFormatMetadata);

    public void ExportImage(MemoryImageExportFormat format, int maxDataBytesPerRecord, Stream output) =>
        _fileService.Export(CurrentImage, format, maxDataBytesPerRecord, output, SourceFormatMetadata);

    public void ExportAddressRange(uint startAddress, uint endAddress, MemoryImageExportFormat format, int maxDataBytesPerRecord, Stream output) =>
        _fileService.ExportAddressRange(CurrentImage, startAddress, endAddress, format, maxDataBytesPerRecord, output, SourceFormatMetadata);

    public void ReportExported(string fileName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fileName);
        StatusMessage = $"Exported selected occupied bytes to {fileName}.";
    }

    public void ReportFileOperationCancelled(string operation) => StatusMessage = $"{operation} operation cancelled.";
    public void ReportCompareInteraction(string message) => StatusMessage = message;

    public void RefreshActiveCommandState() => Raise(nameof(ActiveCanUndo), nameof(ActiveCanRedo), nameof(ActiveHasImage),
        nameof(ActiveCanSave), nameof(ActiveCanSaveAs), nameof(ActiveCanCloseImage), nameof(ActiveCanRelocate),
        nameof(ActiveHasSelection), nameof(ActiveCanPaste));

    private void SetOpenReplacementLoading(bool loading)
    {
        if (_isOpenReplacementLoading == loading) return;
        _isOpenReplacementLoading = loading;
        Raise(nameof(IsOpenReplacementLoading), nameof(CanEdit), nameof(CanStartOpenCompareOrMerge),
            nameof(ActiveCanSave), nameof(ActiveCanSaveAs), nameof(ActiveCanCloseImage), nameof(ActiveCanUndo),
            nameof(ActiveCanRedo), nameof(ActiveCanPaste), nameof(ActiveCanRelocate));
    }

    public MemoryRelocationPreview PrepareRelocation(int segmentIndex, uint destinationBaseAddress) => Editing.PrepareRelocation(segmentIndex, destinationBaseAddress);
    public bool ApplyRelocation(MemoryRelocationPreview preview, RelocationOverlapDecision decision) => Editing.ApplyRelocation(preview, decision);

    private void OnEditingImageChanged(MemoryImage previousImage, MemoryImage updatedImage)
    {
        Checksum.TrackImageChange(previousImage, updatedImage);
        RefreshImage();
        RefreshModifiedTitle();
        StatusMessage = "Image modified. Save to write changes to disk.";
        RefreshEditorStatus();
        Raise(nameof(CurrentImage), nameof(IsModified), nameof(CanUndo), nameof(CanRedo), nameof(ActiveImage),
            nameof(ActiveCanUndo), nameof(ActiveCanRedo), nameof(ActiveHasImage), nameof(ActiveCanSave),
            nameof(ActiveCanSaveAs), nameof(ActiveCanCloseImage), nameof(ActiveCanRelocate), nameof(ActiveCanPaste));
        NotifyImageChanged(previousImage, updatedImage);
    }

    private void NotifyImageChanged(MemoryImage previousImage, MemoryImage updatedImage) => ImageChanged?.Invoke(previousImage, updatedImage);

    private void RefreshImage(bool clearSelection = true)
    {
        if (clearSelection) EditorStatus.ClearSelection();
        ImageView = new HexImageViewModel(CurrentImage, ImageView.BytesPerRow, ImageView.ShowAscii, Workspace.Document);
        Segments.Clear();
        foreach (var (block, index) in CurrentImage.Blocks.Select((block, index) => (block, index)))
            Segments.Add(new MemorySegmentRow(block.StartAddress.ToString("X8"), block.EndAddress.ToString("X8"), checked((long)block.ByteCount), index));
    }

    private void RefreshEditorStatus() => EditorStatus.UpdateImageState(SourceFormat, IsModified);

    private void RefreshTitle()
    {
        if (_activeComparisonWorkspace is { } comparison)
        {
            var dirtyMarker = _activeComparisonDocument?.IsModified == true ? " *" : string.Empty;
            WindowTitle = $"Compare: {comparison.NameA}{dirtyMarker} ↔ {comparison.NameB} - HEX Editor";
            return;
        }

        WindowTitle = $"{(string.IsNullOrWhiteSpace(SourcePath) ? "Untitled" : Path.GetFileName(SourcePath))}{(IsModified ? " *" : "")} - HEX Editor";
    }
    private void RefreshModifiedTitle() => RefreshTitle();

    private string DescribeImage(string? format)
    {
        return $"{CurrentImage.Blocks.Count:N0} Blocks, {CurrentImage.OccupiedByteCount:N0} bytes" + (format is null ? string.Empty : $" | {format}");
    }

    private void Raise(params string[] names)
    {
        foreach (var name in names) PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    private void SetProperty<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return;
        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

}

public sealed record MemorySegmentRow(string StartAddress, string EndAddress, long ByteCount, int Number = 0)
{
    public string ByteCountText => SegmentByteCountFormatter.Format(ByteCount);
    public string Description => SegmentBlockFormatter.Format(Number, StartAddress, EndAddress, ByteCount);
}
