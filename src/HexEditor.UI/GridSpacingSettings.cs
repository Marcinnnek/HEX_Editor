using System.IO;
using System.Text.Json;
using System.Windows;

namespace HexEditor.UI;

/// <summary>Stores and publishes the user's HEX grid spacing preferences.</summary>
public sealed class GridSpacingSettings
{
    private static readonly Lazy<GridSpacingSettings> Shared = new(() => new());
    public static GridSpacingSettings Current => Shared.Value;
    private int _horizontalByteSpacing = 2;
    private int _verticalRowSpacing = 1;

    public GridSpacingSettings(string? filePath = null)
    {
        FilePath = filePath ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "HexEditor", "grid-spacing.json");
        try
        {
            var stored = JsonSerializer.Deserialize<StoredSettings>(File.ReadAllText(FilePath));
            if (stored is not null && stored.HorizontalByteSpacing is >= 0 and <= 12 && stored.VerticalRowSpacing is >= 0 and <= 8)
            {
                _horizontalByteSpacing = stored.HorizontalByteSpacing;
                _verticalRowSpacing = stored.VerticalRowSpacing;
            }
        }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
        catch (JsonException) { }
    }

    public static event Action? SpacingChanged;
    public string FilePath { get; }
    public int HorizontalByteSpacing => _horizontalByteSpacing;
    public int VerticalRowSpacing => _verticalRowSpacing;

    public void SetSpacing(int horizontalByteSpacing, int verticalRowSpacing)
    {
        if (horizontalByteSpacing is < 0 or > 12) throw new ArgumentOutOfRangeException(nameof(horizontalByteSpacing));
        if (verticalRowSpacing is < 0 or > 8) throw new ArgumentOutOfRangeException(nameof(verticalRowSpacing));
        var folder = Path.GetDirectoryName(FilePath) ?? throw new InvalidOperationException("The settings path has no directory.");
        Directory.CreateDirectory(folder);
        var temp = FilePath + ".tmp";
        try
        {
            File.WriteAllText(temp, JsonSerializer.Serialize(new StoredSettings(horizontalByteSpacing, verticalRowSpacing)));
            File.Move(temp, FilePath, true);
            _horizontalByteSpacing = horizontalByteSpacing;
            _verticalRowSpacing = verticalRowSpacing;
            SpacingChanged?.Invoke();
        }
        finally { if (File.Exists(temp)) File.Delete(temp); }
    }

    private sealed record StoredSettings(int HorizontalByteSpacing, int VerticalRowSpacing);
}

/// <summary>Calculates the rendered and highlighted bounds of a byte in a HEX row.</summary>
public readonly record struct ByteGridLayout(double CharacterWidth, double LineHeight, int HorizontalSpacing, int VerticalSpacing)
{
    public Rect GetByteBounds(int index, bool isAscii)
    {
        var width = CharacterWidth * (isAscii ? 1 : 2);
        var stride = width + (isAscii ? 0 : HorizontalSpacing);
        return new Rect(index * stride, 0, width, LineHeight + VerticalSpacing);
    }

    public Rect GetHighlightBounds(int index) => new(index * (CharacterWidth * 2 + HorizontalSpacing), 0, CharacterWidth * 2, LineHeight);
}
