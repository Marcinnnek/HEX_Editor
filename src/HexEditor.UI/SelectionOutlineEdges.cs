namespace HexEditor.UI;

internal readonly record struct SelectionOutlineEdges(bool Top, bool Right, bool Bottom, bool Left)
{
    public static SelectionOutlineEdges ForCell(HexViewRow row, HexViewRow? above, HexViewRow? below,
        int index, int bytesPerRow, uint start, uint end)
    {
        if (!Selected(row, index, start, end)) return default;
        var address = row.Cells[index].Address!.Value;
        var left = index > 0 && Selected(row, index - 1, start, end) &&
                   (ulong)row.Cells[index - 1].Address!.Value + 1 == address;
        var right = index + 1 < row.Cells.Count && Selected(row, index + 1, start, end) &&
                    (ulong)address + 1 == row.Cells[index + 1].Address!.Value;
        var top = index < (above?.Cells.Count ?? 0) && Selected(above!, index, start, end) &&
                  (ulong)above!.Cells[index].Address!.Value + (uint)bytesPerRow == address;
        var bottom = index < (below?.Cells.Count ?? 0) && Selected(below!, index, start, end) &&
                     (ulong)address + (uint)bytesPerRow == below!.Cells[index].Address!.Value;
        return new SelectionOutlineEdges(!top, !right, !bottom, !left);
    }

    private static bool Selected(HexViewRow row, int index, uint start, uint end) =>
        !row.IsGapMarker && index < row.Cells.Count && row.Cells[index].Hex.Length > 0 &&
        row.Cells[index].Address is uint address && address >= start && address <= end;
}
