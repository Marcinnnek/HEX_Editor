using System.Collections.Specialized;

namespace HexEditor.Compare;

internal readonly record struct SparseRowSpan(ulong StartAddress, ulong EndAddress, bool IsGap = false);

internal sealed record IndexedSparseRowSpan(ulong StartAddress, ulong EndAddress, bool IsGap, int StartIndex, int Count);

internal readonly record struct SparseRowPosition(bool IsGap, ulong RowAddress, ulong GapStartAddress, ulong GapEndAddress);

internal static class AddressRowGrid
{
    private const ulong RegionSize = 0x01000000;
    private const ulong RegionMask = 0xFF000000;

    public static ulong RegionStart(ulong address) => address & RegionMask;

    public static ulong RegionEnd(ulong address) => Math.Min(RegionStart(address) + RegionSize - 1, uint.MaxValue);

    public static ulong RowStart(ulong address, uint rowWidth)
    {
        var regionStart = RegionStart(address);
        return regionStart + ((address - regionStart) / rowWidth) * rowWidth;
    }

    public static ulong RowEnd(ulong rowStart, uint rowWidth) =>
        Math.Min(rowStart + rowWidth - 1, RegionEnd(rowStart));

    public static IEnumerable<(ulong First, ulong Last)> RowsForRange(ulong startAddress, ulong endAddress, uint rowWidth)
    {
        var current = startAddress;
        while (current <= endAddress)
        {
            var regionEnd = RegionEnd(current);
            var chunkEnd = Math.Min(endAddress, regionEnd);
            yield return (RowStart(current, rowWidth), RowStart(chunkEnd, rowWidth));
            current = chunkEnd + 1;
        }
    }
}

/// <summary>Indexes visible address spans and collapsed gaps without materializing each row.</summary>
internal sealed class SparseRowList<T> : ReadOnlyListAdapter<T>, INotifyCollectionChanged where T : class
{
    private IReadOnlyList<IndexedSparseRowSpan> _spans;
    private readonly Func<SparseRowPosition, T> _createItem;
    private readonly uint _rowWidth;
    private int _count;

    public SparseRowList(IEnumerable<SparseRowSpan> spans, int bytesPerRow, Func<SparseRowPosition, T> createItem,
        object? sourceIdentity = null)
    {
        if (bytesPerRow <= 0) throw new ArgumentOutOfRangeException(nameof(bytesPerRow));
        _rowWidth = (uint)bytesPerRow;
        _createItem = createItem ?? throw new ArgumentNullException(nameof(createItem));
        SourceIdentity = sourceIdentity;

        (_spans, _count) = SparseRowIndexBuilder.Build(spans, _rowWidth);
    }

    public override int Count => _count;
    public object? SourceIdentity { get; }
    public event NotifyCollectionChangedEventHandler? CollectionChanged;

    protected override T GetAt(int index)
    {
        if ((uint)index >= (uint)Count) throw new ArgumentOutOfRangeException(nameof(index));
        var span = FindSpan(index);
        var position = span.IsGap
            ? new SparseRowPosition(true, 0, span.StartAddress, span.EndAddress)
            : new SparseRowPosition(false,
                span.StartAddress + (ulong)(index - span.StartIndex) * _rowWidth, 0, 0);
        return _createItem(position);
    }

    public bool TryGetRowIndex(uint address, out int rowIndex)
    {
        var low = 0;
        var high = _spans.Count - 1;
        while (low <= high)
        {
            var middle = low + ((high - low) / 2);
            var span = _spans[middle];
            var spanEnd = span.IsGap ? span.EndAddress : AddressRowGrid.RowEnd(span.EndAddress, _rowWidth);
            if (middle + 1 < _spans.Count)
                spanEnd = Math.Min(spanEnd, _spans[middle + 1].StartAddress - 1);
            if (address < span.StartAddress)
            {
                high = middle - 1;
            }
            else if (address > spanEnd)
            {
                low = middle + 1;
            }
            else
            {
                var rowAddress = span.IsGap
                    ? Math.Max(span.StartAddress, AddressRowGrid.RowStart(address, _rowWidth))
                    : span.StartAddress + ((address - span.StartAddress) / _rowWidth) * _rowWidth;
                if (span.IsGap)
                {
                    ExpandNavigationRow(span, rowAddress);
                    return TryGetRowIndex(address, out rowIndex);
                }

                rowIndex = span.StartIndex + checked((int)((rowAddress - span.StartAddress) / _rowWidth));
                return true;
            }
        }

        rowIndex = -1;
        return false;
    }

    private void ExpandNavigationRow(IndexedSparseRowSpan targetGap, ulong rowAddress)
    {
        var ranges = new List<SparseRowSpan>(_spans.Count + 2);
        foreach (var span in _spans)
        {
            if (!ReferenceEquals(span, targetGap))
            {
                ranges.Add(new SparseRowSpan(span.StartAddress, span.EndAddress, span.IsGap));
                continue;
            }

            if (rowAddress > span.StartAddress)
                ranges.Add(new SparseRowSpan(span.StartAddress, rowAddress - 1, IsGap: true));
            ranges.Add(new SparseRowSpan(rowAddress, rowAddress));
            var afterRow = AddressRowGrid.RowEnd(rowAddress, _rowWidth) + 1;
            if (afterRow <= span.EndAddress)
                ranges.Add(new SparseRowSpan(afterRow, span.EndAddress, IsGap: true));
        }

        (_spans, _count) = SparseRowIndexBuilder.Build(ranges, _rowWidth);
        ClearCachedRows();
        CollectionChanged?.Invoke(this, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
    }

    private IndexedSparseRowSpan FindSpan(int index)
    {
        var low = 0;
        var high = _spans.Count - 1;
        while (low <= high)
        {
            var middle = low + ((high - low) / 2);
            var span = _spans[middle];
            if (index < span.StartIndex) high = middle - 1;
            else if (index >= span.StartIndex + span.Count) low = middle + 1;
            else return span;
        }

        throw new InvalidOperationException("The row index is not represented by the sparse row map.");
    }

}

internal static class SparseRowIndexBuilder
{
    public static (IReadOnlyList<IndexedSparseRowSpan> Spans, int Count) Build(IEnumerable<SparseRowSpan> spans, uint rowWidth)
    {
        var indexed = new List<IndexedSparseRowSpan>();
        long nextIndex = 0;
        foreach (var span in spans)
        {
            if (span.StartAddress > span.EndAddress || span.EndAddress > uint.MaxValue)
                throw new ArgumentOutOfRangeException(nameof(spans), "Sparse row spans must be ordered valid addresses.");
            var rowCount = span.IsGap ? 1L : checked((long)(((span.EndAddress - span.StartAddress) / rowWidth) + 1));
            indexed.Add(new IndexedSparseRowSpan(span.StartAddress, span.EndAddress, span.IsGap,
                checked((int)nextIndex), checked((int)rowCount)));
            nextIndex += rowCount;
        }

        return (indexed, checked((int)nextIndex));
    }
}
