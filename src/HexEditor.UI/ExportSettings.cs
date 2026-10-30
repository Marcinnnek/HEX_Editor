using System.IO;
using System.Text.Json;

namespace HexEditor.UI;

/// <summary>Stores the last export record size in per-user data.</summary>
public sealed class ExportSettings
{
    public ExportSettings(string? filePath = null)
    {
        FilePath = filePath ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "HexEditor", "export.json");
        MaxDataBytesPerRecord = ReadValue(FilePath);
    }

    public string FilePath { get; }
    public int MaxDataBytesPerRecord { get; private set; }

    public void SetMaxDataBytesPerRecord(int value)
    {
        if (value is not (8 or 16 or 32)) throw new ArgumentOutOfRangeException(nameof(value));
        var directory = Path.GetDirectoryName(FilePath) ?? throw new InvalidOperationException("The settings path has no directory.");
        Directory.CreateDirectory(directory);
        var temporaryPath = FilePath + ".tmp";
        try
        {
            File.WriteAllText(temporaryPath, JsonSerializer.Serialize(new StoredSettings(value)));
            File.Move(temporaryPath, FilePath, true);
            MaxDataBytesPerRecord = value;
        }
        finally
        {
            if (File.Exists(temporaryPath)) File.Delete(temporaryPath);
        }
    }

    private static int ReadValue(string path)
    {
        try
        {
            var value = JsonSerializer.Deserialize<StoredSettings>(File.ReadAllText(path))?.MaxDataBytesPerRecord;
            return value is 8 or 16 or 32 ? value.Value : 16;
        }
        catch (IOException) { return 16; }
        catch (UnauthorizedAccessException) { return 16; }
        catch (JsonException) { return 16; }
    }

    private sealed record StoredSettings(int MaxDataBytesPerRecord);
}
