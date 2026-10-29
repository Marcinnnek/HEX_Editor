namespace HexEditor.Formats;

public sealed record MemoryImageFileProgress(string Stage, long Completed, long? Total = null);
