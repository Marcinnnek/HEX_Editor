using System.Collections;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text;
using HexEditor.Core;

namespace HexEditor.UI;

public sealed class HexImageViewModel : INotifyPropertyChanged
{
    private int _bytesPerRow;
    private bool _showAscii;
    private HexImageRows _rows;

    public HexImageViewModel(MemoryImage image, int bytesPerRow = 16, bool showAscii = true)
    {
        Image = image ?? throw new ArgumentNullException(nameof(image));
        ValidateRowWidth(bytesPerRow);
        _bytesPerRow = bytesPerRow;
        _showAscii = showAscii;
        _rows = new HexImageRows(image, bytesPerRow, showAscii);
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
            if (_bytesPerRow == value)
            {
                return;
            }

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
            if (_showAscii == value)
            {
                return;
            }

            _showAscii = value;
            RebuildRows();
            OnPropertyChanged();
        }
    }

    public bool TryNavigateToAddress(uint address, out int rowIndex) => _rows.TryGetRowIndex(address, out rowIndex);

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
        _rows = new HexImageRows(Image, _bytesPerRow, _showAscii);
        OnPropertyChanged(nameof(Rows));
    }

    private static void ValidateRowWidth(int bytesPerRow)
    {
        if (bytesPerRow is not (8 or 16 or 32))
        {
            throw new ArgumentOutOfRangeException(nameof(bytesPerRow), "Row width must be 8, 16, or 32 bytes.");
        }
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}

public sealed record HexViewRow(string Address, string HexData, string? Ascii, bool IsGapMarker, string? GapDescription)
{
    internal static HexViewRow Gap(uint startAddress, uint endAddress) => new(
        string.Empty,
        string.Empty,
        null,
        true,
        $"Skipped 0x{startAddress:X8}–0x{endAddress:X8}");
}

internal sealed class HexImageRows : IList<HexViewRow>
{
    private readonly MemoryImage _image;
    private readonly int _bytesPerRow;
    private readonly bool _showAscii;
    private readonly IReadOnlyList<RowRange> _ranges;
    private readonly int _count;

    public HexImageRows(MemoryImage image, int bytesPerRow, bool showAscii)
    {
        _image = image;
        _bytesPerRow = bytesPerRow;
        _showAscii = showAscii;
        _ranges = BuildRanges(image, bytesPerRow);
        _count = _ranges.Count == 0 ? 0 : checked(_ranges[^1].StartIndex + _ranges[^1].Count);
    }

    public int Count => _count;

    public bool IsReadOnly => true;

    public HexViewRow this[int index]
    {
        get
        {
            if ((uint)index >= (uint)_count)
            {
                throw new ArgumentOutOfRangeException(nameof(index));
            }

            var range = FindRange(index);
            if (range.IsGap)
            {
                return HexViewRow.Gap(range.AddressStart, range.AddressEnd);
            }

            var rowAddress = (uint)(range.FirstRowAddress + ((ulong)(index - range.StartIndex) * (uint)_bytesPerRow));
            return CreateRow(rowAddress);
        }
        set => throw new NotSupportedException();
    }

    public bool TryGetRowIndex(uint address, out int rowIndex)
    {
        var rowAddress = address & ~((uint)_bytesPerRow - 1);
        var low = 0;
        var high = _ranges.Count - 1;
        while (low <= high)
        {
            var middle = low + ((high - low) / 2);
            var range = _ranges[middle];
            if (range.IsGap)
            {
                if (rowAddress < range.AddressStart)
                {
                    high = middle - 1;
                }
                else if (rowAddress > range.AddressEnd)
                {
                    low = middle + 1;
                }
                else
                {
                    rowIndex = range.StartIndex;
                    return true;
                }
            }
            else
            {
                var lastRow = range.FirstRowAddress + ((ulong)(range.Count - 1) * (uint)_bytesPerRow);
                if (rowAddress < range.FirstRowAddress)
                {
                    high = middle - 1;
                }
                else if (rowAddress > lastRow)
                {
                    low = middle + 1;
                }
                else
                {
                    rowIndex = checked(range.StartIndex + (int)((rowAddress - range.FirstRowAddress) / (uint)_bytesPerRow));
                    return true;
                }
            }
        }

        rowIndex = -1;
        return false;
    }

