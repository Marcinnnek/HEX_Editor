using System.Collections;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text;
using HexEditor.Core;

namespace HexEditor.UI;

public enum CompareByteState { Equal, Different, OnlyA, OnlyB }
public enum CompareViewMode { AllRegions, DifferencesOnly, AddressRange }

public sealed class CompareViewModel : INotifyPropertyChanged
{
    private MemoryImage _fileA;
    private MemoryImage _fileB;
    private int _bytesPerRow;
    private bool _showAscii;
    private CompareViewMode _viewMode;
    private uint _rangeStart;
    private uint _rangeEnd = uint.MaxValue;
    private CompareRows _rows;

    public CompareViewModel(MemoryImage fileA, MemoryImage fileB, int bytesPerRow = 16, bool showAscii = true)
    {
        _fileA = fileA ?? throw new ArgumentNullException(nameof(fileA));
        _fileB = fileB ?? throw new ArgumentNullException(nameof(fileB));
        ValidateRowWidth(bytesPerRow);
        _bytesPerRow = bytesPerRow;
        _showAscii = showAscii;
        _rows = new CompareRows(this);
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    public IList<CompareViewRow> Rows => _rows;
    public MemoryImage FileA => _fileA;
    public MemoryImage FileB => _fileB;
    public int BytesPerRow { get => _bytesPerRow; set { ValidateRowWidth(value); if (SetField(ref _bytesPerRow, value)) RebuildRows(); } }
    public bool ShowAscii { get => _showAscii; set { if (SetField(ref _showAscii, value)) RebuildRows(); } }
    public CompareViewMode ViewMode { get => _viewMode; set { if (SetField(ref _viewMode, value)) RebuildRows(); } }

    public void SetAddressRange(uint start, uint end)
    {
        if (start > end) throw new ArgumentOutOfRangeException(nameof(start), "Range start must not exceed range end.");
        _rangeStart = start;
        _rangeEnd = end;
        if (_viewMode == CompareViewMode.AddressRange) RebuildRows();
    }

    public void Refresh(MemoryImage fileA, MemoryImage fileB)
    {
        _fileA = fileA ?? throw new ArgumentNullException(nameof(fileA));
        _fileB = fileB ?? throw new ArgumentNullException(nameof(fileB));
        OnPropertyChanged(nameof(FileA));
        OnPropertyChanged(nameof(FileB));
        RebuildRows();
    }

    public bool TryNavigateToAddress(uint address, out int rowIndex) => _rows.TryGetRowIndex(address, out rowIndex);

    public bool TryFindNextDifference(uint address, out uint difference) => TryFindDifference(address, forward: true, out difference);
    public bool TryFindPreviousDifference(uint address, out uint difference) => TryFindDifference(address, forward: false, out difference);

    private bool TryFindDifference(uint address, bool forward, out uint difference)
    {
        var streams = GetOccupiedAddresses();
        if (forward)
        {
            foreach (var current in streams)
            {
                if (current < address) continue;
                if (GetState(current) != CompareByteState.Equal) { difference = current; return true; }
            }
        }
        else
        {
            var found = false;
            difference = 0;
            foreach (var current in streams)
            {
                if (current > address) break;
                if (GetState(current) != CompareByteState.Equal) { difference = current; found = true; }
            }
            if (found) return true;
        }
        difference = 0;
        return false;
    }

    internal CompareByteState GetState(uint address)
    {
        var hasA = _fileA.TryGetByte(address, out var a);
        var hasB = _fileB.TryGetByte(address, out var b);
        if (hasA && hasB) return a == b ? CompareByteState.Equal : CompareByteState.Different;
        if (hasA) return CompareByteState.OnlyA;
        if (hasB) return CompareByteState.OnlyB;
        return CompareByteState.Equal;
    }

    internal ComparePanelRow CreatePanel(uint rowAddress, bool fileA)
    {
        var image = fileA ? _fileA : _fileB;
        var cells = new CompareByteCell[_bytesPerRow];
        var hex = new StringBuilder((_bytesPerRow * 3) - 1);
        var ascii = _showAscii ? new StringBuilder(_bytesPerRow) : null;
        for (var offset = 0; offset < _bytesPerRow; offset++)
        {
            if (offset > 0) hex.Append(' ');
            var rawAddress = (ulong)rowAddress + (uint)offset;
            byte value = 0;
            var hasValue = rawAddress <= uint.MaxValue && image.TryGetByte((uint)rawAddress, out value);
            var state = rawAddress <= uint.MaxValue ? GetState((uint)rawAddress) : CompareByteState.Equal;
            var text = hasValue ? value.ToString("X2") : string.Empty;
            var character = hasValue ? (value is >= 0x20 and <= 0x7E ? (char)value : '.') : ' ';
            cells[offset] = new CompareByteCell(text, character.ToString(), state, (uint)rawAddress);
            hex.Append(hasValue ? text : "  ");
            ascii?.Append(character);
        }
        return new ComparePanelRow(cells, hex.ToString(), ascii?.ToString());
    }

    private IEnumerable<uint> GetOccupiedAddresses()
    {
        var segments = _fileA.Segments.Concat(_fileB.Segments).OrderBy(segment => segment.StartAddress).ToArray();
        uint? last = null;
        foreach (var segment in segments)
        {
            for (var offset = 0; offset < segment.ByteCount; offset++)
            {
                var address = (uint)((ulong)segment.StartAddress + (uint)offset);
                if (last is not null && address <= last.Value) continue;
                last = address;
                yield return address;
            }
        }
    }

    private void RebuildRows()
    {
        _rows = new CompareRows(this);
        OnPropertyChanged(nameof(Rows));
    }

    private bool SetField<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return false;
        field = value;
        OnPropertyChanged(name);
        return true;
    }

