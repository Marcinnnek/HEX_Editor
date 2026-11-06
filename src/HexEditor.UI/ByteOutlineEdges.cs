namespace HexEditor.UI;

internal readonly record struct ByteOutlineEdges(bool Top, bool Right, bool Bottom, bool Left)
{
    public static ByteOutlineEdges ForSelection(HexViewRow row, HexViewRow? above, HexViewRow? below,
        int index, int bytesPerRow, uint start, uint end)
        => ForCell(row, above, below, index, bytesPerRow, start, end, edited: false);

    public static ByteOutlineEdges ForEdited(HexViewRow row, HexViewRow? above, HexViewRow? below,
        int index, int bytesPerRow)
        => ForCell(row, above, below, index, bytesPerRow, 0, 0, edited: true);

    private static ByteOutlineEdges ForCell(HexViewRow row, HexViewRow? above, HexViewRow? below,
        int index, int bytesPerRow, uint start, uint end, bool edited)
    {
        if (!Included(row, index, start, end, edited)) return default;
        var address = row.Cells[index].Address!.Value;
        var left = index > 0 && Included(row, index - 1, start, end, edited) &&
                   (ulong)row.Cells[index - 1].Address!.Value + 1 == address;
        var right = index + 1 < row.Cells.Count && Included(row, index + 1, start, end, edited) &&
                    (ulong)address + 1 == row.Cells[index + 1].Address!.Value;
        var top = index < (above?.Cells.Count ?? 0) && Included(above!, index, start, end, edited) &&
                  (ulong)above!.Cells[index].Address!.Value + (uint)bytesPerRow == address;
        var bottom = index < (below?.Cells.Count ?? 0) && Included(below!, index, start, end, edited) &&
                     (ulong)address + (uint)bytesPerRow == below!.Cells[index].Address!.Value;
        return new ByteOutlineEdges(!top, !right, !bottom, !left);
    }

    private static bool Included(HexViewRow row, int index, uint start, uint end, bool edited) =>
        !row.IsGapMarker && index < row.Cells.Count && row.Cells[index].Hex.Length > 0 &&
        row.Cells[index].Address is uint address &&
        (edited ? row.Cells[index].IsEdited : address >= start && address <= end);
}
