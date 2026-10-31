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

    protected override Size MeasureOverride(Size constraint) => new(double.IsInfinity(constraint.Width) ? 100 : constraint.Width, 18);

    protected override void OnRender(DrawingContext drawingContext)
    {
        base.OnRender(drawingContext);
        var row = Row;
        if (row is null) return;
        var text = row.IsGapMarker ? row.GapDescription ?? string.Empty : row.Address;
        var isGap = row.IsGapMarker;
        ToolTip = isGap ? text : null;
        var width = Math.Max(0, ActualWidth - 12);
        var formatted = new FormattedText(text, CultureInfo.InvariantCulture, FlowDirection.LeftToRight,
            new Typeface(FontFamily, row.IsGapMarker ? FontStyles.Italic : FontStyles.Normal, FontWeight, FontStretch),
            FontSize, Foreground, VisualTreeHelper.GetDpi(this).PixelsPerDip)
        {
            MaxTextWidth = width,
            TextAlignment = isGap ? TextAlignment.Left : TextAlignment.Center,
            Trimming = TextTrimming.CharacterEllipsis
        };
        drawingContext.DrawText(formatted, new Point(6, 0));
    }
}
