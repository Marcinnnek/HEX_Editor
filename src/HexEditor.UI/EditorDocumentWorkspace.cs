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

    public EditorDocumentWorkspace()
    {
        Document = new EditorDocument(MemoryImage.Empty, null, null);
        Document.ImageChanged += OnImageChanged;
    }

    public EditorDocument Document { get; private set; }
    public event Action<MemoryImage, MemoryImage>? ImageChanged;

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
    public void EditByte(uint address, byte value, OverlapDecision? decision = null) => Document.Editing.EditByte(address, value, decision);
    public bool Undo() => Document.Editing.Undo();
    public bool Redo() => Document.Editing.Redo();
    public EditRowSession BeginEditRow(uint rowStartAddress, uint selectedAddress, int bytesPerRow) =>
        new(Document, rowStartAddress, selectedAddress, bytesPerRow);
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
