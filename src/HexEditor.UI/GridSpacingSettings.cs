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
            File.WriteAllText(temp, Serialize(horizontalByteSpacing, verticalRowSpacing));
            File.Move(temp, FilePath, true);
            ApplySavedSpacing(horizontalByteSpacing, verticalRowSpacing);
        }
        finally { if (File.Exists(temp)) File.Delete(temp); }
    }

    internal static string Serialize(int horizontalByteSpacing, int verticalRowSpacing) =>
        JsonSerializer.Serialize(new StoredSettings(horizontalByteSpacing, verticalRowSpacing));

    internal void ApplySavedSpacing(int horizontalByteSpacing, int verticalRowSpacing)
    {
        _horizontalByteSpacing = horizontalByteSpacing;
        _verticalRowSpacing = verticalRowSpacing;
        SpacingChanged?.Invoke();
    }

    private sealed record StoredSettings(int HorizontalByteSpacing, int VerticalRowSpacing);
}

/// <summary>Calculates the rendered and highlighted bounds of a byte in a HEX row.</summary>
public readonly record struct ByteGridLayout(double CharacterWidth, double LineHeight, int HorizontalSpacing, int VerticalSpacing)
{
    public Size GetRowSize(int count, bool isAscii)
    {
        var height = LineHeight + VerticalSpacing;
        if (count <= 0) return new Size(0, height);
        var byteWidth = CharacterWidth * (isAscii ? 1 : 2);
        var spacing = isAscii ? 0 : HorizontalSpacing;
        return new Size(count * byteWidth + (count - 1) * spacing, height);
    }

    public Rect GetByteBounds(int index, bool isAscii)
    {
        var width = CharacterWidth * (isAscii ? 1 : 2);
        var stride = width + (isAscii ? 0 : HorizontalSpacing);
        return new Rect(index * stride, 0, width, LineHeight);
    }

    public bool TryGetByteIndexAt(Point point, int count, bool isAscii, out int index)
    {
        index = default;
        if (count <= 0 || point.X < 0 || point.Y < 0) return false;
        var byteWidth = CharacterWidth * (isAscii ? 1 : 2);
        var stride = byteWidth + (isAscii ? 0 : HorizontalSpacing);
        if (byteWidth <= 0 || stride <= 0 || point.Y >= LineHeight || point.X >= GetRowSize(count, isAscii).Width) return false;
        var candidate = (int)(point.X / stride);
        if ((uint)candidate >= (uint)count) return false;
        var bounds = GetByteBounds(candidate, isAscii);
        if (!bounds.Contains(point))
        {
            // HEX spacing is a shared hit target: each byte owns the half of
            // the following separator nearest its glyph. ASCII has no gaps.
            if (isAscii || candidate >= count - 1 || point.X <= bounds.Right || point.X >= bounds.Right + HorizontalSpacing)
                return false;
            var midpoint = bounds.Right + HorizontalSpacing / 2d;
            candidate = point.X < midpoint ? candidate : candidate + 1;
        }
        index = candidate;
        return true;
    }

    public Rect GetHighlightBounds(int index, bool isAscii = false) => GetByteBounds(index, isAscii);
}
