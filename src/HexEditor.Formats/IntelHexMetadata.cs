namespace HexEditor.Formats;

public sealed record IntelHexMetadata(
    uint? StartLinearAddress,
    ushort? StartSegmentCodeSegment,
    ushort? StartSegmentInstructionPointer);
