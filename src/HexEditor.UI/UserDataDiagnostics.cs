using System.IO;
using System.Text;

namespace HexEditor.UI;

/// <summary>Writes bounded, error-only diagnostics to per-user local application data.</summary>
public sealed class UserDataDiagnostics
{
    public const long MaximumFileBytes = 1024 * 1024;
    public const int MaximumFiles = 5;
    private readonly string _directory;
    private readonly object _gate = new();

    public UserDataDiagnostics() : this(Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "HEX Editor", "Diagnostics"))
    {
    }

    public UserDataDiagnostics(string directory) => _directory = directory;

    public string DirectoryPath => _directory;

    public void RecordError(string operation, Exception exception, uint? selectionStart = null, uint? selectionEnd = null)
    {
        ArgumentNullException.ThrowIfNull(exception);
        try
        {
            var safeOperation = new string(operation.Where(c => char.IsLetterOrDigit(c) || c is ' ' or '-' or '_').Take(64).ToArray());
            if (string.IsNullOrWhiteSpace(safeOperation)) safeOperation = "Unknown";
            var context = selectionStart is { } start
                ? $" selection=0x{start:X8}-0x{(selectionEnd ?? start):X8}"
                : string.Empty;
            var entry = $"{DateTimeOffset.UtcNow:O} operation={safeOperation}{context} error={exception.GetType().Name}{Environment.NewLine}";
            var bytes = Encoding.UTF8.GetBytes(entry);
            lock (_gate)
            {
                Directory.CreateDirectory(_directory);
                var current = Path.Combine(_directory, "diagnostics.log");
                if (File.Exists(current) && new FileInfo(current).Length + bytes.Length > MaximumFileBytes)
                    Rotate(current);
                using var stream = new FileStream(current, FileMode.Append, FileAccess.Write, FileShare.Read);
                stream.Write(bytes);
            }
        }
        catch
        {
            // Diagnostics are optional and must never interrupt an application workflow.
        }
    }

    private void Rotate(string current)
    {
        var oldest = Path.Combine(_directory, "diagnostics.4.log");
        if (File.Exists(oldest)) File.Delete(oldest);
        for (var index = MaximumFiles - 2; index >= 1; index--)
        {
            var source = Path.Combine(_directory, $"diagnostics.{index}.log");
            if (File.Exists(source)) File.Move(source, Path.Combine(_directory, $"diagnostics.{index + 1}.log"));
        }
        File.Move(current, Path.Combine(_directory, "diagnostics.1.log"));
    }
}
