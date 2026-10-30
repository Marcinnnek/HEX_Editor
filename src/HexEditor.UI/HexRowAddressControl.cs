using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace HexEditor.UI;

/// <summary>Renders the address gutter without a TextBlock layout pass for every visible row.</summary>
public sealed class HexRowAddressControl : Control
{
    public HexRowAddressControl() => Focusable = false;

    public static readonly DependencyProperty RowProperty = DependencyProperty.Register(
        nameof(Row), typeof(HexViewRow), typeof(HexRowAddressControl),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

    public HexViewRow? Row
    {
        get => (HexViewRow?)GetValue(RowProperty);
        set => SetValue(RowProperty, value);
    }

    protected override Size MeasureOverride(Size constraint) => new(100, 18);

    protected override void OnRender(DrawingContext drawingContext)
    {
        base.OnRender(drawingContext);
        var row = Row;
        if (row is null) return;
        var text = row.IsGapMarker ? row.GapDescription ?? string.Empty : row.Address;
        var formatted = new FormattedText(text, CultureInfo.InvariantCulture, FlowDirection.LeftToRight,
            new Typeface(FontFamily, row.IsGapMarker ? FontStyles.Italic : FontStyles.Normal, FontWeight, FontStretch),
            FontSize, Foreground, VisualTreeHelper.GetDpi(this).PixelsPerDip)
        {
            MaxTextWidth = 100,
            Trimming = TextTrimming.CharacterEllipsis
        };
        drawingContext.DrawText(formatted, new Point(0, 0));
    }
}
