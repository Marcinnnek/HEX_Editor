using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using HexEditor.Formats;

namespace HexEditor.UI;

public sealed class MainViewModel : INotifyPropertyChanged
{
    private readonly MemoryImageFileService _fileService = new();
    private string _statusMessage = "Open an Intel HEX file to inspect its occupied memory segments.";
    private string _windowTitle = "HEX Editor";

    public event PropertyChangedEventHandler? PropertyChanged;

    public ObservableCollection<MemorySegmentRow> Segments { get; } = new();

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

    public void OpenIntelHex(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        using var stream = File.OpenRead(path);
        var imported = _fileService.ImportIntelHex(stream, path);

        Segments.Clear();
        foreach (var segment in imported.Image.Segments)
        {
            Segments.Add(new MemorySegmentRow(
                segment.StartAddress.ToString("X8"),
                segment.EndAddress.ToString("X8"),
                segment.ByteCount));
        }

        var totalBytes = imported.Image.Segments.Sum(segment => (long)segment.ByteCount);
        var fileName = Path.GetFileName(path);
        WindowTitle = $"{fileName} - HEX Editor";
        StatusMessage = $"{imported.Image.Segments.Count:N0} segments, {totalBytes:N0} bytes | {imported.InputFormat}";
    }

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
