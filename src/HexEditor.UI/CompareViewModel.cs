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
    private uint? _navigationAddress;
    private SparseRowList<CompareViewRow> _rows;
    private ComparePanelRows _fileARows;
    private ComparePanelRows _fileBRows;
    private string _comparisonStatus = "No differences";

    public CompareViewModel(MemoryImage fileA, MemoryImage fileB, int bytesPerRow = 16, bool showAscii = true)
    {
        _fileA = fileA ?? throw new ArgumentNullException(nameof(fileA));
        _fileB = fileB ?? throw new ArgumentNullException(nameof(fileB));
        ValidateRowWidth(bytesPerRow);
        _bytesPerRow = bytesPerRow;
        _showAscii = showAscii;
        _rows = CreateRows();
        _fileARows = new ComparePanelRows(this, fileA: true);
        _fileBRows = new ComparePanelRows(this, fileA: false);
        UpdateComparisonStatus();
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    public IList<CompareViewRow> Rows => _rows;
    public IList<HexViewRow> FileARows => _fileARows;
    public IList<HexViewRow> FileBRows => _fileBRows;
    public MemoryImage FileA => _fileA;
    public MemoryImage FileB => _fileB;
    public int BytesPerRow { get => _bytesPerRow; set { ValidateRowWidth(value); if (SetField(ref _bytesPerRow, value)) RebuildRows(); } }
    public bool ShowAscii { get => _showAscii; set { if (SetField(ref _showAscii, value)) RebuildRows(); } }
    public CompareViewMode ViewMode { get => _viewMode; set { if (SetField(ref _viewMode, value)) RebuildRows(); } }
    public string ComparisonStatus => _comparisonStatus;

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
        UpdateComparisonStatus();
        RebuildRows();
    }

    public bool TryNavigateToAddress(uint address, out int rowIndex)
    {
        _navigationAddress = address;
        RebuildRows();
        return _rows.TryGetRowIndex(address, out rowIndex) && !_rows[rowIndex].IsGapMarker;
    }

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
            var inSelectedRange = rawAddress <= uint.MaxValue && IsInSelectedAddressRange((uint)rawAddress);
            byte value = 0;
            var hasValue = inSelectedRange && rawAddress <= uint.MaxValue && image.TryGetByte((uint)rawAddress, out value);
            var state = inSelectedRange && rawAddress <= uint.MaxValue ? GetState((uint)rawAddress) : CompareByteState.Equal;
            var text = hasValue ? value.ToString("X2") : string.Empty;
            var character = hasValue ? (value is >= 0x20 and <= 0x7E ? (char)value : '.') : ' ';
            cells[offset] = new CompareByteCell(text, character.ToString(), state,
                inSelectedRange && rawAddress <= uint.MaxValue ? (uint)rawAddress : null);
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
        _rows = CreateRows();
        _fileARows = new ComparePanelRows(this, fileA: true);
        _fileBRows = new ComparePanelRows(this, fileA: false);
        OnPropertyChanged(nameof(Rows));
        OnPropertyChanged(nameof(FileARows));
        OnPropertyChanged(nameof(FileBRows));
    }

    private void UpdateComparisonStatus()
    {
        long different = 0;
        long onlyA = 0;
        long onlyB = 0;
        foreach (var address in GetOccupiedAddresses())
        {
            switch (GetState(address))
            {
                case CompareByteState.Different: different++; break;
                case CompareByteState.OnlyA: onlyA++; break;
                case CompareByteState.OnlyB: onlyB++; break;
            }
        }

        var total = different + onlyA + onlyB;
        _comparisonStatus = total == 0
            ? "No differences"
            : $"{total:N0} differences ({different:N0} different, {onlyA:N0} only in File A, {onlyB:N0} only in File B)";
        OnPropertyChanged(nameof(ComparisonStatus));
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

    private SparseRowList<CompareViewRow> CreateRows() =>
        new(BuildRanges(), _bytesPerRow, position =>
        {
            if (position.IsGap)
            {
                var description = $"Skipped 0x{position.GapStartAddress:X8}...0x{position.GapEndAddress:X8}";
                return new CompareViewRow(string.Empty, EmptyPanel(), EmptyPanel(), Array.Empty<CompareByteState>(), true, description);
            }

            var address = (uint)position.RowAddress;
            var a = CreatePanel(address, true);
            var b = CreatePanel(address, false);
            var states = Enumerable.Range(0, _bytesPerRow).Select(offset =>
            {
                var cellAddress = (ulong)address + (uint)offset;
                return cellAddress <= uint.MaxValue && IsInSelectedAddressRange((uint)cellAddress)
                    ? GetState((uint)cellAddress)
                    : CompareByteState.Equal;
            }).ToArray();
            return new CompareViewRow(address.ToString("X8"), a, b, states, false, null);
        });

    private IReadOnlyList<SparseRowSpan> BuildRanges()
    {
        var width = (uint)_bytesPerRow;
        if (_viewMode == CompareViewMode.DifferencesOnly)
        {
            var intervals = GetDifferenceRows().Select(address => ((ulong)address, (ulong)address));
            if (_navigationAddress is uint navigationAddress)
            {
                var rowAddress = (ulong)(navigationAddress / width) * width;
                intervals = intervals.Append((rowAddress, rowAddress));
            }
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
            .ToList();
        if (_navigationAddress is uint address &&
            (_viewMode != CompareViewMode.AddressRange || IsInSelectedAddressRange(address)))
        {
            var rowAddress = (ulong)(address / width) * width;
            intervalsToDisplay.Add((rowAddress, rowAddress));
        }
        intervalsToDisplay.Sort((left, right) => left.Item1.CompareTo(right.Item1));
        return BuildSparseRanges(intervalsToDisplay, width, includeOneBlankRow: true, markLargeGaps: true);
    }

    private static IReadOnlyList<SparseRowSpan> BuildSparseRanges(IEnumerable<(ulong Start, ulong End)> input, uint width, bool includeOneBlankRow, bool markLargeGaps)
    {
        var spans = input.OrderBy(item => item.Start).ToArray();
        if (spans.Length == 0) return Array.Empty<SparseRowSpan>();
        var ranges = new List<SparseRowSpan>();
        var start = spans[0].Start;
        var end = spans[0].End;
        foreach (var span in spans.Skip(1))
        {
            var mergeLimit = includeOneBlankRow ? end + width * 2UL : end + width;
            if (span.Start <= mergeLimit) { end = Math.Max(end, span.End); continue; }
            ranges.Add(new SparseRowSpan(start, end));
            if (span.Start > end + width && markLargeGaps)
                ranges.Add(new SparseRowSpan(end + width, span.Start - width, IsGap: true));
            start = span.Start;
            end = span.End;
        }
        ranges.Add(new SparseRowSpan(start, end));
        return ranges;
    }

    private static ComparePanelRow EmptyPanel() => new(Array.Empty<CompareByteCell>(), string.Empty, null);
    internal bool IsInSelectedAddressRange(uint address) =>
        _viewMode != CompareViewMode.AddressRange || address >= _rangeStart && address <= _rangeEnd;
}

internal sealed class ComparePanelRows : IList<HexViewRow>
{
    private readonly CompareViewModel _view;
    private readonly bool _fileA;

    public ComparePanelRows(CompareViewModel view, bool fileA)
    {
        _view = view;
        _fileA = fileA;
    }

    public int Count => _view.Rows.Count;
    public bool IsReadOnly => true;
    public HexViewRow this[int index]
    {
        get
        {
            var row = _view.Rows[index];
            if (row.IsGapMarker)
                return new HexViewRow(row.Address, string.Empty, null, true, row.GapDescription);

            var panel = _fileA ? row.FileA : row.FileB;
            var cells = panel.Cells.Select(cell => new HexByteCell(
                cell.Hex,
                cell.Ascii,
                cell.Address,
                cell.State switch
                {
                    CompareByteState.Different => "Different",
                    CompareByteState.OnlyA when _fileA => "OnlyInPanel",
                    CompareByteState.OnlyB when !_fileA => "OnlyInPanel",
                    CompareByteState.OnlyA or CompareByteState.OnlyB => "OnlyOther",
                    _ => string.Empty
                })).ToArray();
            return new HexViewRow(row.Address, panel.HexData, panel.Ascii, false, null) { Cells = cells };
        }
        set => throw new NotSupportedException();
    }

    public int IndexOf(HexViewRow item) => Enumerable.Range(0, Count).FirstOrDefault(index => Equals(this[index], item), -1);
    public bool Contains(HexViewRow item) => IndexOf(item) >= 0;
    public void CopyTo(HexViewRow[] array, int arrayIndex) { for (var i = 0; i < Count; i++) array[arrayIndex + i] = this[i]; }
    public IEnumerator<HexViewRow> GetEnumerator() { for (var i = 0; i < Count; i++) yield return this[i]; }
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    public void Add(HexViewRow item) => throw new NotSupportedException();
    public void Clear() => throw new NotSupportedException();
    public void Insert(int index, HexViewRow item) => throw new NotSupportedException();
    public bool Remove(HexViewRow item) => throw new NotSupportedException();
    public void RemoveAt(int index) => throw new NotSupportedException();
}

public sealed record CompareByteCell(string Hex, string Ascii, CompareByteState State, uint? Address);
public sealed record ComparePanelRow(IReadOnlyList<CompareByteCell> Cells, string HexData, string? Ascii);
public sealed record CompareViewRow(string Address, ComparePanelRow FileA, ComparePanelRow FileB, IReadOnlyList<CompareByteState> States, bool IsGapMarker, string? GapDescription);
