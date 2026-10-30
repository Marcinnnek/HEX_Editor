using System.ComponentModel;
using System.Collections.Specialized;
using System.Runtime.CompilerServices;
using HexEditor.Core;
using HexEditor.Compare;

namespace HexEditor.UI;

/// <summary>Adapts the comparison domain model to bindings used by the WPF controls.</summary>
public sealed class CompareViewModel : INotifyPropertyChanged
{
    private readonly MemoryComparisonModel _comparison;
    private readonly ComparePanelRows _fileARows;
    private readonly ComparePanelRows _fileBRows;

    public CompareViewModel(MemoryImage fileA, MemoryImage fileB, int bytesPerRow = 16, bool showAscii = true,
        CancellationToken cancellationToken = default, IProgress<ComparisonProgress>? progress = null)
    {
        _comparison = new MemoryComparisonModel(fileA, fileB, bytesPerRow, showAscii, cancellationToken, progress);
        _fileARows = new ComparePanelRows(_comparison, fileA: true);
        _fileBRows = new ComparePanelRows(_comparison, fileA: false);
        _comparison.RowsChanged += OnRowsChanged;
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    public IList<CompareViewRow> Rows => _comparison.Rows;
    public IList<HexViewRow> FileARows => _fileARows;
    public IList<HexViewRow> FileBRows => _fileBRows;
    public MemoryImage FileA => _comparison.FileA;
    public MemoryImage FileB => _comparison.FileB;
    public int BytesPerRow { get => _comparison.BytesPerRow; set => SetComparisonProperty(() => _comparison.BytesPerRow = value, nameof(BytesPerRow)); }
    public bool ShowAscii { get => _comparison.ShowAscii; set => SetComparisonProperty(() => _comparison.ShowAscii = value, nameof(ShowAscii)); }
    public CompareViewMode ViewMode { get => _comparison.ViewMode; set => SetComparisonProperty(() => _comparison.ViewMode = value, nameof(ViewMode)); }
    public string ComparisonStatus => _comparison.ComparisonStatus;

    public void SetAddressRange(uint start, uint end) => _comparison.SetAddressRange(start, end);
    public void Refresh(MemoryImage fileA, MemoryImage fileB)
    {
        _comparison.Refresh(fileA, fileB);
        Raise(nameof(FileA), nameof(FileB), nameof(ComparisonStatus));
    }

    public bool TryNavigateToAddress(uint address, out int rowIndex) => _comparison.TryNavigateToAddress(address, out rowIndex);
    public bool TryFindNextDifference(uint address, out uint difference) => _comparison.TryFindNextDifference(address, out difference);
    public bool TryFindPreviousDifference(uint address, out uint difference) => _comparison.TryFindPreviousDifference(address, out difference);
    public bool TryFindNextDifferenceFromSelection(uint? selectedAddress, out uint difference) => _comparison.TryFindNextDifferenceFromSelection(selectedAddress, out difference);
    public bool TryFindPreviousDifferenceFromSelection(uint? selectedAddress, out uint difference) => _comparison.TryFindPreviousDifferenceFromSelection(selectedAddress, out difference);
    public bool TryFindNextDifferenceFromAddress(uint? selectedAddress, uint? anchorAddress, out uint difference) => _comparison.TryFindNextDifferenceFromAddress(selectedAddress, anchorAddress, out difference);
    public bool TryFindPreviousDifferenceFromAddress(uint? selectedAddress, uint? anchorAddress, out uint difference) => _comparison.TryFindPreviousDifferenceFromAddress(selectedAddress, anchorAddress, out difference);

    private void SetComparisonProperty(Action update, string propertyName)
    {
        var priorRowCount = Rows.Count;
        update();
        Raise(propertyName);
        if (Rows.Count == priorRowCount) Raise(nameof(Rows), nameof(FileARows), nameof(FileBRows));
    }

    private void OnRowsChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e) =>
        Raise(nameof(Rows), nameof(FileARows), nameof(FileBRows), nameof(ComparisonStatus));

    private void Raise(params string[] names)
    {
        foreach (var name in names) PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}

internal sealed class ComparePanelRows : ReadOnlyListAdapter<HexViewRow>, INotifyCollectionChanged
{
    private readonly MemoryComparisonModel _comparison;
    private readonly bool _fileA;

    public ComparePanelRows(MemoryComparisonModel comparison, bool fileA)
    {
        _comparison = comparison;
        _fileA = fileA;
        _comparison.RowsChanged += (_, _) =>
        {
            ClearCachedRows();
            CollectionChanged?.Invoke(this, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
        };
    }

    public event NotifyCollectionChangedEventHandler? CollectionChanged;
    public override int Count => _comparison.Rows.Count;

    protected override HexViewRow GetAt(int index)
    {
        var row = _comparison.Rows[index];
        if (row.IsGapMarker) return new HexViewRow(row.Address, string.Empty, null, true, row.GapDescription);

        var panel = _fileA ? row.FileA : row.FileB;
        var cells = panel.Cells.Select(cell => new HexByteCell(
            cell.Hex,
            cell.Ascii,
            cell.Address,
            cell.State switch
            {
                CompareByteState.Different => ByteHighlightState.Different,
                CompareByteState.OnlyA when _fileA => ByteHighlightState.OnlyInPanel,
                CompareByteState.OnlyB when !_fileA => ByteHighlightState.OnlyInPanel,
                CompareByteState.OnlyA or CompareByteState.OnlyB => ByteHighlightState.OnlyOther,
                _ => ByteHighlightState.None
            })).ToArray();
        return new HexViewRow(row.Address, panel.HexData, panel.Ascii, false, null) { Cells = cells };
    }
}
