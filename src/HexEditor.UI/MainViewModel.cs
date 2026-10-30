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

    public MainViewModel(MemoryImage? image = null)
    {
        Workspace.ImageChanged += OnEditingImageChanged;
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
    public void ActivateEditor() { _activeComparison = null; Raise(nameof(ActiveBytesPerRow), nameof(ActiveShowAscii)); }
    public void ActivateComparison(CompareViewModel? comparison)
    {
        if (_activeComparison is not null) _activeComparison.PropertyChanged -= OnActiveComparisonPropertyChanged;
        _activeComparison = comparison;
        if (_activeComparison is not null) _activeComparison.PropertyChanged += OnActiveComparisonPropertyChanged;
        Raise(nameof(ActiveBytesPerRow), nameof(ActiveShowAscii));
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

    public void ApplyImport(MemoryImageImportPreview preview, OverlapDecision decision)
    {
        ArgumentNullException.ThrowIfNull(preview);
        var previousImage = CurrentImage;
        if (!Workspace.ApplyImport(preview, decision)) return;

        Checksum.Clear();
        RefreshImage();
        RefreshEditorStatus();
        RefreshTitle();
        StatusMessage = DescribeImage(preview.InputFormat) + (IsModified ? " | merged changes not saved" : string.Empty);
        Raise(nameof(CurrentImage), nameof(IsModified), nameof(CanUndo), nameof(CanRedo), nameof(SourcePath), nameof(SourceFormat), nameof(SourceFormatMetadata));
        if (!ReferenceEquals(previousImage, CurrentImage)) NotifyImageChanged(previousImage, CurrentImage);
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
        Raise(nameof(CurrentImage), nameof(IsModified), nameof(CanUndo), nameof(CanRedo), nameof(SourcePath), nameof(SourceFormat), nameof(SourceFormatMetadata));
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
        StatusMessage = $"Copied {copiedBytes:N0} occupied byte(s), preserving address gaps.";
    }
    public void CutRange(uint startAddress, uint endAddress) => Editing.CutRange(startAddress, endAddress);
    public void PasteAt(uint address, OverlapDecision? overlapDecision = null) => Editing.PasteAt(address, overlapDecision);
    public int GetPasteOverlapCount(uint address) => Editing.GetPasteOverlapCount(address);
    public void DeleteRange(uint startAddress, uint endAddress) => Editing.DeleteRange(startAddress, endAddress);
    public void FillRange(uint startAddress, uint endAddress, IReadOnlyList<byte> pattern) => Editing.FillRange(startAddress, endAddress, pattern);

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
        return Workspace.Undo();
    }

    public bool Redo()
    {
        return Workspace.Redo();
    }

    public void Save()
    {
        Workspace.Save();
        CompleteSave();
    }

    public async Task SaveAsync(CancellationToken cancellationToken, IProgress<MemoryImageFileProgress>? progress)
    {
        var document = Workspace.Document;
        await Workspace.SaveAsync(cancellationToken, progress);
        if (ReferenceEquals(Workspace.Document, document)) CompleteSave();
    }

    public async Task SaveAsAsync(string path, MemoryImageExportFormat format, bool overwriteExisting,
        CancellationToken cancellationToken, IProgress<MemoryImageFileProgress>? progress)
    {
        var document = Workspace.Document;
        await Workspace.SaveAsAsync(path, format, overwriteExisting, cancellationToken, progress);
        if (ReferenceEquals(Workspace.Document, document)) CompleteSave();
    }

    public void SaveAs(string path, MemoryImageExportFormat format, bool overwriteExisting)
    {
        Workspace.SaveAs(path, format, overwriteExisting);
        CompleteSave();
    }

    private void CompleteSave()
    {
        RefreshTitle();
        StatusMessage = $"Saved {CurrentImage.Segments.Sum(segment => (long)segment.ByteCount):N0} bytes as {SourceFormat}.";
        RefreshEditorStatus();
        Raise(nameof(SourcePath), nameof(SourceFormat), nameof(SourceFormatMetadata), nameof(IsModified));
    }

    public bool TryNavigateToAddress(uint address, out int rowIndex) => ImageView.TryNavigateToAddress(address, out rowIndex);
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

    public MemoryRelocationPreview PrepareRelocation(int segmentIndex, uint destinationBaseAddress) => Editing.PrepareRelocation(segmentIndex, destinationBaseAddress);
    public bool ApplyRelocation(MemoryRelocationPreview preview, RelocationOverlapDecision decision) => Editing.ApplyRelocation(preview, decision);

    private void OnEditingImageChanged(MemoryImage previousImage, MemoryImage updatedImage)
    {
        Checksum.TrackImageChange(previousImage, updatedImage);
        RefreshImage();
        RefreshModifiedTitle();
        StatusMessage = "Image modified. Save to write changes to disk.";
        RefreshEditorStatus();
        Raise(nameof(CurrentImage), nameof(IsModified), nameof(CanUndo), nameof(CanRedo));
        NotifyImageChanged(previousImage, updatedImage);
    }

    private void NotifyImageChanged(MemoryImage previousImage, MemoryImage updatedImage) => ImageChanged?.Invoke(previousImage, updatedImage);

    private void RefreshImage()
    {
        EditorStatus.ClearSelection();
        ImageView = new HexImageViewModel(CurrentImage, ImageView.BytesPerRow, ImageView.ShowAscii);
        Segments.Clear();
        foreach (var segment in CurrentImage.Segments)
            Segments.Add(new MemorySegmentRow(segment.StartAddress.ToString("X8"), segment.EndAddress.ToString("X8"), segment.ByteCount));
    }

    private void RefreshEditorStatus() => EditorStatus.UpdateImageState(SourceFormat, IsModified);

    private void RefreshTitle() => WindowTitle = $"{(string.IsNullOrWhiteSpace(SourcePath) ? "Untitled" : Path.GetFileName(SourcePath))}{(IsModified ? " *" : "")} - HEX Editor";
    private void RefreshModifiedTitle() => RefreshTitle();

    private string DescribeImage(string? format)
    {
        var totalBytes = CurrentImage.Segments.Sum(segment => (long)segment.ByteCount);
        return $"{CurrentImage.Segments.Count:N0} segments, {totalBytes:N0} bytes" + (format is null ? string.Empty : $" | {format}");
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

public sealed record MemorySegmentRow(string StartAddress, string EndAddress, int ByteCount);
