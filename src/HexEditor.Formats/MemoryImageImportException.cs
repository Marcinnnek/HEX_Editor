namespace HexEditor.Formats;

public sealed class MemoryImageImportException : FormatException
{
    public MemoryImageImportException(string reason)
        : base(reason)
    {
        Reason = reason;
    }

    public MemoryImageImportException(int lineNumber, string reason)
        : base($"Line {lineNumber}: {reason}")
    {
        LineNumber = lineNumber;
        Reason = reason;
    }

    public int LineNumber { get; }

    public string Reason { get; }
}
