using HexEditor.Core;
using System.IO;

namespace HexEditor.UI;

/// <summary>Stages changes to occupied bytes in one aligned Editor row.</summary>
public sealed class EditRowSession
{
    private readonly EditorDocument _document;
    private readonly MemoryImage _originalImage;
    private readonly Dictionary<uint, byte> _proposed = new();
    private readonly HashSet<uint> _invalidAddresses = new();

    internal EditRowSession(EditorDocument document, uint rowStartAddress, uint selectedAddress, int bytesPerRow)
    {
        if (bytesPerRow is < 1 or > 128) throw new ArgumentOutOfRangeException(nameof(bytesPerRow));
        if (selectedAddress < rowStartAddress || (ulong)selectedAddress >= (ulong)rowStartAddress + (uint)bytesPerRow)
            throw new ArgumentOutOfRangeException(nameof(selectedAddress));
        _document = document;
        _originalImage = document.CurrentImage;
        RowStartAddress = rowStartAddress;
        SelectedAddress = selectedAddress;
        var cells = new List<EditRowCell>(bytesPerRow);
        for (var index = 0; index < bytesPerRow; index++)
        {
            var address = (ulong)rowStartAddress + (uint)index;
            byte? value = address <= uint.MaxValue && _originalImage.TryGetByte((uint)address, out var found) ? found : null;
            if (value is byte occupied) _proposed[(uint)address] = occupied;
            cells.Add(new EditRowCell(address <= uint.MaxValue ? (uint)address : null, value, index));
        }
        Cells = cells;
    }

    public uint RowStartAddress { get; }
    public uint SelectedAddress { get; }
    public string ImageName => string.IsNullOrWhiteSpace(_document.SourcePath)
        ? "Untitled image"
        : Path.GetFileName(_document.SourcePath);
    public IReadOnlyList<EditRowCell> Cells { get; }
    public bool IsValid => _invalidAddresses.Count == 0;
    public bool IsCompleted { get; private set; }

    public bool SetHex(uint address, string text)
    {
        if (!CanEdit(address)) return false;
        if (text is null || text.Length is < 1 or > 2 || !byte.TryParse(text, System.Globalization.NumberStyles.AllowHexSpecifier,
                System.Globalization.CultureInfo.InvariantCulture, out var value))
        { _invalidAddresses.Add(address); return false; }
        _proposed[address] = value;
        _invalidAddresses.Remove(address);
        return true;
    }

    public bool SetAscii(uint address, string text)
    {
        if (!CanEdit(address)) return false;
        if (text is null || text.Length != 1 || text[0] < 0x20 || text[0] > 0x7E)
        { _invalidAddresses.Add(address); return false; }
        _proposed[address] = (byte)text[0];
        _invalidAddresses.Remove(address);
        return true;
    }

    public bool Commit()
    {
        if (IsCompleted || !IsValid) return false;
        if (!ReferenceEquals(_document.CurrentImage, _originalImage))
            throw new InvalidOperationException("The image changed while Edit Row was open. Reopen the dialog before applying changes.");
        var changed = _proposed.Where(pair => !_originalImage.TryGetByte(pair.Key, out var original) || original != pair.Value).ToArray();
        IsCompleted = true;
        if (changed.Length == 0) return false;
        _document.Editing.EditOccupiedBytes(changed.ToDictionary(pair => pair.Key, pair => pair.Value));
        return true;
    }

    public void Cancel() => IsCompleted = true;

    private bool CanEdit(uint address) => !IsCompleted && Cells.Any(cell => cell.Address == address && cell.IsOccupied);
}

public sealed record EditRowCell(uint? Address, byte? OriginalValue, int Offset)
{
    public bool IsOccupied => OriginalValue.HasValue;
    public string OffsetText => Offset.ToString("X2");
    public string AddressText => Address?.ToString("X8") ?? "—";
    public string Hex => OriginalValue?.ToString("X2") ?? "  ";
    public string Ascii => OriginalValue is byte value ? value is >= 0x20 and <= 0x7E ? ((char)value).ToString() : "." : "";
}