    public int IndexOf(HexViewRow item)
    {
        for (var index = 0; index < Count; index++)
        {
            if (Equals(this[index], item))
            {
                return index;
            }
        }

        return -1;
    }

    public bool Contains(HexViewRow item) => IndexOf(item) >= 0;

    public void CopyTo(HexViewRow[] array, int arrayIndex)
    {
        ArgumentNullException.ThrowIfNull(array);
        for (var index = 0; index < Count; index++)
        {
            array[arrayIndex + index] = this[index];
        }
    }

    public IEnumerator<HexViewRow> GetEnumerator()
    {
        for (var index = 0; index < Count; index++)
        {
            yield return this[index];
        }
    }

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    public void Add(HexViewRow item) => throw new NotSupportedException();
    public void Clear() => throw new NotSupportedException();
    public void Insert(int index, HexViewRow item) => throw new NotSupportedException();
    public bool Remove(HexViewRow item) => throw new NotSupportedException();
    public void RemoveAt(int index) => throw new NotSupportedException();

    private HexViewRow CreateRow(uint rowAddress)
    {
        var hex = new StringBuilder((_bytesPerRow * 3) - 1);
        var ascii = _showAscii ? new StringBuilder(_bytesPerRow) : null;
        for (var offset = 0; offset < _bytesPerRow; offset++)
        {
            if (offset > 0)
            {
                hex.Append(' ');
            }

            var address = (ulong)rowAddress + (uint)offset;
            if (address <= uint.MaxValue && _image.TryGetByte((uint)address, out var value))
            {
                hex.Append(value.ToString("X2"));
                if (ascii is not null)
                {
                    ascii.Append(value is >= 0x20 and <= 0x7E ? (char)value : '.');
                }
            }
            else
            {
                hex.Append("  ");
                ascii?.Append(' ');
            }
        }

        return new HexViewRow(rowAddress.ToString("X8"), hex.ToString(), ascii?.ToString(), false, null);
    }

    private RowRange FindRange(int index)
    {
        var low = 0;
        var high = _ranges.Count - 1;
        while (low <= high)
        {
            var middle = low + ((high - low) / 2);
            var range = _ranges[middle];
            if (index < range.StartIndex)
            {
                high = middle - 1;
            }
            else if (index >= range.StartIndex + range.Count)
            {
                low = middle + 1;
            }
            else
            {
                return range;
            }
        }

        throw new InvalidOperationException("The row index is not represented by the sparse row map.");
    }

    private static IReadOnlyList<RowRange> BuildRanges(MemoryImage image, int bytesPerRow)
    {
        if (image.Segments.Count == 0)
        {
            return Array.Empty<RowRange>();
        }

        var ranges = new List<RowRange>();
        var width = (uint)bytesPerRow;
        var rowRanges = image.Segments.Select(segment =>
        {
            var first = (ulong)(segment.StartAddress / width) * width;
            var last = (ulong)(segment.EndAddress / width) * width;
            return (First: first, Last: last);
        }).ToArray();

        var logicalIndex = 0L;
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

            var runCount = ((runEnd - runStart) / width) + 1;
            ranges.Add(RowRange.Rows(checked((int)logicalIndex), runStart, checked((int)runCount)));
            logicalIndex += checked((long)runCount);

            var gapStart = runEnd + width;
            var gapEnd = next.First - width;
            ranges.Add(RowRange.Gap(checked((int)logicalIndex), gapStart, gapEnd));
            logicalIndex++;
            runStart = next.First;
            runEnd = next.Last;
        }

        var finalCount = ((runEnd - runStart) / width) + 1;
        ranges.Add(RowRange.Rows(checked((int)logicalIndex), runStart, checked((int)finalCount)));
        return ranges;
    }

    private sealed record RowRange(int StartIndex, int Count, ulong FirstRowAddress, bool IsGap, uint AddressStart, uint AddressEnd)
    {
        public long EndIndex => (long)StartIndex + Count - 1;

        public static RowRange Rows(int startIndex, ulong address, int count) => new(startIndex, count, address, false, 0, 0);

        public static RowRange Gap(int startIndex, ulong addressStart, ulong addressEnd) => new(
            startIndex,
            1,
            0,
            true,
            (uint)addressStart,
            (uint)addressEnd);
    }
}
