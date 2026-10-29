using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace HexEditor.UI;

/// <summary>Draws a row of byte text and highlights without a visual element per byte.</summary>
public sealed class HexByteRowControl : Control
{
    private static readonly Brush DifferentBrush = CreateBrush(0xFF, 0xB8, 0xB8);
    private static readonly Brush OnlyInPanelBrush = CreateBrush(0xFF, 0xD9, 0x93);
    private static readonly Brush OnlyOtherBrush = CreateBrush(0xD7, 0xD7, 0xD7);
    private double _characterWidth;
    private double _lineHeight;
    private double _pixelsPerDip;

    public HexByteRowControl()
    {
        Focusable = false;
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
        if (row is null || row.IsGapMarker || point.X < 0) return false;
        EnsureMetrics();
        var cellWidth = _characterWidth * (IsAscii ? 1 : 3);
        if (cellWidth <= 0) return false;
        var index = (int)(point.X / cellWidth);
        if ((uint)index >= (uint)row.Cells.Count || row.Cells[index].Address is not uint selected) return false;
        address = selected;
        return true;
    }

    protected override Size MeasureOverride(Size constraint)
    {
        EnsureMetrics();
        return new Size((Row?.Cells.Count ?? 0) * _characterWidth * (IsAscii ? 1 : 3), _lineHeight);
    }

    protected override void OnRender(DrawingContext drawingContext)
    {
        base.OnRender(drawingContext);
        var row = Row;
        if (row is null || row.IsGapMarker) return;
        EnsureMetrics();

        var cellWidth = _characterWidth * (IsAscii ? 1 : 3);
        drawingContext.DrawRectangle(Brushes.Transparent, null,
            new Rect(0, 0, row.Cells.Count * cellWidth, _lineHeight));
        for (var index = 0; index < row.Cells.Count; index++)
        {
            var brush = row.Cells[index].Highlight switch
            {
                "Different" => DifferentBrush,
                "OnlyInPanel" => OnlyInPanelBrush,
                "OnlyOther" => OnlyOtherBrush,
                _ => null
            };
            if (brush is not null)
                drawingContext.DrawRectangle(brush, null, new Rect(index * cellWidth, 0, cellWidth, _lineHeight));
        }

        var text = IsAscii ? row.Ascii : row.HexData;
        if (!string.IsNullOrEmpty(text))
            drawingContext.DrawText(Format(text), new Point(0, 0));
    }

    protected override void OnPropertyChanged(DependencyPropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);
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

    private FormattedText Format(string text) => new(
        text, CultureInfo.InvariantCulture, FlowDirection.LeftToRight,
        new Typeface(FontFamily, FontStyle, FontWeight, FontStretch), FontSize, Foreground, _pixelsPerDip);

    private static Brush CreateBrush(byte red, byte green, byte blue)
    {
        var brush = new SolidColorBrush(Color.FromRgb(red, green, blue));
        brush.Freeze();
        return brush;
    }
}
