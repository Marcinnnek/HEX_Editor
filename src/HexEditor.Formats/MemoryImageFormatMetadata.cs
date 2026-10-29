namespace HexEditor.Formats;

/// <summary>Typed container for the metadata supported by the two firmware record formats.</summary>
public sealed record MemoryImageFormatMetadata
{
    private MemoryImageFormatMetadata(IntelHexMetadata? intelHex, SRecordMetadata? sRecord)
    {
        IntelHex = intelHex;
        SRecord = sRecord;
    }

    public IntelHexMetadata? IntelHex { get; }
    public SRecordMetadata? SRecord { get; }

    public static MemoryImageFormatMetadata FromIntelHex(IntelHexMetadata metadata) =>
        new(metadata ?? throw new ArgumentNullException(nameof(metadata)), null);

    public static MemoryImageFormatMetadata FromSRecord(SRecordMetadata metadata) =>
        new(null, metadata ?? throw new ArgumentNullException(nameof(metadata)));
}
