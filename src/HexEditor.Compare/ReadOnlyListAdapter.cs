using System.Collections;
using System.Runtime.CompilerServices;

namespace HexEditor.Compare;

/// <summary>Exposes computed rows as an indexed WPF list without materializing the full address space.</summary>
internal abstract class ReadOnlyListAdapter<T> : IList<T>, IList where T : class
{
    private const int CachedRowCount = 128;
    private ConditionalWeakTable<T, RowIndex> _rowIndexes = new();
    private readonly Dictionary<int, T> _cachedRows = new();
    private readonly Queue<int> _cacheOrder = new();

    public abstract int Count { get; }
    public bool IsReadOnly => true;

    public T this[int index]
    {
        get
        {
            if ((uint)index >= (uint)Count) throw new ArgumentOutOfRangeException(nameof(index));
            if (_cachedRows.TryGetValue(index, out var cached)) return cached;

            var row = GetAt(index);
            _rowIndexes.Add(row, new RowIndex(index));
            _cachedRows.Add(index, row);
            _cacheOrder.Enqueue(index);
            if (_cacheOrder.Count > CachedRowCount)
                _cachedRows.Remove(_cacheOrder.Dequeue());
            return row;
        }
        set => throw new NotSupportedException();
    }

    protected abstract T GetAt(int index);

    protected void ClearCachedRows()
    {
        _cachedRows.Clear();
        _cacheOrder.Clear();
        _rowIndexes = new ConditionalWeakTable<T, RowIndex>();
    }

    public int IndexOf(T item)
    {
        if (_rowIndexes.TryGetValue(item, out var found)) return found.Value;
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

    bool IList.IsFixedSize => true;
    bool ICollection.IsSynchronized => false;
    object ICollection.SyncRoot => this;

    object? IList.this[int index]
    {
        get => this[index];
        set => throw new NotSupportedException();
    }

    // WPF asks for the index of rows it previously read. Avoid regenerating every row to answer it.
    int IList.IndexOf(object? value) => value is T row && _rowIndexes.TryGetValue(row, out var found)
        ? found.Value
        : -1;

    bool IList.Contains(object? value) => ((IList)this).IndexOf(value) >= 0;

    void ICollection.CopyTo(Array array, int index)
    {
        ArgumentNullException.ThrowIfNull(array);
        if (array.Rank != 1) throw new ArgumentException("The destination array must be one-dimensional.", nameof(array));
        if (index < 0 || index > array.Length) throw new ArgumentOutOfRangeException(nameof(index));
        if (array.Length - index < Count) throw new ArgumentException("The destination array is too small.", nameof(array));
        for (var row = 0; row < Count; row++) array.SetValue(this[row], index + row);
    }

    int IList.Add(object? value) => throw new NotSupportedException();
    void IList.Clear() => throw new NotSupportedException();
    void IList.Insert(int index, object? value) => throw new NotSupportedException();
    void IList.Remove(object? value) => throw new NotSupportedException();
    void IList.RemoveAt(int index) => throw new NotSupportedException();

    private sealed record RowIndex(int Value);
}
