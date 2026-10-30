using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace HexEditor.UI;

/// <summary>Draws a row of byte text and highlights without a visual element per byte.</summary>
public sealed class HexByteRowControl : Control
{
    private static readonly Brush DifferentBrush = CreateBrush(0xD0, 0x38, 0x38);
    private static readonly Brush OnlyInPanelBrush = CreateBrush(0xD0, 0x85, 0x14);
    private static readonly Brush OnlyOtherBrush = CreateBrush(0x70, 0x70, 0x70);
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
        var byteWidth = _characterWidth * (IsAscii ? 1 : 2);
        var stride = byteWidth + (IsAscii ? 0 : GridSpacingSettings.Current.HorizontalByteSpacing);
        if (byteWidth <= 0 || point.Y >= _lineHeight + GridSpacingSettings.Current.VerticalRowSpacing || point.X >= row.Cells.Count * stride) return false;
        if (!IsAscii && point.X % stride >= byteWidth) return false;
        var index = (int)(point.X / stride);
        if ((uint)index >= (uint)row.Cells.Count || row.Cells[index].Address is not uint selected) return false;
        address = selected;
        return true;
    }

    protected override Size MeasureOverride(Size constraint)
    {
        EnsureMetrics();
        var count = Row?.Cells.Count ?? 0;
        var width = _characterWidth * (IsAscii ? 1 : 2);
        var spacing = IsAscii ? 0 : GridSpacingSettings.Current.HorizontalByteSpacing;
        return new Size(count == 0 ? 0 : count * width + (count - 1) * spacing,
            _lineHeight + GridSpacingSettings.Current.VerticalRowSpacing);
    }

    protected override void OnRender(DrawingContext drawingContext)
    {
        base.OnRender(drawingContext);
        var row = Row;
        if (row is null || row.IsGapMarker) return;
        EnsureMetrics();

        var byteWidth = _characterWidth * (IsAscii ? 1 : 2);
        var stride = byteWidth + (IsAscii ? 0 : GridSpacingSettings.Current.HorizontalByteSpacing);
        drawingContext.DrawRectangle(Brushes.Transparent, null,
            new Rect(0, 0, row.Cells.Count == 0 ? 0 : row.Cells.Count * byteWidth + (row.Cells.Count - 1) * (stride - byteWidth), _lineHeight));
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
                drawingContext.DrawRectangle(brush, null, new Rect(index * stride, 0, byteWidth, _lineHeight));
        }

        for (var index = 0; index < row.Cells.Count; index++)
        {
            var text = IsAscii ? row.Cells[index].Ascii : row.Cells[index].Hex;
            drawingContext.DrawText(Format(text), new Point(index * stride, 0));
        }
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

    private void Control_Loaded(object sender, RoutedEventArgs e) => GridSpacingSettings.SpacingChanged += SpacingChanged;
    private void Control_Unloaded(object sender, RoutedEventArgs e) => GridSpacingSettings.SpacingChanged -= SpacingChanged;

    private void SpacingChanged() => Dispatcher.BeginInvoke(() =>
    {
        InvalidateMeasure();
        InvalidateVisual();
    });

    private FormattedText Format(string text) => new(
        text, CultureInfo.InvariantCulture, FlowDirection.LeftToRight,
        new Typeface(FontFamily, FontStyle, FontWeight, FontStretch), FontSize, Foreground, _pixelsPerDip);

    private static Brush CreateBrush(byte red, byte green, byte blue)
    {
        var brush = new SolidColorBrush(Color.FromArgb(0x55, red, green, blue));
        brush.Freeze();
        return brush;
    }
}
