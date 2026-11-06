using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text;
using HexEditor.Core;
using HexEditor.Compare;

namespace HexEditor.UI;

public sealed class HexImageViewModel : INotifyPropertyChanged
{
    private int _bytesPerRow;
    private bool _showAscii;
    private uint? _findMatchStart;
    private uint? _findMatchEnd;
    private SparseRowList<HexViewRow> _rows;
    private readonly EditorDocument? _document;

    public HexImageViewModel(MemoryImage image, int bytesPerRow = 16, bool showAscii = true, EditorDocument? document = null)
    {
        Image = image ?? throw new ArgumentNullException(nameof(image));
        _document = document;
        ValidateRowWidth(bytesPerRow);
        _bytesPerRow = bytesPerRow;
        _showAscii = showAscii;
        _rows = CreateRows(image, bytesPerRow, showAscii);
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    public MemoryImage Image { get; }
    public IList<HexViewRow> Rows => _rows;

    public int BytesPerRow
    {
        get => _bytesPerRow;
        set
        {
            ValidateRowWidth(value);
            if (_bytesPerRow == value) return;
            _bytesPerRow = value;
            RebuildRows();
            OnPropertyChanged();
        }
    }

    public bool ShowAscii
    {
        get => _showAscii;
        set
        {
            if (_showAscii == value) return;
            _showAscii = value;
            RebuildRows();
            OnPropertyChanged();
        }
    }

    public void SetFindMatch(uint startAddress, uint endAddress)
    {
        if (startAddress > endAddress) throw new ArgumentOutOfRangeException(nameof(startAddress));
        _findMatchStart = startAddress;
        _findMatchEnd = endAddress;
        RebuildRows();
    }

    public void ClearFindMatch()
    {
        if (_findMatchStart is null) return;
        _findMatchStart = _findMatchEnd = null;
        RebuildRows();
    }

    public bool TryNavigateToAddress(uint address, out int rowIndex)
    {
        var originalRowCount = _rows.Count;
        var found = _rows.TryGetRowIndex(address, out rowIndex);
        if (_rows.Count != originalRowCount) OnPropertyChanged(nameof(Rows));
        return found;
    }

    public bool TryNavigateToSegment(int segmentIndex, out int rowIndex)
    {
        if ((uint)segmentIndex >= (uint)Image.Blocks.Count)
        {
            rowIndex = -1;
            return false;
        }
        return TryNavigateToAddress(Image.Blocks[segmentIndex].StartAddress, out rowIndex);
    }

    private void RebuildRows()
    {
        _rows = CreateRows(Image, _bytesPerRow, _showAscii);
        OnPropertyChanged(nameof(Rows));
    }

    private SparseRowList<HexViewRow> CreateRows(MemoryImage image, int bytesPerRow, bool showAscii) =>
        new(image.Ranges.Select(range => ((ulong)range.StartAddress, (ulong)range.EndAddress)),
            bytesPerRow, position => position.IsGap
            ? HexViewRow.Gap((uint)position.GapStartAddress, (uint)position.GapEndAddress)
            : CreateRow(image, bytesPerRow, showAscii, (uint)position.RowAddress), sourceIdentity: image);

    private HexViewRow CreateRow(MemoryImage image, int bytesPerRow, bool showAscii, uint rowAddress)
    {
        var regionEnd = AddressRowGrid.RegionEnd(rowAddress);
        var hex = new StringBuilder((bytesPerRow * 3) - 1);
        var ascii = showAscii ? new StringBuilder(bytesPerRow) : null;
        var cells = new HexByteCell[bytesPerRow];
        for (var offset = 0; offset < bytesPerRow; offset++)
        {
            if (offset > 0) hex.Append(' ');
            var address = (ulong)rowAddress + (uint)offset;
            byte? value = address <= regionEnd && image.TryGetByte((uint)address, out var present) ? present : null;
            var text = ByteText.FromByte(value);
            hex.Append(text.HexColumn);
            ascii?.Append(text.Ascii);
            var byteAddress = address <= regionEnd ? (uint?)address : null;
            var isFindMatch = byteAddress is uint current && _findMatchStart is uint matchStart &&
                current >= matchStart && current <= _findMatchEnd!.Value;
            var isEdited = byteAddress is uint editedAddress && value is not null && _document?.IsEditedByte(editedAddress) == true;
            cells[offset] = new HexByteCell(text.Hex, text.Ascii.ToString(), byteAddress, ByteHighlightState.None, isFindMatch, isEdited);
        }
        return new HexViewRow(rowAddress.ToString("X8"), hex.ToString(), ascii?.ToString(), false, null) { Cells = cells };
    }

    private static void ValidateRowWidth(int bytesPerRow)
    {
        if (bytesPerRow is < 1 or > 128)
            throw new ArgumentOutOfRangeException(nameof(bytesPerRow), "Row width must be between 1 and 128 bytes.");
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
public sealed record HexViewRow(string Address, string HexData, string? Ascii, bool IsGapMarker, string? GapDescription)
{
    public IReadOnlyList<HexByteCell> Cells { get; init; } = Array.Empty<HexByteCell>();

    internal static HexViewRow Gap(uint startAddress, uint endAddress) => new(
        string.Empty,
        string.Empty,
        null,
        true,
        $"Skipped 0x{startAddress:X8}...0x{endAddress:X8}");
}

public enum ByteHighlightState { None, Different, OnlyInPanel, OnlyOther, OnlyInA, OnlyInB }

public sealed record HexByteCell(string Hex, string Ascii, uint? Address, ByteHighlightState Highlight, bool IsFindMatch = false, bool IsEdited = false);
