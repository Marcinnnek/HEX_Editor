namespace HexEditor.UI;

internal static class SegmentByteCountFormatter
{
    public static string Format(int byteCount) => $"0x{byteCount:X} = {byteCount}";
}
