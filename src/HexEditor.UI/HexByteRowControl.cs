using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace HexEditor.UI;

/// <summary>Draws a row of byte text and highlights without a visual element per byte.</summary>
public sealed class HexByteRowControl : Control
{
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
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty IsAsciiProperty = DependencyProperty.Register(
        nameof(IsAscii), typeof(bool), typeof(HexByteRowControl),
        new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsRender));

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
        var count = Row?.Cells.Count ?? 0;
        var size = CreateLayout().GetRowSize(count, IsAscii);
        return new Size(size.Width, _lineHeight);
    }

    protected override void OnRender(DrawingContext drawingContext)
    {
        base.OnRender(drawingContext);
        var row = Row;
        if (row is null || row.IsGapMarker) return;
        EnsureMetrics();

        var layout = CreateLayout();
        var dataBounds = new Rect(0, 0, layout.GetRowSize(row.Cells.Count, IsAscii).Width, _lineHeight);
        drawingContext.DrawRectangle(Brushes.Transparent, null, dataBounds);
        if (IsAscii && TryFindResource("HexAsciiBackgroundBrush") is Brush asciiBackground)
            drawingContext.DrawRectangle(asciiBackground, null, dataBounds);
        for (var index = 0; index < row.Cells.Count; index++)
        {
            var brush = row.Cells[index].Highlight switch
            {
                ByteHighlightState.Different => TryFindResource("HexDifferentBrush") as Brush,
                ByteHighlightState.OnlyInPanel => TryFindResource("HexOnlyInPanelBrush") as Brush,
                ByteHighlightState.OnlyOther => TryFindResource("HexOnlyOtherBrush") as Brush,
                _ => null
            };
            if (brush is not null)
                drawingContext.DrawRectangle(brush, null, layout.GetHighlightBounds(index, IsAscii));
        }

        for (var index = 0; index < row.Cells.Count; index++)
        {
            var text = IsAscii ? row.Cells[index].Ascii : row.Cells[index].Hex;
            drawingContext.DrawText(Format(text), layout.GetByteBounds(index, IsAscii).TopLeft);
        }
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

    private FormattedText Format(string text) => new(
        text, CultureInfo.InvariantCulture, FlowDirection.LeftToRight,
        new Typeface(FontFamily, FontStyle, FontWeight, FontStretch), FontSize, Foreground, _pixelsPerDip);

}
