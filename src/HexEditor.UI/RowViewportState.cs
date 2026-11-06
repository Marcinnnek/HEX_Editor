using HexEditor.Compare;

namespace HexEditor.UI;

/// <summary>Chooses the visible row window independently of WPF layout and scroll controls.</summary>
internal sealed class RowViewportState
{
    private IList<HexViewRow>? _rows;
    private int _knownRowCount;

    public int FirstRowIndex { get; private set; }
    public int VisibleRowCount { get; private set; } = 24;
    public int MaximumStart { get; private set; }
    public int EndExclusive => Math.Min(_knownRowCount, FirstRowIndex + VisibleRowCount);

    public void ReplaceRows(IList<HexViewRow>? rows)
    {
        var nextStart = 0;
        if (_rows is SparseRowList<HexViewRow> oldRows &&
            rows is SparseRowList<HexViewRow> newRows &&
            oldRows.SourceIdentity is not null &&
            ReferenceEquals(oldRows.SourceIdentity, newRows.SourceIdentity))
        {
            if (IsAtEnd)
            {
                nextStart = Math.Max(0, newRows.Count - VisibleRowCount);
            }
            else if ((uint)FirstRowIndex < (uint)oldRows.Count)
            {
                var oldRow = oldRows[FirstRowIndex];
                if (!oldRow.IsGapMarker && HexAddressParser.TryParse(oldRow.Address, out var address) &&
                    newRows.TryGetRowIndex(address, out var rowIndex))
                    nextStart = rowIndex;
                else
                    nextStart = (int)((long)FirstRowIndex * newRows.Count / oldRows.Count);
            }
        }

        _rows = rows;
        _knownRowCount = rows?.Count ?? 0;
        MaximumStart = Math.Max(0, _knownRowCount - VisibleRowCount);
        FirstRowIndex = Math.Clamp(nextStart, 0, MaximumStart);
    }

    public void SetVisibleRowCount(int visibleRowCount)
    {
        if (visibleRowCount < 1) throw new ArgumentOutOfRangeException(nameof(visibleRowCount));
        var wasAtEnd = IsAtEnd;
        VisibleRowCount = visibleRowCount;
        _knownRowCount = _rows?.Count ?? 0;
        MaximumStart = Math.Max(0, _knownRowCount - VisibleRowCount);
        FirstRowIndex = wasAtEnd ? MaximumStart : Math.Clamp(FirstRowIndex, 0, MaximumStart);
    }

    public void MoveTo(int rowIndex)
    {
        _knownRowCount = _rows?.Count ?? 0;
        MaximumStart = Math.Max(0, _knownRowCount - VisibleRowCount);
        FirstRowIndex = Math.Clamp(rowIndex, 0, MaximumStart);
    }

    public void ResetToStart() => FirstRowIndex = 0;

    private bool IsAtEnd => MaximumStart > 0 && FirstRowIndex >= MaximumStart;
}
