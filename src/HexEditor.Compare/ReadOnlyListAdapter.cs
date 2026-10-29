using System.Collections;

namespace HexEditor.Compare;

/// <summary>Provides the common read-only IList plumbing for computed UI row collections.</summary>
internal abstract class ReadOnlyListAdapter<T> : IList<T>
{
    public abstract int Count { get; }
    public bool IsReadOnly => true;

    public T this[int index]
    {
        get => GetAt(index);
        set => throw new NotSupportedException();
    }

    protected abstract T GetAt(int index);

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
}
