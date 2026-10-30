namespace HexEditor.UI;

internal static class SegmentByteCountFormatter
{
    public static string Format(long byteCount) => $"0x{byteCount:X} = {byteCount}";
}
