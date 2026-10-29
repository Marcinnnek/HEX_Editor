using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using HexEditor.Core;
using HexEditor.Formats;

namespace HexEditor.UI;

public sealed class MainViewModel : INotifyPropertyChanged
{
    private readonly MemoryImageFileService _fileService = new();
    private string _statusMessage = "Open an Intel HEX or Motorola S-Record file to inspect its occupied memory segments.";
    private string _windowTitle = "HEX Editor";
    private MemoryImage _currentImage = MemoryImage.Empty;
    private HexImageViewModel _imageView = new(MemoryImage.Empty);

    public event PropertyChangedEventHandler? PropertyChanged;

    public ObservableCollection<MemorySegmentRow> Segments { get; } = new();

    public int[] RowWidths { get; } = [8, 16, 32];

    public HexImageViewModel ImageView
    {
        get => _imageView;
        private set => SetProperty(ref _imageView, value);
    }

    public string StatusMessage
    {
        get => _statusMessage;
        private set => SetProperty(ref _statusMessage, value);
    }

    public string WindowTitle
    {
        get => _windowTitle;
        private set => SetProperty(ref _windowTitle, value);
    }

    public MemoryImageImportPreview PrepareOpen(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        using var stream = File.OpenRead(path);
        return _fileService.PrepareImport(stream, _currentImage, path);
    }

    public void ApplyImport(MemoryImageImportPreview preview, OverlapDecision decision)
    {
        ArgumentNullException.ThrowIfNull(preview);
        var imported = preview.Apply(decision);
        if (imported is null)
        {
            return;
        }

        _currentImage = imported.Image;
        ImageView = new HexImageViewModel(imported.Image);

        Segments.Clear();
        foreach (var segment in imported.Image.Segments)
        {
            Segments.Add(new MemorySegmentRow(
                segment.StartAddress.ToString("X8"),
                segment.EndAddress.ToString("X8"),
                segment.ByteCount));
        }

        var totalBytes = imported.Image.Segments.Sum(segment => (long)segment.ByteCount);
        var fileName = Path.GetFileName(imported.SourcePath ?? "memory image");
        WindowTitle = $"{fileName} - HEX Editor";
        StatusMessage = $"{imported.Image.Segments.Count:N0} segments, {totalBytes:N0} bytes | {imported.InputFormat}";
    }

    public bool TryNavigateToAddress(uint address, out int rowIndex) => ImageView.TryNavigateToAddress(address, out rowIndex);

    public bool TryNavigateToSegment(int segmentIndex, out int rowIndex) => ImageView.TryNavigateToSegment(segmentIndex, out rowIndex);

    private void SetProperty<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return;
        }

        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}

public sealed record MemorySegmentRow(string StartAddress, string EndAddress, int ByteCount);