    private void OnPropertyChanged([CallerMemberName] string? name = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

    private static void ValidateRowWidth(int width)
    {
        if (width is not (8 or 16 or 32)) throw new ArgumentOutOfRangeException(nameof(width), "Row width must be 8, 16, or 32 bytes.");
    }

    internal IEnumerable<uint> GetDifferenceRows()
    {
        uint? lastRow = null;
        foreach (var address in GetOccupiedAddresses())
        {
            if (GetState(address) == CompareByteState.Equal) continue;
            var row = address & ~((uint)_bytesPerRow - 1);
            if (lastRow == row) continue;
            lastRow = row;
            yield return row;
        }
    }

    internal IReadOnlyList<CompareRows.RowRange> BuildRanges()
    {
        var width = (uint)_bytesPerRow;
        if (_viewMode == CompareViewMode.DifferencesOnly)
        {
            var intervals = GetDifferenceRows().Select(address => ((ulong)address, (ulong)address));
            return BuildSparseRanges(intervals, width, includeOneBlankRow: false, markLargeGaps: false);
        }

        var intervalsToDisplay = _fileA.Segments.Concat(_fileB.Segments)
            .Select(segment =>
            {
                var start = (ulong)segment.StartAddress;
                var end = (ulong)segment.EndAddress;
                if (_viewMode == CompareViewMode.AddressRange)
                {
                    start = Math.Max(start, _rangeStart);
                    end = Math.Min(end, _rangeEnd);
                }
                return start <= end
                    ? ((ulong)((start / width) * width), (ulong)((end / width) * width))
                    : ((ulong)uint.MaxValue + 1, (ulong)uint.MaxValue + 1);
            })
            .Where(interval => interval.Item1 <= uint.MaxValue)
            .OrderBy(interval => interval.Item1);
        return BuildSparseRanges(intervalsToDisplay, width, includeOneBlankRow: true, markLargeGaps: true);
    }

    private static IReadOnlyList<CompareRows.RowRange> BuildSparseRanges(IEnumerable<(ulong Start, ulong End)> input, uint width, bool includeOneBlankRow, bool markLargeGaps)
    {
        var spans = input.OrderBy(item => item.Start).ToArray();
        if (spans.Length == 0) return Array.Empty<CompareRows.RowRange>();
        var ranges = new List<CompareRows.RowRange>();
        var index = 0;
        var start = spans[0].Start;
        var end = spans[0].End;
        foreach (var span in spans.Skip(1))
        {
            var mergeLimit = includeOneBlankRow ? end + width * 2UL : end + width;
            if (span.Start <= mergeLimit) { end = Math.Max(end, span.End); continue; }
            ranges.Add(CompareRows.RowRange.AddressSpan(index, start, end));
            index++;
            if (span.Start > end + width && markLargeGaps) ranges.Add(CompareRows.RowRange.Gap(index++, end + width, span.Start - width));
            start = span.Start;
            end = span.End;
        }
        ranges.Add(CompareRows.RowRange.AddressSpan(index, start, end));
        return ranges;
    }

    internal uint RangeStart => _rangeStart;
    internal uint RangeEnd => _rangeEnd;
}

public sealed record CompareByteCell(string Hex, string Ascii, CompareByteState State, uint Address);
public sealed record ComparePanelRow(IReadOnlyList<CompareByteCell> Cells, string HexData, string? Ascii);
public sealed record CompareViewRow(string Address, ComparePanelRow FileA, ComparePanelRow FileB, IReadOnlyList<CompareByteState> States, bool IsGapMarker, string? GapDescription);

internal sealed class CompareRows : IList<CompareViewRow>
{
    private readonly CompareViewModel _view;
    private readonly IReadOnlyList<RowRange> _ranges;
    private readonly int _count;
    private readonly uint _width;

