namespace HexEditor.Formats;

public sealed record SRecordMetadata(
    uint? StartAddress,
    int DataRecordCount,
    ushort? HeaderAddress = null,
    byte[]? HeaderData = null);
