namespace HexEditor.UI;

internal static class SegmentBlockFormatter
{
    public static string Format(int number, string startAddress, string endAddress, long byteCount) =>
        $"Block {number} Starts at: 0x{Normalize(startAddress)} Ends at: 0x{Normalize(endAddress)} (Length: 0x{byteCount:X} = {byteCount})";

    private static string Normalize(string address) => address.ToUpperInvariant().TrimStart('0').PadLeft(8, '0');
}
