using System.Collections;

namespace HexEditor.UI;

internal readonly record struct SparseRowSpan(ulong StartAddress, ulong EndAddress, bool IsGap = false);

internal readonly record struct SparseRowPosition(bool IsGap, ulong RowAddress, ulong GapStartAddress, ulong GapEndAddress);

/// <summary>Indexes visible address spans and collapsed gaps without materializing each row.</summary>
internal sealed class SparseRowList<T> : IList<T>
{
    private readonly IReadOnlyList<IndexedSpan> _spans;
    private readonly Func<SparseRowPosition, T> _createItem;
    private readonly uint _rowWidth;

    public SparseRowList(IEnumerable<SparseRowSpan> spans, int bytesPerRow, Func<SparseRowPosition, T> createItem)
    {
        if (bytesPerRow <= 0) throw new ArgumentOutOfRangeException(nameof(bytesPerRow));
        _rowWidth = (uint)bytesPerRow;
        _createItem = createItem ?? throw new ArgumentNullException(nameof(createItem));

        var indexed = new List<IndexedSpan>();
        long nextIndex = 0;
        foreach (var span in spans)
        {
            if (span.StartAddress > span.EndAddress || span.EndAddress > uint.MaxValue)
                throw new ArgumentOutOfRangeException(nameof(spans), "Sparse row spans must be ordered valid addresses.");
            var rowCount = span.IsGap ? 1L : checked((long)(((span.EndAddress - span.StartAddress) / _rowWidth) + 1));
            indexed.Add(new IndexedSpan(span.StartAddress, span.EndAddress, span.IsGap,
                checked((int)nextIndex), checked((int)rowCount)));
            nextIndex += rowCount;
        }

        _spans = indexed;
        Count = checked((int)nextIndex);
    }

    public int Count { get; }
    public bool IsReadOnly => true;

    public T this[int index]
    {
        get
        {
            if ((uint)index >= (uint)Count) throw new ArgumentOutOfRangeException(nameof(index));
            var span = FindSpan(index);
            var position = span.IsGap
                ? new SparseRowPosition(true, 0, span.StartAddress, span.EndAddress)
                : new SparseRowPosition(false,
                    span.StartAddress + (ulong)(index - span.StartIndex) * _rowWidth, 0, 0);
            return _createItem(position);
        }
        set => throw new NotSupportedException();
    }

    public bool TryGetRowIndex(uint address, out int rowIndex)
    {
        var rowAddress = (ulong)(address / _rowWidth) * _rowWidth;
        var low = 0;
        var high = _spans.Count - 1;
        while (low <= high)
        {
            var middle = low + ((high - low) / 2);
            var span = _spans[middle];
            if (rowAddress < span.StartAddress)
            {
                high = middle - 1;
            }
            else if (rowAddress > span.EndAddress)
            {
                low = middle + 1;
            }
            else
            {
                rowIndex = span.StartIndex + (span.IsGap ? 0 : checked((int)((rowAddress - span.StartAddress) / _rowWidth)));
                return true;
            }
        }

        rowIndex = -1;
        return false;
    }

    public int IndexOf(T item)
    {
        for (var index = 0; index < Count; index++)
            if (EqualityComparer<T>.Default.Equals(this[index], item)) return index;
        return -1;
    }

    public bool Contains(T item) => IndexOf(item) >= 0;

    public void CopyTo(T[] array, int arrayIndex)
    {
        ArgumentNullException.ThrowIfNull(array);
        if (arrayIndex < 0 || arrayIndex > array.Length) throw new ArgumentOutOfRangeException(nameof(arrayIndex));
        if (array.Length - arrayIndex < Count) throw new ArgumentException("The destination array is too small.", nameof(array));
        for (var index = 0; index < Count; index++) array[arrayIndex + index] = this[index];
    }

    public IEnumerator<T> GetEnumerator()
    {
        for (var index = 0; index < Count; index++) yield return this[index];
    }

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    public void Add(T item) => throw new NotSupportedException();
    public void Clear() => throw new NotSupportedException();
    public void Insert(int index, T item) => throw new NotSupportedException();
    public bool Remove(T item) => throw new NotSupportedException();
    public void RemoveAt(int index) => throw new NotSupportedException();

    private IndexedSpan FindSpan(int index)
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

    private sealed record IndexedSpan(ulong StartAddress, ulong EndAddress, bool IsGap, int StartIndex, int Count);
}
