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
    private SparseRowList<HexViewRow> _rows;

    public HexImageViewModel(MemoryImage image, int bytesPerRow = 16, bool showAscii = true)
    {
        Image = image ?? throw new ArgumentNullException(nameof(image));
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

    public bool TryNavigateToAddress(uint address, out int rowIndex)
    {
        var originalRowCount = _rows.Count;
        var found = _rows.TryGetRowIndex(address, out rowIndex);
        if (_rows.Count != originalRowCount) OnPropertyChanged(nameof(Rows));
        return found;
    }

    public bool TryNavigateToSegment(int segmentIndex, out int rowIndex)
    {
        if ((uint)segmentIndex >= (uint)Image.Segments.Count)
        {
            rowIndex = -1;
            return false;
        }
        return TryNavigateToAddress(Image.Segments[segmentIndex].StartAddress, out rowIndex);
    }

    private void RebuildRows()
    {
        _rows = CreateRows(Image, _bytesPerRow, _showAscii);
        OnPropertyChanged(nameof(Rows));
    }

    private static SparseRowList<HexViewRow> CreateRows(MemoryImage image, int bytesPerRow, bool showAscii) =>
        new(BuildRanges(image, bytesPerRow), bytesPerRow, position => position.IsGap
            ? HexViewRow.Gap((uint)position.GapStartAddress, (uint)position.GapEndAddress)
            : CreateRow(image, bytesPerRow, showAscii, (uint)position.RowAddress));

    private static HexViewRow CreateRow(MemoryImage image, int bytesPerRow, bool showAscii, uint rowAddress)
    {
        var hex = new StringBuilder((bytesPerRow * 3) - 1);
        var ascii = showAscii ? new StringBuilder(bytesPerRow) : null;
        var cells = new HexByteCell[bytesPerRow];
        for (var offset = 0; offset < bytesPerRow; offset++)
        {
            if (offset > 0) hex.Append(' ');
            var address = (ulong)rowAddress + (uint)offset;
            if (address <= uint.MaxValue && image.TryGetByte((uint)address, out var value))
            {
                var character = value is >= 0x20 and <= 0x7E ? ((char)value).ToString() : ".";
                hex.Append(value.ToString("X2"));
                ascii?.Append(character);
                cells[offset] = new HexByteCell(value.ToString("X2"), character, (uint)address, string.Empty);
            }
            else
            {
                hex.Append("  ");
                ascii?.Append(' ');
                cells[offset] = new HexByteCell(string.Empty, " ", address <= uint.MaxValue ? (uint)address : null, string.Empty);
            }
        }
        return new HexViewRow(rowAddress.ToString("X8"), hex.ToString(), ascii?.ToString(), false, null) { Cells = cells };
    }

    private static IReadOnlyList<SparseRowSpan> BuildRanges(MemoryImage image, int bytesPerRow)
    {
        if (image.Segments.Count == 0) return Array.Empty<SparseRowSpan>();
        var width = (uint)bytesPerRow;
        var rowRanges = image.Segments.Select(segment =>
        {
            var first = (ulong)(segment.StartAddress / width) * width;
            var last = (ulong)(segment.EndAddress / width) * width;
            return (First: first, Last: last);
        }).ToArray();

        var ranges = new List<SparseRowSpan>();
        var runStart = rowRanges[0].First;
        var runEnd = rowRanges[0].Last;
        for (var index = 1; index < rowRanges.Length; index++)
        {
            var next = rowRanges[index];
            var emptyRows = ((long)next.First - (long)runEnd) / bytesPerRow - 1;
            if (emptyRows <= 1)
            {
                runEnd = next.Last;
                continue;
            }
            ranges.Add(new SparseRowSpan(runStart, runEnd));
            ranges.Add(new SparseRowSpan(runEnd + width, next.First - width, IsGap: true));
            runStart = next.First;
            runEnd = next.Last;
        }
        ranges.Add(new SparseRowSpan(runStart, runEnd));
        return ranges;
    }

    private static void ValidateRowWidth(int bytesPerRow)
    {
        if (bytesPerRow is not (8 or 16 or 32))
            throw new ArgumentOutOfRangeException(nameof(bytesPerRow), "Row width must be 8, 16, or 32 bytes.");
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

public sealed record HexByteCell(string Hex, string Ascii, uint? Address, string Highlight);
