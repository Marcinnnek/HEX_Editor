using System.ComponentModel;
using System.IO;
using HexEditor.Core;
using HexEditor.Formats;

namespace HexEditor.UI;

/// <summary>An editable image and its file association, saved revision, and history.</summary>
public sealed class EditorDocument : INotifyPropertyChanged
{
    private readonly MemoryImageFileService _files = new();
    private MemoryImage? _savedImage;

    internal EditorDocument(MemoryImage image, string? sourcePath, MemoryImageExportFormat? sourceFormat,
        MemoryImageFormatMetadata? metadata = null, bool isModified = false)
    {
        Editing.Reset(image);
        _savedImage = isModified ? null : image;
        SourcePath = sourcePath;
        SourceFormat = sourceFormat;
        SourceFormatMetadata = metadata;
        Editing.ImageChanged += (previous, updated) =>
        {
            Raise(nameof(CurrentImage), nameof(IsModified), nameof(CanUndo), nameof(CanRedo));
            ImageChanged?.Invoke(previous, updated);
        };
    }

    public Guid Id { get; } = Guid.NewGuid();
    public ImageEditingSession Editing { get; } = new();
    public MemoryImage CurrentImage => Editing.CurrentImage;
    public bool IsModified => !ReferenceEquals(CurrentImage, _savedImage);
    public bool CanUndo => Editing.CanUndo;
    public bool CanRedo => Editing.CanRedo;
    public string? SourcePath { get; private set; }
    public MemoryImageExportFormat? SourceFormat { get; private set; }
    public MemoryImageFormatMetadata? SourceFormatMetadata { get; private set; }
    public event Action<MemoryImage, MemoryImage>? ImageChanged;
    public event PropertyChangedEventHandler? PropertyChanged;

    public void Save()
    {
        if (string.IsNullOrWhiteSpace(SourcePath) || SourceFormat is null)
            throw new InvalidOperationException("Choose Save As and select a file format first.");
        SaveAs(SourcePath, SourceFormat.Value, overwriteExisting: true);
    }

    public Task SaveAsync(CancellationToken cancellationToken, IProgress<MemoryImageFileProgress>? progress)
    {
        if (string.IsNullOrWhiteSpace(SourcePath) || SourceFormat is null)
            throw new InvalidOperationException("Choose Save As and select a file format first.");
        return SaveAsAsync(SourcePath, SourceFormat.Value, overwriteExisting: true, cancellationToken, progress);
    }

    public void SaveAs(string path, MemoryImageExportFormat format, bool overwriteExisting)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        var image = CurrentImage;
        var metadata = SourceFormatMetadata;
        _files.SaveAtomically(image, path, format, 16, overwriteExisting, metadata);
        CompleteSave(path, format, image, metadata);
    }

    public async Task SaveAsAsync(string path, MemoryImageExportFormat format, bool overwriteExisting,
        CancellationToken cancellationToken, IProgress<MemoryImageFileProgress>? progress)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        var image = CurrentImage;
        var metadata = SourceFormatMetadata;
        await Task.Run(() => _files.SaveAtomically(image, path, format, 16, overwriteExisting,
            metadata, cancellationToken, progress), cancellationToken);
        CompleteSave(path, format, image, metadata);
    }

    private void CompleteSave(string path, MemoryImageExportFormat format, MemoryImage image,
        MemoryImageFormatMetadata? metadata)
    {
        SourcePath = Path.GetFullPath(path);
        SourceFormat = format;
        SourceFormatMetadata = MemoryImageFileService.ConvertMetadata(metadata, format);
        _savedImage = image;
        Raise(nameof(SourcePath), nameof(SourceFormat), nameof(SourceFormatMetadata), nameof(IsModified));
    }

    private void Raise(params string[] propertyNames)
    {
        foreach (var name in propertyNames) PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}

/// <summary>Routes Editor operations to the current document without using image or path equality as identity.</summary>
public sealed class EditorDocumentWorkspace
{
    private readonly MemoryImageFileService _files = new();
    private readonly List<CompareWorkspaceTabViewModel> _comparisonViews = [];

    public EditorDocumentWorkspace()
    {
        Document = new EditorDocument(MemoryImage.Empty, null, null);
        Document.ImageChanged += OnImageChanged;
    }

    public EditorDocument Document { get; private set; }
    public IReadOnlyList<CompareWorkspaceTabViewModel> ComparisonViews => _comparisonViews;
    public event Action<MemoryImage, MemoryImage>? ImageChanged;

    public EditorDocument GetActiveDocument(CompareWorkspaceTabViewModel? activeComparison) =>
        activeComparison?.BaseDocument ?? Document;

