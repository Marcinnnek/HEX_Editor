using System.ComponentModel;
using System.Runtime.CompilerServices;
using HexEditor.Formats;

namespace HexEditor.UI;

/// <summary>Owns the compact editor context shown in the status bar.</summary>
public sealed class EditorStatusViewModel : INotifyPropertyChanged
{
    private uint? _currentAddress;
    private long _selectionLength;
    private MemoryImageExportFormat? _imageFormat;
    private bool _isModified;

    public event PropertyChangedEventHandler? PropertyChanged;

    public uint? CurrentAddress => _currentAddress;
    public string CurrentAddressText => _currentAddress?.ToString("X8") ?? "—";
    public long SelectionLength => _selectionLength;
    public string ImageFormatText => _imageFormat switch
    {
        MemoryImageExportFormat.IntelHex => "Intel HEX",
        MemoryImageExportFormat.SRecord => "Motorola S-Record",
        MemoryImageExportFormat.Vbf => "VBF",
        _ => "Unknown format"
    };
    public string ModifiedStateText => _isModified ? "Modified" : "Saved";

    public void SetSelection(uint startAddress, uint endAddress)
    {
        if (startAddress > endAddress) throw new ArgumentOutOfRangeException(nameof(startAddress), "Selection start must not exceed its end.");
        _currentAddress = startAddress;
        _selectionLength = (long)endAddress - startAddress + 1;
        Raise(nameof(CurrentAddress), nameof(CurrentAddressText), nameof(SelectionLength));
    }

    public void ClearSelection()
    {
        _currentAddress = null;
        _selectionLength = 0;
        Raise(nameof(CurrentAddress), nameof(CurrentAddressText), nameof(SelectionLength));
    }

    public void UpdateImageState(MemoryImageExportFormat? format, bool isModified)
    {
        var formatChanged = _imageFormat != format;
        var modifiedChanged = _isModified != isModified;
        _imageFormat = format;
        _isModified = isModified;
        if (formatChanged) Raise(nameof(ImageFormatText));
        if (modifiedChanged) Raise(nameof(ModifiedStateText));
    }

    private void Raise(params string[] names)
    {
        foreach (var name in names) PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
