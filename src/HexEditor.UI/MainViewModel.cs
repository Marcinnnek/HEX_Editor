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
    private MemoryImage _savedImage = MemoryImage.Empty;
    private HexImageViewModel _imageView = new(MemoryImage.Empty);
    private string _statusMessage = "Open an Intel HEX or Motorola S-Record file to inspect its occupied memory segments.";
    private string _windowTitle = "HEX Editor";
    private string? _sourcePath;
    private MemoryImageExportFormat? _sourceFormat;
    private object? _sourceFormatMetadata;

    public MainViewModel(MemoryImage? image = null)
    {
        Editing.ImageChanged += OnEditingImageChanged;
        if (image is not null)
        {
            Editing.Reset(image);
            _savedImage = image;
            RefreshImage();
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    public event Action<MemoryImage, MemoryImage>? ImageChanged;

    public EditorStatusViewModel EditorStatus { get; } = new();
    public ChecksumViewModel Checksum { get; } = new();
    public ImageEditingSession Editing { get; } = new();

    public ObservableCollection<MemorySegmentRow> Segments { get; } = new();
    public int[] RowWidths { get; } = [8, 16, 32];
    public int[] ExportPayloadSizes { get; } = [8, 16, 32];
    public MemoryImageExportFormat[] ExportFormats { get; } = Enum.GetValues<MemoryImageExportFormat>();

    public MemoryImage CurrentImage => Editing.CurrentImage;
    public bool IsModified => !ReferenceEquals(CurrentImage, _savedImage);
    public bool CanUndo => Editing.CanUndo;
    public bool CanRedo => Editing.CanRedo;
    public string? SourcePath => _sourcePath;
    public MemoryImageExportFormat? SourceFormat => _sourceFormat;
    public object? SourceFormatMetadata => _sourceFormatMetadata;

    public HexImageViewModel ImageView
    {
        get => _imageView;
        private set => SetProperty(ref _imageView, value);
    }

    public string StatusMessage { get => _statusMessage; private set => SetProperty(ref _statusMessage, value); }
    public string WindowTitle { get => _windowTitle; private set => SetProperty(ref _windowTitle, value); }
    public MemoryImageImportPreview PrepareOpen(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        using var stream = File.OpenRead(path);
        return _fileService.PrepareImport(stream, CurrentImage, path);
    }

    public void ApplyImport(MemoryImageImportPreview preview, OverlapDecision decision)
    {
        ArgumentNullException.ThrowIfNull(preview);
        var hadExistingImage = CurrentImage.Segments.Count > 0;
        var previousImage = CurrentImage;
        var imported = preview.Apply(decision);
        if (imported is null) return;

        Editing.Reset(imported.Image);
        Checksum.Clear();
        // An import applied over an already-open image creates merged contents that are not represented
        // by the newly associated source file, so keep them marked unsaved until the user saves explicitly.
        _savedImage = hadExistingImage ? MemoryImage.Empty : imported.Image;
        _sourcePath = imported.SourcePath;
        _sourceFormat = imported.InputFormat == "Intel HEX" ? MemoryImageExportFormat.IntelHex : MemoryImageExportFormat.SRecord;
        _sourceFormatMetadata = imported.FormatMetadata;
        RefreshImage();
        RefreshEditorStatus();
        RefreshTitle();
        StatusMessage = DescribeImage(imported.InputFormat) + (IsModified ? " | merged changes not saved" : string.Empty);
        Raise(nameof(CurrentImage), nameof(IsModified), nameof(CanUndo), nameof(CanRedo), nameof(SourcePath), nameof(SourceFormat), nameof(SourceFormatMetadata));
        if (!ReferenceEquals(previousImage, CurrentImage)) NotifyImageChanged(previousImage, CurrentImage);
    }

    public void LoadImage(MemoryImage image, string? sourcePath, MemoryImageExportFormat? format)
    {
        ArgumentNullException.ThrowIfNull(image);
        var previousImage = CurrentImage;
        Editing.Reset(image);
        _savedImage = image;
        Checksum.Clear();
        _sourcePath = sourcePath;
        _sourceFormat = format;
        _sourceFormatMetadata = null;
        RefreshImage();
        RefreshEditorStatus();
        RefreshTitle();
        StatusMessage = DescribeImage(format?.ToString());
        Raise(nameof(CurrentImage), nameof(IsModified), nameof(CanUndo), nameof(CanRedo), nameof(SourcePath), nameof(SourceFormat), nameof(SourceFormatMetadata));
        if (!ReferenceEquals(previousImage, image)) NotifyImageChanged(previousImage, image);
    }

    public void CloseImage() => LoadImage(MemoryImage.Empty, null, null);

    public void EditByte(uint address, byte value, OverlapDecision? overlapDecision = null) => Editing.EditByte(address, value, overlapDecision);
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
        return Editing.Undo();
    }

    public bool Redo()
    {
        return Editing.Redo();
    }

    public void Save()
    {
        if (string.IsNullOrWhiteSpace(_sourcePath) || _sourceFormat is null)
            throw new InvalidOperationException("Choose Save As and select a file format first.");
        SaveAs(_sourcePath, _sourceFormat.Value, overwriteExisting: true);
    }

    public void SaveAs(string path, MemoryImageExportFormat format, bool overwriteExisting)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        _fileService.SaveAtomically(CurrentImage, path, format, 16, overwriteExisting, _sourceFormatMetadata);
        _sourcePath = Path.GetFullPath(path);
        _sourceFormat = format;
        _sourceFormatMetadata = MemoryImageFileService.ConvertMetadata(_sourceFormatMetadata, format);
        _savedImage = CurrentImage;
        RefreshTitle();
        StatusMessage = $"Saved {CurrentImage.Segments.Sum(segment => (long)segment.ByteCount):N0} bytes as {format}.";
        RefreshEditorStatus();
        Raise(nameof(SourcePath), nameof(SourceFormat), nameof(SourceFormatMetadata), nameof(IsModified));
    }

    public bool TryNavigateToAddress(uint address, out int rowIndex) => ImageView.TryNavigateToAddress(address, out rowIndex);
    public bool TryNavigateToSegment(int segmentIndex, out int rowIndex) => ImageView.TryNavigateToSegment(segmentIndex, out rowIndex);

    public void ExportSegments(IEnumerable<int> segmentIndexes, MemoryImageExportFormat format, int maxDataBytesPerRecord, Stream output) =>
        _fileService.ExportSegments(CurrentImage, segmentIndexes, format, maxDataBytesPerRecord, output, _sourceFormatMetadata);

    public void ExportAddressRange(uint startAddress, uint endAddress, MemoryImageExportFormat format, int maxDataBytesPerRecord, Stream output) =>
        _fileService.ExportAddressRange(CurrentImage, startAddress, endAddress, format, maxDataBytesPerRecord, output, _sourceFormatMetadata);

    public void ReportExported(string fileName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fileName);
        StatusMessage = $"Exported selected occupied bytes to {fileName}.";
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

    private void RefreshEditorStatus() => EditorStatus.UpdateImageState(_sourceFormat, IsModified);

    private void RefreshTitle() => WindowTitle = $"{(string.IsNullOrWhiteSpace(_sourcePath) ? "Untitled" : Path.GetFileName(_sourcePath))}{(IsModified ? " *" : "")} - HEX Editor";
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