    public MemoryImageImportPreview PrepareOpen(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        using var stream = File.OpenRead(path);
        return _files.PrepareImport(stream, Document.CurrentImage, path);
    }

    public Task<MemoryImageImportPreview> PrepareOpenAsync(string path, CancellationToken cancellationToken,
        IProgress<MemoryImageFileProgress>? progress)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        var currentImage = Document.CurrentImage;
        return Task.Run(() =>
        {
            using var stream = File.OpenRead(path);
            return _files.PrepareImport(stream, currentImage, path, cancellationToken, progress);
        }, cancellationToken);
    }

    public bool Open(string path, OverlapDecision decision) => ApplyImport(PrepareOpen(path), decision);

    public void OpenImage(MemoryImage image, string? sourcePath, MemoryImageExportFormat? format,
        MemoryImageFormatMetadata? metadata = null, bool isModified = false)
    {
        ArgumentNullException.ThrowIfNull(image);
        Document.ImageChanged -= OnImageChanged;
        Document = new EditorDocument(image, sourcePath, format, metadata, isModified);
        Document.ImageChanged += OnImageChanged;
    }

    public bool ApplyImport(MemoryImageImportPreview preview, OverlapDecision decision)
    {
        ArgumentNullException.ThrowIfNull(preview);
        var imported = preview.Apply(decision);
        if (imported is null) return false;
        OpenImage(imported.Image, imported.SourcePath, imported.DetectedFormat, imported.FormatMetadata,
            isModified: Document.CurrentImage.Segments.Count > 0);
        return true;
    }

    public void CloseImage() => OpenImage(MemoryImage.Empty, null, null);

    public void RegisterComparisonView(CompareWorkspaceTabViewModel view)
    {
        ArgumentNullException.ThrowIfNull(view);
        if (view.BaseDocument is null) throw new ArgumentException("A comparison view must reference its editable base document.", nameof(view));
        if (_comparisonViews.Contains(view)) throw new InvalidOperationException("The comparison view is already open.");
        _comparisonViews.Add(view);
    }

    public bool IsLastView(EditorDocument document, CompareWorkspaceTabViewModel closingView)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(closingView);
        var otherViews = _comparisonViews.Count(view => !ReferenceEquals(view, closingView) && ReferenceEquals(view.BaseDocument, document));
        if (ReferenceEquals(Document, document)) otherViews++;
        return otherViews == 0;
    }

    public bool CloseView(CompareWorkspaceTabViewModel view) => _comparisonViews.Remove(view);

    public IReadOnlyList<CompareWorkspaceTabViewModel> CloseImage(EditorDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        var views = _comparisonViews.Where(view => ReferenceEquals(view.BaseDocument, document)).ToArray();
        foreach (var view in views) _comparisonViews.Remove(view);
        return views;
    }
    public void EditByte(uint address, byte value, OverlapDecision? decision = null) => Document.Editing.EditByte(address, value, decision);
    public bool Undo() => Document.Editing.Undo();
    public bool Redo() => Document.Editing.Redo();
    public EditRowSession BeginEditRow(uint rowStartAddress, uint selectedAddress, int bytesPerRow) =>
        BeginEditRow(Document, rowStartAddress, selectedAddress, bytesPerRow);
    public EditRowSession BeginEditRow(EditorDocument document, uint rowStartAddress, uint selectedAddress, int bytesPerRow)
    {
        ArgumentNullException.ThrowIfNull(document);
        return new EditRowSession(document, rowStartAddress, selectedAddress, bytesPerRow);
    }
    public MemoryRelocationPreview PrepareRelocation(int segmentIndex, uint destinationBaseAddress) =>
        Document.Editing.PrepareRelocation(segmentIndex, destinationBaseAddress);
    public bool ApplyRelocation(MemoryRelocationPreview preview, RelocationOverlapDecision decision) =>
        Document.Editing.ApplyRelocation(preview, decision);
    public void Save() => Document.Save();
    public Task SaveAsync(CancellationToken cancellationToken, IProgress<MemoryImageFileProgress>? progress) =>
        Document.SaveAsync(cancellationToken, progress);
    public void SaveAs(string path, MemoryImageExportFormat format, bool overwriteExisting) =>
        Document.SaveAs(path, format, overwriteExisting);
    public Task SaveAsAsync(string path, MemoryImageExportFormat format, bool overwriteExisting,
        CancellationToken cancellationToken, IProgress<MemoryImageFileProgress>? progress) =>
        Document.SaveAsAsync(path, format, overwriteExisting, cancellationToken, progress);

    private void OnImageChanged(MemoryImage previous, MemoryImage updated) => ImageChanged?.Invoke(previous, updated);
}
