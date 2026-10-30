using System.IO;
using System.Text.Json;

namespace HexEditor.UI;

public enum AppearanceMode
{
    Automatic,
    Light,
    Dark
}

/// <summary>Stores the user's appearance choice independently of the executable location.</summary>
public sealed class AppearanceSettings
{
    public AppearanceSettings(string? filePath = null)
    {
        FilePath = filePath ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "HexEditor", "appearance.json");
        Mode = ReadMode(FilePath);
    }

    public string FilePath { get; }
    public AppearanceMode Mode { get; private set; }

    public AppearanceMode GetEffectiveTheme(bool windowsIsDark) => Mode == AppearanceMode.Automatic
        ? windowsIsDark ? AppearanceMode.Dark : AppearanceMode.Light
        : Mode;

    public void SetMode(AppearanceMode mode)
    {
        if (!Enum.IsDefined(mode)) throw new ArgumentOutOfRangeException(nameof(mode));
        var directory = Path.GetDirectoryName(FilePath) ?? throw new InvalidOperationException("The settings path has no directory.");
        Directory.CreateDirectory(directory);
        var temporaryPath = FilePath + ".tmp";
        try
        {
            File.WriteAllText(temporaryPath, Serialize(mode));
            File.Move(temporaryPath, FilePath, true);
            ApplySavedMode(mode);
        }
        finally
        {
            if (File.Exists(temporaryPath)) File.Delete(temporaryPath);
        }
    }

    internal static string Serialize(AppearanceMode mode) => JsonSerializer.Serialize(new StoredSettings(mode));

    internal void ApplySavedMode(AppearanceMode mode) => Mode = mode;

    private static AppearanceMode ReadMode(string path)
    {
        try
        {
            var mode = JsonSerializer.Deserialize<StoredSettings>(File.ReadAllText(path))?.Mode;
            return mode is AppearanceMode.Automatic or AppearanceMode.Light or AppearanceMode.Dark
                ? mode.Value : AppearanceMode.Automatic;
        }
        catch (IOException) { return AppearanceMode.Automatic; }
        catch (UnauthorizedAccessException) { return AppearanceMode.Automatic; }
        catch (JsonException) { return AppearanceMode.Automatic; }
    }

    private sealed record StoredSettings(AppearanceMode Mode);
}