    public CompareRows(CompareViewModel view)
    {
        _view = view;
        _width = (uint)view.BytesPerRow;
        _ranges = view.BuildRanges();
        var count = 0L;
        var mapped = new List<RowRange>(_ranges.Count);
        foreach (var range in _ranges)
        {
            var rows = range.IsGap ? 1 : checked((int)((range.EndAddress - range.StartAddress) / _width) + 1);
            mapped.Add(range with { StartIndex = checked((int)count), Count = rows });
            count += rows;
        }
        _ranges = mapped;
        _count = checked((int)count);
    }

    public int Count => _count;
    public bool IsReadOnly => true;
    public CompareViewRow this[int index]
    {
        get
        {
            if ((uint)index >= (uint)_count) throw new ArgumentOutOfRangeException(nameof(index));
            var range = FindRange(index);
            if (range.IsGap) return new CompareViewRow(string.Empty, EmptyPanel(), EmptyPanel(), Array.Empty<CompareByteState>(), true, $"Skipped 0x{range.StartAddress:X8}–0x{range.EndAddress:X8}");
            var address = checked((uint)(range.StartAddress + (ulong)(index - range.StartIndex) * _width));
            var a = _view.CreatePanel(address, true);
            var b = _view.CreatePanel(address, false);
            var states = Enumerable.Range(0, (int)_width).Select(offset => (ulong)address + (uint)offset <= uint.MaxValue ? _view.GetState((uint)((ulong)address + (uint)offset)) : CompareByteState.Equal).ToArray();
            return new CompareViewRow(address.ToString("X8"), a, b, states, false, null);
        }
        set => throw new NotSupportedException();
    }

    public bool TryGetRowIndex(uint address, out int rowIndex)
    {
        var aligned = (ulong)(address / _width) * _width;
        for (var i = 0; i < _ranges.Count; i++)
        {
            var range = _ranges[i];
            if (range.IsGap)
            {
                if (aligned >= range.StartAddress && aligned <= range.EndAddress) { rowIndex = range.StartIndex; return true; }
            }
            else if (aligned >= range.StartAddress && aligned <= range.EndAddress)
            {
                rowIndex = range.StartIndex + checked((int)((aligned - range.StartAddress) / _width));
                return true;
            }
        }
        rowIndex = -1;
        return false;
    }

    private RowRange FindRange(int index)
    {
        var low = 0;
        var high = _ranges.Count - 1;
        while (low <= high)
        {
            var middle = low + ((high - low) / 2);
            var range = _ranges[middle];
            if (index < range.StartIndex) high = middle - 1;
            else if (index >= range.StartIndex + range.Count) low = middle + 1;
            else return range;
        }
        throw new InvalidOperationException("The row index is not represented by the sparse comparison map.");
    }

    private static ComparePanelRow EmptyPanel() => new(Array.Empty<CompareByteCell>(), string.Empty, null);
    public IEnumerator<CompareViewRow> GetEnumerator() { for (var i = 0; i < Count; i++) yield return this[i]; }
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    public int IndexOf(CompareViewRow item) => Enumerable.Range(0, Count).FirstOrDefault(i => Equals(this[i], item), -1);
    public bool Contains(CompareViewRow item) => IndexOf(item) >= 0;
    public void CopyTo(CompareViewRow[] array, int arrayIndex) { for (var i = 0; i < Count; i++) array[arrayIndex + i] = this[i]; }
    public void Add(CompareViewRow item) => throw new NotSupportedException();
    public void Clear() => throw new NotSupportedException();
    public void Insert(int index, CompareViewRow item) => throw new NotSupportedException();
    public bool Remove(CompareViewRow item) => throw new NotSupportedException();
    public void RemoveAt(int index) => throw new NotSupportedException();

    internal sealed record RowRange(int StartIndex, int Count, ulong StartAddress, ulong EndAddress, bool IsGap)
    {
        public static RowRange Rows(int index, ulong start, int count) => new(index, count, start, start, false);
        public static RowRange AddressSpan(int index, ulong start, ulong end) => new(index, 0, start, end, false);
        public static RowRange Gap(int index, ulong start, ulong end) => new(index, 1, start, end, true);
    }
}
