using System.IO;
using System.Text.Json;

namespace HexEditor.UI;

/// <summary>Persists the default row width used by newly opened Editor and Compare views.</summary>
public sealed class RowWidthSettings
{
    private int _defaultBytesPerRow = 16;

    public RowWidthSettings(string? filePath = null)
    {
        FilePath = filePath ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "HexEditor", "row-width.json");
        try
        {
            var stored = JsonSerializer.Deserialize<StoredSettings>(File.ReadAllText(FilePath));
            if (stored?.DefaultBytesPerRow is >= 1 and <= 128) _defaultBytesPerRow = stored.DefaultBytesPerRow;
        }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
        catch (JsonException) { }
    }

    public static RowWidthSettings Current { get; } = new();
    public string FilePath { get; }
    public int DefaultBytesPerRow => _defaultBytesPerRow;

    internal static string Serialize(int bytesPerRow)
    {
        Validate(bytesPerRow);
        return JsonSerializer.Serialize(new StoredSettings(bytesPerRow));
    }

    internal void ApplySavedWidth(int bytesPerRow)
    {
        Validate(bytesPerRow);
        _defaultBytesPerRow = bytesPerRow;
    }

    internal static void Validate(int bytesPerRow)
    {
        if (bytesPerRow is < 1 or > 128)
            throw new ArgumentOutOfRangeException(nameof(bytesPerRow), "Default row width must be between 1 and 128 bytes.");
    }

    private sealed record StoredSettings(int DefaultBytesPerRow);
}
