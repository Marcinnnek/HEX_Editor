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

    public static readonly DependencyProperty SelectionStartProperty = DependencyProperty.Register(
        nameof(SelectionStart), typeof(uint?), typeof(HexByteRowControl),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty SelectionEndProperty = DependencyProperty.Register(
        nameof(SelectionEnd), typeof(uint?), typeof(HexByteRowControl),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

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

    public uint? SelectionStart { get => (uint?)GetValue(SelectionStartProperty); set => SetValue(SelectionStartProperty, value); }
    public uint? SelectionEnd { get => (uint?)GetValue(SelectionEndProperty); set => SetValue(SelectionEndProperty, value); }

    public bool TryGetAddressAt(Point point, out uint address)
    {
        address = default;
        var row = Row;
        if (row is null || row.IsGapMarker || point.X < 0 || point.Y < 0) return false;
        EnsureMetrics();
        var layout = CreateLayout();
        if (!layout.TryGetByteIndexAt(point, row.Cells.Count, IsAscii, out var index) ||
            row.Cells[index].Hex.Length == 0 || row.Cells[index].Address is not uint selected) return false;
        address = selected;
        return true;
    }

    internal bool IsOuterInsetAt(Point point)
    {
        if (!IsAscii || point.X < 0 || point.Y < 0) return false;
        EnsureMetrics();
        var capacity = RowCapacity > 0 ? RowCapacity : Row?.Cells.Count ?? 0;
        var width = CreateLayout().GetRowSize(capacity, isAscii: true).Width;
        return point.Y < _lineHeight && point.X < width && (point.X < 6 || point.X >= width - 6);
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
        var hasSelection = SelectionStart is not null && SelectionEnd is not null;
        var owner = hasSelection || row.Cells.Any(static cell => cell.IsEdited) ? FindOwner() : null;
        var above = owner?.GetAdjacentRow(row, -1);
        var below = owner?.GetAdjacentRow(row, 1);
        var bytesPerRow = owner?.BytesPerRow ?? RowCapacity;
        var editedBackground = TryFindResource("HexEditedByteBackgroundBrush") as Brush;
        var selectedBackground = TryFindResource("HexSelectionBrush") as Brush;
        var editedBorder = TryFindResource("HexEditedByteBrush") as Brush;
        var selectedBorder = TryFindResource("HexSelectedByteBorderBrush") as Brush;
        for (var index = 0; index < row.Cells.Count; index++)
        {
            var cell = row.Cells[index];
            var comparisonBrush = cell.Highlight switch
            {
                ByteHighlightState.Different => TryFindResource("HexDifferentBrush") as Brush,
                ByteHighlightState.OnlyInA => TryFindResource("HexOnlyInABrush") as Brush,
                ByteHighlightState.OnlyInB => TryFindResource("HexOnlyInBBrush") as Brush,
                _ => null
            };
            if (comparisonBrush is not null)
                drawingContext.DrawRectangle(comparisonBrush, null, layout.GetHighlightBounds(index, IsAscii));
            if (cell.IsEdited && cell.Hex.Length > 0 && cell.Address is not null && editedBackground is not null)
                DrawFill(drawingContext, layout, index, row.Cells.Count, editedBackground,
                    ByteOutlineEdges.ForEdited(row, above, below, index, bytesPerRow));
            if (cell.Hex.Length > 0 && cell.Address is uint address &&
                SelectionStart is uint selectionStart && SelectionEnd is uint selectionEnd &&
                address >= selectionStart && address <= selectionEnd && selectedBackground is not null)
                DrawFill(drawingContext, layout, index, row.Cells.Count, selectedBackground,
                    ByteOutlineEdges.ForSelection(row, above, below, index, bytesPerRow,
                        selectionStart, selectionEnd));
        }

        for (var index = 0; index < row.Cells.Count; index++)
        {
            var cellAddress = row.Cells[index].Address;
            if (row.Cells[index].IsEdited && row.Cells[index].Hex.Length > 0 &&
                row.Cells[index].Address is not null && editedBorder is not null)
                DrawOutline(drawingContext, layout, index, row.Cells.Count, editedBorder,
                    ByteOutlineEdges.ForEdited(row, above, below, index, bytesPerRow));
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

            // Keep the selected-byte outline above comparison, edited, and Find indicators.
            if (row.Cells[index].Hex.Length > 0 && cellAddress is uint address &&
                SelectionStart is uint selectionStart && SelectionEnd is uint selectionEnd &&
                address >= selectionStart && address <= selectionEnd &&
                selectedBorder is not null)
            {
                DrawOutline(drawingContext, layout, index, row.Cells.Count, selectedBorder,
                    ByteOutlineEdges.ForSelection(row, above, below, index, bytesPerRow,
                        selectionStart, selectionEnd));
            }
        }

        drawingContext.DrawDrawing(GetRowDrawing(row, layout));
    }

    private void DrawFill(DrawingContext drawingContext, ByteGridLayout layout, int index,
        int cellCount, Brush brush, ByteOutlineEdges edges)
    {
        var bounds = layout.GetHighlightBounds(index, IsAscii);
        var right = bounds.Right + (index + 1 < cellCount && !edges.Right && !IsAscii
            ? layout.HorizontalSpacing : 0);
        var bottom = edges.Bottom ? bounds.Bottom : bounds.Bottom + layout.VerticalSpacing;
        drawingContext.DrawRectangle(brush, null,
            new Rect(bounds.Left, bounds.Top, right - bounds.Left, bottom - bounds.Top));
    }

    private void DrawOutline(DrawingContext drawingContext, ByteGridLayout layout, int index,
        int cellCount, Brush brush, ByteOutlineEdges edges)
    {
        var bounds = layout.GetHighlightBounds(index, IsAscii);
        var joinedLeft = index > 0 && !edges.Left;
        var joinedRight = index + 1 < cellCount && !edges.Right;
        var halfSpacing = IsAscii ? 0 : layout.HorizontalSpacing / 2d;
        var left = bounds.Left - (joinedLeft ? halfSpacing : 0);
        var right = bounds.Right + (joinedRight ? halfSpacing : 0);
        var pen = new Pen(brush, 2);
        if (edges.Top) drawingContext.DrawLine(pen, new Point(left, bounds.Top), new Point(right, bounds.Top));
        if (edges.Bottom) drawingContext.DrawLine(pen, new Point(left, bounds.Bottom), new Point(right, bounds.Bottom));
        var sideBottom = edges.Bottom ? bounds.Bottom : bounds.Bottom + layout.VerticalSpacing;
        if (edges.Left) drawingContext.DrawLine(pen, new Point(bounds.Left, bounds.Top), new Point(bounds.Left, sideBottom));
        if (edges.Right) drawingContext.DrawLine(pen, new Point(bounds.Right, bounds.Top), new Point(bounds.Right, sideBottom));
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

    private HexViewControl? FindOwner()
    {
        for (DependencyObject? current = this; current is not null; current = VisualTreeHelper.GetParent(current))
            if (current is HexViewControl owner) return owner;
        return null;
    }

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
            for (var index = 0; index < row.Cells.Count; index++)
            {
                // Draw each cell at its own grid position. Empty HEX cells must still
                // reserve their slot so later bytes stay under their absolute address.
                var cellText = IsAscii ? row.Cells[index].Ascii : row.Cells[index].Hex;
                context.DrawText(Format(cellText), layout.GetByteBounds(index, IsAscii).TopLeft);
            }
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
