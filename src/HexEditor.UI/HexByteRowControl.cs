using System.Globalization;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Markup;
using System.Windows.Controls;
using System.Windows.Media;

namespace HexEditor.UI;

/// <summary>Draws a row of byte text and highlights without a visual element per byte.</summary>
public sealed class HexByteRowControl : Control
{
    private const int MaxCachedRows = 256;
    private static readonly Dictionary<RowDrawingKey, DrawingGroup> RowDrawingCache = [];
    private static readonly Queue<RowDrawingKey> RowDrawingOrder = new();
    private double _characterWidth;
    private double _lineHeight;
    private double _pixelsPerDip;

    public HexByteRowControl()
    {
        Focusable = false;
        Loaded += Control_Loaded;
        Unloaded += Control_Unloaded;
    }

    public static readonly DependencyProperty RowProperty = DependencyProperty.Register(
        nameof(Row), typeof(HexViewRow), typeof(HexByteRowControl),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender, OnRowChanged));

    public static readonly DependencyProperty IsAsciiProperty = DependencyProperty.Register(
        nameof(IsAscii), typeof(bool), typeof(HexByteRowControl),
        new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty RowCapacityProperty = DependencyProperty.Register(
        nameof(RowCapacity), typeof(int), typeof(HexByteRowControl),
        new FrameworkPropertyMetadata(0, FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsRender));

    public HexViewRow? Row
    {
        get => (HexViewRow?)GetValue(RowProperty);
        set => SetValue(RowProperty, value);
    }

    public bool IsAscii
    {
        get => (bool)GetValue(IsAsciiProperty);
        set => SetValue(IsAsciiProperty, value);
    }

    public int RowCapacity
    {
        get => (int)GetValue(RowCapacityProperty);
        set => SetValue(RowCapacityProperty, value);
    }

    public bool TryGetAddressAt(Point point, out uint address)
    {
        address = default;
        var row = Row;
        if (row is null || row.IsGapMarker || point.X < 0 || point.Y < 0) return false;
        EnsureMetrics();
        var layout = CreateLayout();
        if (!layout.TryGetByteIndexAt(point, row.Cells.Count, IsAscii, out var index) || row.Cells[index].Address is not uint selected) return false;
        address = selected;
        return true;
    }

    protected override Size MeasureOverride(Size constraint)
    {
        EnsureMetrics();
        var count = IsAscii ? Math.Max(Row?.Cells.Count ?? 0, RowCapacity) : Row?.Cells.Count ?? 0;
        var size = CreateLayout().GetRowSize(count, IsAscii);
        return size;
    }

    protected override void OnRender(DrawingContext drawingContext)
    {
        base.OnRender(drawingContext);
        var row = Row;
        EnsureMetrics();
        var layout = CreateLayout();
        var count = IsAscii ? Math.Max(row?.Cells.Count ?? 0, RowCapacity) : row?.Cells.Count ?? 0;
        var dataBounds = new Rect(0, 0, layout.GetRowSize(count, IsAscii).Width, _lineHeight);
        drawingContext.DrawRectangle(Brushes.Transparent, null, dataBounds);
        if (IsAscii && TryFindResource("HexAsciiBackgroundBrush") is Brush asciiBackground)
            drawingContext.DrawRectangle(asciiBackground, null, dataBounds);
        if (row is null || row.IsGapMarker) return;
        for (var index = 0; index < row.Cells.Count; index++)
        {
            var brush = row.Cells[index].Highlight switch
            {
                ByteHighlightState.Different => TryFindResource("HexDifferentBrush") as Brush,
                ByteHighlightState.OnlyInA => TryFindResource("HexOnlyInABrush") as Brush,
                ByteHighlightState.OnlyInB => TryFindResource("HexOnlyInBBrush") as Brush,
                _ => null
            };
            if (brush is not null)
                drawingContext.DrawRectangle(brush, null, layout.GetHighlightBounds(index, IsAscii));
            if (row.Cells[index].IsEdited && TryFindResource("HexEditedByteBrush") is Brush editedBrush)
                drawingContext.DrawRectangle(null, new Pen(editedBrush, 2), layout.GetHighlightBounds(index, IsAscii));
            if (row.Cells[index].IsFindMatch && TryFindResource("HexFindMatchBrush") is Brush findBrush)
            {
                // Keep the Find marker visually distinct from the edited-byte outline,
                // even when a user-selected palette uses a similar color for both.
                var bounds = layout.GetHighlightBounds(index, IsAscii);
                bounds.Inflate(-2, -2);
                if (bounds.Width > 0 && bounds.Height > 0)
                {
                    var findPen = new Pen(findBrush, 2) { DashStyle = DashStyles.Dash };
                    drawingContext.DrawRectangle(null, findPen, bounds);
                }
            }
        }

        drawingContext.DrawDrawing(GetRowDrawing(row, layout));
    }

    protected override void OnPropertyChanged(DependencyPropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);
        if (e.Property == ForegroundProperty) InvalidateVisual();
        if (e.Property == FontFamilyProperty || e.Property == FontSizeProperty || e.Property == FontStyleProperty ||
            e.Property == FontWeightProperty || e.Property == FontStretchProperty)
        {
            _characterWidth = 0;
            InvalidateMeasure();
            InvalidateVisual();
        }
    }

    private void EnsureMetrics()
    {
        var pixelsPerDip = VisualTreeHelper.GetDpi(this).PixelsPerDip;
        if (_characterWidth > 0 && _pixelsPerDip == pixelsPerDip) return;
        _pixelsPerDip = pixelsPerDip;
        var sample = Format("0");
        _characterWidth = sample.WidthIncludingTrailingWhitespace;
        _lineHeight = sample.Height;
    }

    private ByteGridLayout CreateLayout() => new(
        _characterWidth,
        _lineHeight,
        GridSpacingSettings.Current.HorizontalByteSpacing,
        GridSpacingSettings.Current.VerticalRowSpacing);

    private void Control_Loaded(object sender, RoutedEventArgs e)
    {
        GridSpacingSettings.SpacingChanged += SpacingChanged;
        AppearancePalette.Changed += PaletteChanged;
    }

    private void Control_Unloaded(object sender, RoutedEventArgs e)
    {
        GridSpacingSettings.SpacingChanged -= SpacingChanged;
        AppearancePalette.Changed -= PaletteChanged;
    }

    private void PaletteChanged() => InvalidateVisual();

    private void SpacingChanged() => Dispatcher.BeginInvoke(() =>
    {
        InvalidateMeasure();
        InvalidateVisual();
    });

    private static void OnRowChanged(DependencyObject owner, DependencyPropertyChangedEventArgs e)
    {
        var oldRow = e.OldValue as HexViewRow;
        var newRow = e.NewValue as HexViewRow;
        if (oldRow?.Cells.Count != newRow?.Cells.Count || oldRow?.IsGapMarker != newRow?.IsGapMarker)
            ((HexByteRowControl)owner).InvalidateMeasure();
    }

    private FormattedText Format(string text) => new(
        text, CultureInfo.InvariantCulture, FlowDirection.LeftToRight,
        new Typeface(FontFamily, FontStyle, FontWeight, FontStretch), FontSize, Foreground, _pixelsPerDip);

    private DrawingGroup GetRowDrawing(HexViewRow row, ByteGridLayout layout)
    {
        var text = IsAscii ? row.Ascii ?? string.Empty : row.HexData;
        var color = Foreground is SolidColorBrush solid ? solid.Color : Colors.Black;
        var typeface = new Typeface(FontFamily, FontStyle, FontWeight, FontStretch).ToString() ?? string.Empty;
        var key = new RowDrawingKey(text, IsAscii, FontSize, typeface, color, _pixelsPerDip,
            layout.CharacterWidth, layout.LineHeight, layout.HorizontalSpacing);
        if (RowDrawingCache.TryGetValue(key, out var cached)) return cached;

        var group = new DrawingGroup();
        using (var context = group.Open())
        {
            var typefaceInfo = new Typeface(FontFamily, FontStyle, FontWeight, FontStretch);
            if (typefaceInfo.TryGetGlyphTypeface(out var glyphTypeface))
            {
                var characters = new List<char>(row.Cells.Count * (IsAscii ? 1 : 2));
                var glyphs = new List<ushort>(characters.Capacity);
                var advances = new List<double>(characters.Capacity);
                for (var index = 0; index < row.Cells.Count; index++)
                {
                    var cellText = IsAscii ? row.Cells[index].Ascii : row.Cells[index].Hex;
                    for (var charIndex = 0; charIndex < cellText.Length; charIndex++)
                    {
                        var character = cellText[charIndex];
                        characters.Add(character);
                        glyphs.Add(glyphTypeface.CharacterToGlyphMap.TryGetValue(character, out var glyph)
                            ? glyph
                            : glyphTypeface.CharacterToGlyphMap['?']);
                        var isLastCellCharacter = index == row.Cells.Count - 1 && charIndex == cellText.Length - 1;
                        var isByteSeparator = !IsAscii && charIndex == cellText.Length - 1 && !isLastCellCharacter;
                        advances.Add(_characterWidth + (isByteSeparator ? layout.HorizontalSpacing : 0));
                    }
                }
                if (glyphs.Count > 0)
                {
                    var run = new GlyphRun(glyphTypeface, 0, false, FontSize, (float)_pixelsPerDip, glyphs,
                        new Point(IsAscii ? 6 : 0, Format("0").Baseline), advances, null, characters, null, null, null,
                        XmlLanguage.GetLanguage("en-US"));
                    context.DrawGlyphRun(Foreground, run);
                }
            }
            else
                for (var index = 0; index < row.Cells.Count; index++)
                    context.DrawText(Format(IsAscii ? row.Cells[index].Ascii : row.Cells[index].Hex), layout.GetByteBounds(index, IsAscii).TopLeft);
        }
        group.Freeze();
        if (RowDrawingCache.Count >= MaxCachedRows)
        {
            var expired = RowDrawingOrder.Dequeue();
            RowDrawingCache.Remove(expired);
        }
        RowDrawingCache.Add(key, group);
        RowDrawingOrder.Enqueue(key);
        return group;
    }

    private readonly record struct RowDrawingKey(string Text, bool IsAscii, double FontSize, string Typeface,
        Color Foreground, double PixelsPerDip, double CharacterWidth, double LineHeight, int HorizontalSpacing);

}
