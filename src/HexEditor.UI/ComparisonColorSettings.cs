using System.IO;
using System.Text.Json;

namespace HexEditor.UI;

public enum ComparisonColorKind { Different, OnlyInA, OnlyInB }

public sealed record ComparisonColorPalette(string Different, string OnlyInA, string OnlyInB)
{
    public static ComparisonColorPalette LightDefaults { get; } = new("#55D03838", "#55D08514", "#55707070");
    public static ComparisonColorPalette DarkDefaults { get; } = new("#885E3138", "#88634A25", "#88535C67");

    public string Get(ComparisonColorKind kind) => kind switch
    {
        ComparisonColorKind.Different => Different,
        ComparisonColorKind.OnlyInA => OnlyInA,
        ComparisonColorKind.OnlyInB => OnlyInB,
        _ => throw new ArgumentOutOfRangeException(nameof(kind))
    };

    public ComparisonColorPalette With(ComparisonColorKind kind, string color) => kind switch
    {
        ComparisonColorKind.Different => this with { Different = color },
        ComparisonColorKind.OnlyInA => this with { OnlyInA = color },
        ComparisonColorKind.OnlyInB => this with { OnlyInB = color },
        _ => throw new ArgumentOutOfRangeException(nameof(kind))
    };
}

/// <summary>Persists separate comparison highlight palettes for Light and Dark appearances.</summary>
public sealed class ComparisonColorSettings
{
    private static readonly Lazy<ComparisonColorSettings> Shared = new(() => new());
    private ComparisonColorPalette _light;
    private ComparisonColorPalette _dark;

    public static ComparisonColorSettings Current => Shared.Value;

    public ComparisonColorSettings(string? filePath = null)
    {
        FilePath = filePath ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "HexEditor", "comparison-colors.json");
        (_light, _dark) = Read(FilePath);
    }

    public string FilePath { get; }
    public ComparisonColorPalette GetPalette(AppearanceMode mode) => mode switch
    {
        AppearanceMode.Light => _light,
        AppearanceMode.Dark => _dark,
        _ => throw new ArgumentOutOfRangeException(nameof(mode), "Choose Light or Dark palette.")
    };
    public string GetColor(AppearanceMode mode, ComparisonColorKind kind) => GetPalette(mode).Get(kind);

    internal void ApplySavedPalettes(ComparisonColorPalette light, ComparisonColorPalette dark)
    {
        Validate(light);
        Validate(dark);
        _light = light;
        _dark = dark;
    }

    internal string Serialize(ComparisonColorPalette light, ComparisonColorPalette dark)
    {
        Validate(light);
        Validate(dark);
        return JsonSerializer.Serialize(new StoredSettings(light, dark));
    }

    internal static void Validate(ComparisonColorPalette palette)
    {
        ArgumentNullException.ThrowIfNull(palette);
        foreach (var color in new[] { palette.Different, palette.OnlyInA, palette.OnlyInB })
        {
            if (color is null || color.Length != 9 || color[0] != '#' || !color.AsSpan(1).ToString().All(Uri.IsHexDigit))
                throw new ArgumentException("Comparison colors must use #AARRGGBB format.", nameof(palette));
        }
    }

    private static (ComparisonColorPalette Light, ComparisonColorPalette Dark) Read(string path)
    {
        try
        {
            var stored = JsonSerializer.Deserialize<StoredSettings>(File.ReadAllText(path));
            if (stored?.Light is not null && stored.Dark is not null)
            {
                Validate(stored.Light);
                Validate(stored.Dark);
                return (stored.Light, stored.Dark);
            }
        }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
        catch (JsonException) { }
        catch (ArgumentException) { }
        return (ComparisonColorPalette.LightDefaults, ComparisonColorPalette.DarkDefaults);
    }

    private sealed record StoredSettings(ComparisonColorPalette Light, ComparisonColorPalette Dark);
}
