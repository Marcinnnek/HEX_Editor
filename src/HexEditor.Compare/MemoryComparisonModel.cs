using System.Text;
using HexEditor.Core;
using System.Collections.Specialized;

namespace HexEditor.Compare;

public enum CompareByteState { Equal, Different, OnlyA, OnlyB }
public enum CompareViewMode { AllRegions, DifferencesOnly, AddressRange }

public sealed class MemoryComparisonModel
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
    private string _comparisonStatus = "No differences";

    public MemoryComparisonModel(MemoryImage fileA, MemoryImage fileB, int bytesPerRow = 16, bool showAscii = true,
        CancellationToken cancellationToken = default, IProgress<ComparisonProgress>? progress = null)
    {
        _fileA = fileA ?? throw new ArgumentNullException(nameof(fileA));
        _fileB = fileB ?? throw new ArgumentNullException(nameof(fileB));
        ValidateRowWidth(bytesPerRow);
        _bytesPerRow = bytesPerRow;
        _showAscii = showAscii;
        _rows = CreateRows();
        UpdateComparisonStatus(cancellationToken, progress);
    }

    public event NotifyCollectionChangedEventHandler? RowsChanged;
    public IList<CompareViewRow> Rows => _rows;
    public MemoryImage FileA => _fileA;
    public MemoryImage FileB => _fileB;
    public int BytesPerRow { get => _bytesPerRow; set { ValidateRowWidth(value); if (_bytesPerRow != value) { _bytesPerRow = value; RebuildRows(); } } }
    public bool ShowAscii { get => _showAscii; set { if (_showAscii != value) { _showAscii = value; RebuildRows(); } } }
    public CompareViewMode ViewMode { get => _viewMode; set { if (_viewMode != value) { _viewMode = value; RebuildRows(); } } }
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
        UpdateComparisonStatus(CancellationToken.None, null);
        RebuildRows();
    }

    public bool TryNavigateToAddress(uint address, out int rowIndex)
    {
        if (!IsInSelectedAddressRange(address))
        {
            rowIndex = -1;
            return false;
        }

        if (_viewMode == CompareViewMode.DifferencesOnly)
        {
            if (_rows.TryGetRowIndex(address, out rowIndex) && !_rows[rowIndex].IsGapMarker)
            {
                _navigationAddress = address;
                return true;
            }
            rowIndex = -1;
            return false;
        }

        _navigationAddress = address;
        if (_rows.TryGetRowIndex(address, out rowIndex))
            return !_rows[rowIndex].IsGapMarker;

        RebuildRows();
        return _rows.TryGetRowIndex(address, out rowIndex) && !_rows[rowIndex].IsGapMarker;
    }

    public bool TryFindNextDifference(uint address, out uint difference) => TryFindDifference(address, forward: true, out difference);
    public bool TryFindPreviousDifference(uint address, out uint difference) => TryFindDifference(address, forward: false, out difference);

    public bool TryFindNextDifferenceFromSelection(uint? selectedAddress, out uint difference) =>
        TryFindDifferenceGroup(selectedAddress, forward: true, out difference);

    public bool TryFindPreviousDifferenceFromSelection(uint? selectedAddress, out uint difference) =>
        TryFindDifferenceGroup(selectedAddress, forward: false, out difference);

    public bool TryFindNextDifferenceFromAddress(uint? selectedAddress, uint? anchorAddress, out uint difference) =>
        TryFindDifferenceFromAddress(selectedAddress, anchorAddress, forward: true, out difference);

    public bool TryFindPreviousDifferenceFromAddress(uint? selectedAddress, uint? anchorAddress, out uint difference) =>
        TryFindDifferenceFromAddress(selectedAddress, anchorAddress, forward: false, out difference);

    private bool TryFindDifferenceFromAddress(uint? selectedAddress, uint? anchorAddress, bool forward, out uint difference)
    {
        var position = selectedAddress ?? anchorAddress;
        return TryFindDifferenceGroup(position, forward, out difference);
    }

    private bool TryFindDifferenceGroup(uint? position, bool forward, out uint difference)
    {
        uint? previousEnd = null;
        foreach (var (start, end) in GetDifferenceGroups())
        {
            if (forward)
            {
                if (position is null || start > position.Value)
                {
                    difference = start;
                    return true;
                }
            }
            else
            {
                if (position is not null && start >= position.Value) break;
                if (position is not null && end >= position.Value) break;
                previousEnd = end;
            }
        }
        if (!forward && previousEnd is uint previous)
        {
            difference = previous;
            return true;
        }
        return SetNoDifference(out difference);
    }

    private IEnumerable<(uint Start, uint End)> GetDifferenceGroups()
    {
        uint? start = null;
        uint end = 0;
        foreach (var address in GetOccupiedAddresses())
        {
            if (!IsInSelectedAddressRange(address) || GetState(address) == CompareByteState.Equal)
            {
                if (start is uint completed) yield return (completed, end);
                start = null;
                continue;
            }
            if (start is null)
                start = address;
            else if ((ulong)end + 1 != address)
            {
                yield return (start.Value, end);
                start = address;
            }
            end = address;
        }
        if (start is uint last) yield return (last, end);
    }

    private static bool SetNoDifference(out uint difference)
    {
        difference = 0;
        return false;
    }

    private bool TryFindDifference(uint address, bool forward, out uint difference)
    {
        var streams = GetOccupiedAddresses();
        if (forward)
        {
            foreach (var current in streams)
            {
                if (current < address) continue;
                if (IsInSelectedAddressRange(current) && GetState(current) != CompareByteState.Equal) { difference = current; return true; }
            }
        }
        else
        {
            var found = false;
            difference = 0;
            foreach (var current in streams)
            {
                if (current > address) break;
                if (IsInSelectedAddressRange(current) && GetState(current) != CompareByteState.Equal) { difference = current; found = true; }
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
        var regionEnd = AddressRowGrid.RegionEnd(rowAddress);
        var cells = new CompareByteCell[_bytesPerRow];
        var hex = new StringBuilder((_bytesPerRow * 3) - 1);
        var ascii = _showAscii ? new StringBuilder(_bytesPerRow) : null;
        for (var offset = 0; offset < _bytesPerRow; offset++)
        {
            if (offset > 0) hex.Append(' ');
            var rawAddress = (ulong)rowAddress + (uint)offset;
            var inSelectedRange = rawAddress <= regionEnd && IsInSelectedAddressRange((uint)rawAddress);
            byte value = 0;
            var hasValue = inSelectedRange && image.TryGetByte((uint)rawAddress, out value);
            var state = inSelectedRange ? GetState((uint)rawAddress) : CompareByteState.Equal;
            var text = ByteText.FromByte(hasValue ? value : null);
            cells[offset] = new CompareByteCell(text.Hex, text.Ascii.ToString(), state,
                inSelectedRange ? (uint)rawAddress : null);
            hex.Append(text.HexColumn);
            ascii?.Append(text.Ascii);
        }
        return new ComparePanelRow(cells, hex.ToString(), ascii?.ToString());
    }

    private IEnumerable<uint> GetOccupiedAddresses()
    {
        ulong? last = null;
        foreach (var range in _fileA.Ranges.Concat(_fileB.Ranges).OrderBy(range => range.StartAddress))
        {
            for (ulong address = range.StartAddress; address <= range.EndAddress; address++)
            {
                if (last is not null && address <= last.Value) continue;
                last = address;
                yield return (uint)address;
            }
        }
    }

    private void RebuildRows()
    {
        _rows = CreateRows();
        RowsChanged?.Invoke(this, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
    }

    private void UpdateComparisonStatus(CancellationToken cancellationToken, IProgress<ComparisonProgress>? progress)
    {
        if (ImagesHaveSameRanges(_fileA, _fileB))
        {
            cancellationToken.ThrowIfCancellationRequested();
            _comparisonStatus = "No differences";
            progress?.Report(new ComparisonProgress("Comparison complete", (long)Math.Min(_fileA.OccupiedByteCount, long.MaxValue),
                (long)Math.Min(_fileA.OccupiedByteCount, long.MaxValue)));
            cancellationToken.ThrowIfCancellationRequested();
            return;
        }

        if (TrySummarizeRepeatedRanges(cancellationToken, progress, out var repeatedStatus))
        {
            _comparisonStatus = repeatedStatus;
            return;
        }

        long different = 0;
        long onlyA = 0;
        long onlyB = 0;
        long completed = 0;
        foreach (var address in GetOccupiedAddresses())
        {
            if ((completed & 0xFFFF) == 0)
            {
                cancellationToken.ThrowIfCancellationRequested();
                progress?.Report(new ComparisonProgress("Comparing occupied bytes", completed));
            }
            switch (GetState(address))
            {
                case CompareByteState.Different: different++; break;
                case CompareByteState.OnlyA: onlyA++; break;
                case CompareByteState.OnlyB: onlyB++; break;
            }
            completed++;
        }
        cancellationToken.ThrowIfCancellationRequested();
        progress?.Report(new ComparisonProgress("Comparison complete", completed, completed));

        var total = different + onlyA + onlyB;
        _comparisonStatus = total == 0
            ? "No differences"
            : $"{total:N0} differences ({different:N0} different, {onlyA:N0} only in File A, {onlyB:N0} only in File B)";
    }

    private bool TrySummarizeRepeatedRanges(CancellationToken cancellationToken, IProgress<ComparisonProgress>? progress, out string status)
    {
        status = string.Empty;
        if (_fileA.Ranges.Count > 1 || _fileB.Ranges.Count > 1) return false;
        var rangeA = _fileA.Ranges.Count == 1 ? _fileA.Ranges[0] : null;
        var rangeB = _fileB.Ranges.Count == 1 ? _fileB.Ranges[0] : null;
        if ((rangeA is not null && !rangeA.IsRepeated) || (rangeB is not null && !rangeB.IsRepeated)) return false;
        if (rangeA is null && rangeB is null) { status = "No differences"; return true; }
        var start = rangeA?.StartAddress ?? rangeB!.StartAddress;
        var end = rangeA?.EndAddress ?? rangeB!.EndAddress;
        if (rangeA is not null && rangeB is not null &&
            (rangeA.StartAddress != rangeB.StartAddress || rangeA.EndAddress != rangeB.EndAddress)) return false;

        var cycle = rangeA is null ? (ulong)rangeB!.RepetitionLength : rangeB is null ? (ulong)rangeA.RepetitionLength :
            (ulong)LeastCommonMultiple(rangeA.RepetitionLength, rangeB.RepetitionLength);
        var length = (ulong)end - start + 1;
        var sampleLength = Math.Min(length, (ulong)cycle);
        long different = 0, onlyA = 0, onlyB = 0;
        for (ulong offset = 0; offset < sampleLength; offset++)
        {
            if ((offset & 0xFFF) == 0)
            {
                cancellationToken.ThrowIfCancellationRequested();
                progress?.Report(new ComparisonProgress("Comparing repeated bytes", (long)offset, (long)length));
            }
            switch (GetState((uint)((ulong)start + offset)))
            {
                case CompareByteState.Different: different++; break;
                case CompareByteState.OnlyA: onlyA++; break;
                case CompareByteState.OnlyB: onlyB++; break;
            }
        }
        var repetitions = length / sampleLength;
        var remainder = length % sampleLength;
        different *= (long)repetitions;
        onlyA *= (long)repetitions;
        onlyB *= (long)repetitions;
        for (ulong offset = 0; offset < remainder; offset++)
        {
            switch (GetState((uint)((ulong)start + offset)))
            {
                case CompareByteState.Different: different++; break;
                case CompareByteState.OnlyA: onlyA++; break;
                case CompareByteState.OnlyB: onlyB++; break;
            }
        }
        cancellationToken.ThrowIfCancellationRequested();
        progress?.Report(new ComparisonProgress("Comparison complete", (long)length, (long)length));
        cancellationToken.ThrowIfCancellationRequested();
        var total = different + onlyA + onlyB;
        status = total == 0 ? "No differences" :
            $"{total:N0} differences ({different:N0} different, {onlyA:N0} only in File A, {onlyB:N0} only in File B)";
        return true;
    }

    private static long LeastCommonMultiple(int left, int right) => (long)left / GreatestCommonDivisor(left, right) * right;
    private static int GreatestCommonDivisor(int left, int right)
    {
        while (right != 0) (left, right) = (right, left % right);
        return left;
    }

    private static bool ImagesHaveSameRanges(MemoryImage left, MemoryImage right)
    {
        if (left.Ranges.Count != right.Ranges.Count) return false;
        for (var index = 0; index < left.Ranges.Count; index++)
            if (!left.Ranges[index].ContentEquals(right.Ranges[index])) return false;
        return true;
    }

    private static void ValidateRowWidth(int width)
    {
        if (width is < 1 or > 128) throw new ArgumentOutOfRangeException(nameof(width), "Row width must be between 1 and 128 bytes.");
    }

    internal IEnumerable<uint> GetDifferenceRows()
    {
        uint? lastRow = null;
        foreach (var address in GetOccupiedAddresses())
        {
            if (GetState(address) == CompareByteState.Equal) continue;
            var row = (uint)AddressRowGrid.RowStart(address, (uint)_bytesPerRow);
            if (lastRow == row) continue;
            lastRow = row;
            yield return row;
        }
    }

    private SparseRowList<CompareViewRow> CreateRows() =>
        new(DisplayedAddressRanges(), _bytesPerRow, position =>
        {
            if (position.IsGap)
            {
                var description = $"Empty 0x{position.GapStartAddress:X8}...0x{position.GapEndAddress:X8}";
                return new CompareViewRow(string.Empty, EmptyPanel(), EmptyPanel(), Array.Empty<CompareByteState>(), true, description);
            }

            var address = (uint)position.RowAddress;
            var a = CreatePanel(address, true);
            var b = CreatePanel(address, false);
            var regionEnd = AddressRowGrid.RegionEnd(address);
            var states = Enumerable.Range(0, _bytesPerRow).Select(offset =>
            {
                var cellAddress = (ulong)address + (uint)offset;
                return cellAddress <= regionEnd && IsInSelectedAddressRange((uint)cellAddress)
                    ? GetState((uint)cellAddress)
                    : CompareByteState.Equal;
            }).ToArray();
            return new CompareViewRow(address.ToString("X8"), a, b, states, false, null);
        }, layout: _viewMode == CompareViewMode.DifferencesOnly
            ? SparseRowLayout.Compact : SparseRowLayout.WithGaps);

    private IEnumerable<(ulong StartAddress, ulong EndAddress)> DisplayedAddressRanges()
    {
        if (_viewMode == CompareViewMode.DifferencesOnly)
            return GetDifferenceRows().Select(address => ((ulong)address, (ulong)address));

        var intervalsToDisplay = _fileA.Ranges.Concat(_fileB.Ranges)
            .Select(range =>
            {
                var start = (ulong)range.StartAddress;
                var end = (ulong)range.EndAddress;
                if (_viewMode == CompareViewMode.AddressRange)
                {
                    start = Math.Max(start, _rangeStart);
                    end = Math.Min(end, _rangeEnd);
                }
                return (StartAddress: start, EndAddress: end);
            })
            .Where(range => range.StartAddress <= range.EndAddress)
            .ToList();
        if (_navigationAddress is uint address &&
            (_viewMode != CompareViewMode.AddressRange || IsInSelectedAddressRange(address)))
            intervalsToDisplay.Add((address, address));
        return intervalsToDisplay;
    }

    private static ComparePanelRow EmptyPanel() => new(Array.Empty<CompareByteCell>(), string.Empty, null);
    internal bool IsInSelectedAddressRange(uint address) =>
        _viewMode != CompareViewMode.AddressRange || address >= _rangeStart && address <= _rangeEnd;
}

public sealed record ComparisonProgress(string Stage, long Completed, long? Total = null);

public sealed record CompareByteCell(string Hex, string Ascii, CompareByteState State, uint? Address);
public sealed record ComparePanelRow(IReadOnlyList<CompareByteCell> Cells, string HexData, string? Ascii);
public sealed record CompareViewRow(string Address, ComparePanelRow FileA, ComparePanelRow FileB, IReadOnlyList<CompareByteState> States, bool IsGapMarker, string? GapDescription);
