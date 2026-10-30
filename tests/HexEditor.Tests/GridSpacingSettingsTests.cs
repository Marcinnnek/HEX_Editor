using HexEditor.UI;
using HexEditor.Core;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace HexEditor.Tests;

[TestClass]
public sealed class GridSpacingSettingsTests
{
    [TestMethod]
    public void NewProfileUsesTighterGridDefaults()
    {
        var settings = new GridSpacingSettings(Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".json"));

        Assert.AreEqual(2, settings.HorizontalByteSpacing);
        Assert.AreEqual(1, settings.VerticalRowSpacing);
    }

    [TestMethod]
    public void IndependentSpacingChoicesPersistAcrossInstances()
    {
        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".json");
        var settings = new GridSpacingSettings(path);

        settings.SetSpacing(horizontalByteSpacing: 7, verticalRowSpacing: 4);
        var restored = new GridSpacingSettings(path);

        Assert.AreEqual(7, restored.HorizontalByteSpacing);
        Assert.AreEqual(4, restored.VerticalRowSpacing);
    }

    [TestMethod]
    public void ChangingOneSpacingChoicePreservesTheOtherAcrossRestart()
    {
        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".json");
        var settings = new GridSpacingSettings(path);

        settings.SetSpacing(horizontalByteSpacing: 7, verticalRowSpacing: 1);
        Assert.AreEqual(1, settings.VerticalRowSpacing);
        settings.SetSpacing(horizontalByteSpacing: 7, verticalRowSpacing: 8);
        Assert.AreEqual(7, settings.HorizontalByteSpacing);

        var restored = new GridSpacingSettings(path);
        Assert.AreEqual(7, restored.HorizontalByteSpacing);
        Assert.AreEqual(8, restored.VerticalRowSpacing);
    }

    [TestMethod]
    public void ByteGridLayoutLeavesSeparatorsOutsideHighlightBounds()
    {
        var layout = new ByteGridLayout(CharacterWidth: 10, LineHeight: 20, HorizontalSpacing: 5, VerticalSpacing: 3);

        Assert.AreEqual(new System.Windows.Rect(0, 0, 20, 20), layout.GetByteBounds(0, isAscii: false));
        Assert.AreEqual(new System.Windows.Rect(25, 0, 20, 20), layout.GetByteBounds(1, isAscii: false));
        Assert.AreEqual(new System.Windows.Rect(0, 0, 20, 20), layout.GetHighlightBounds(0));
    }

    [TestMethod]
    public void ComparisonHighlightMatchesTheSelectedAsciiByteWithoutMovingItsHitArea()
    {
        var layout = new ByteGridLayout(CharacterWidth: 10, LineHeight: 20, HorizontalSpacing: 5, VerticalSpacing: 3);

        Assert.AreEqual(new Rect(10, 0, 10, 20), layout.GetHighlightBounds(1, isAscii: true));
        Assert.IsTrue(layout.TryGetByteIndexAt(new Point(15, 10), 3, isAscii: true, out var byteIndex));
        Assert.AreEqual(1, byteIndex);
        Assert.AreEqual(new Rect(25, 0, 20, 20), layout.GetHighlightBounds(1, isAscii: false));
        Assert.IsFalse(layout.TryGetByteIndexAt(new Point(22, 10), 3, isAscii: false, out _));
    }

    [TestMethod]
    public void ByteGridLayoutMeasuresRowsAndHitTestsUsingTheSameByteBounds()
    {
        var layout = new ByteGridLayout(CharacterWidth: 10, LineHeight: 20, HorizontalSpacing: 5, VerticalSpacing: 3);

        Assert.AreEqual(new System.Windows.Size(45, 23), layout.GetRowSize(2, isAscii: false));
        Assert.AreEqual(new System.Windows.Size(20, 23), layout.GetRowSize(2, isAscii: true));
        Assert.IsTrue(layout.TryGetByteIndexAt(new System.Windows.Point(44.9, 19.9), 2, isAscii: false, out var second));
        Assert.AreEqual(1, second);
        Assert.IsFalse(layout.TryGetByteIndexAt(new System.Windows.Point(21, 10), 2, isAscii: false, out _));
        Assert.IsFalse(layout.TryGetByteIndexAt(new System.Windows.Point(10, 23), 2, isAscii: false, out _));
        Assert.IsTrue(layout.TryGetByteIndexAt(new System.Windows.Point(19, 19.9), 2, isAscii: true, out var asciiSecond));
        Assert.AreEqual(1, asciiSecond);
    }

    [TestMethod]
    public void HorizontalSpacingDoesNotChangeTheVerticalRowBaseline()
    {
        var compact = new ByteGridLayout(CharacterWidth: 10, LineHeight: 20, HorizontalSpacing: 0, VerticalSpacing: 0);
        var wider = compact with { HorizontalSpacing = 8 };
        var expanded = wider with { VerticalSpacing = 4 };

        Assert.AreEqual(compact.GetRowSize(3, false).Height, wider.GetRowSize(3, false).Height, 0.01);
        Assert.AreEqual(compact.GetRowSize(3, false).Width + 16, wider.GetRowSize(3, false).Width, 0.01);
        Assert.AreEqual(wider.GetRowSize(3, false).Width, expanded.GetRowSize(3, false).Width, 0.01);
        Assert.AreEqual(wider.GetRowSize(3, false).Height + 4, expanded.GetRowSize(3, false).Height, 0.01);
        Assert.AreEqual(wider.GetByteBounds(2, false), expanded.GetByteBounds(2, false));
    }

    [TestMethod]
    public void ZeroVerticalSpacingKeepsAOnePixelMaximumBaselineAndHitTargets()
    {
        var compact = new ByteGridLayout(CharacterWidth: 10, LineHeight: 20, HorizontalSpacing: 5, VerticalSpacing: 0);
        var expanded = compact with { VerticalSpacing = 4 };

        Assert.AreEqual(0, compact.GetRowSize(2, isAscii: false).Height - compact.LineHeight, 0.01);
        Assert.AreEqual(4, expanded.GetRowSize(2, isAscii: false).Height - expanded.LineHeight, 0.01);
        Assert.AreEqual(compact.GetRowSize(2, false).Height + 4, expanded.GetRowSize(2, false).Height, 0.01);
        Assert.AreEqual(20, compact.GetByteBounds(0, false).Height, 0.01);
        Assert.IsFalse(compact.TryGetByteIndexAt(new Point(1, 20), 2, false, out _));
        Assert.IsTrue(compact.TryGetByteIndexAt(new Point(1, 19.9), 2, false, out _));
        Assert.IsFalse(expanded.TryGetByteIndexAt(new Point(1, 20), 2, false, out _));
        Assert.IsTrue(expanded.TryGetByteIndexAt(new Point(1, 19.9), 2, false, out _));
    }

    [TestMethod]
    public void VerticalRowGapIsOutsideOccupiedByteHitArea()
    {
        var layout = new ByteGridLayout(CharacterWidth: 10, LineHeight: 20, HorizontalSpacing: 5, VerticalSpacing: 8);

        Assert.AreEqual(28, layout.GetRowSize(2, isAscii: false).Height);
        Assert.AreEqual(20, layout.GetByteBounds(0, isAscii: false).Height);
        Assert.IsTrue(layout.TryGetByteIndexAt(new System.Windows.Point(2, 19.9), 2, isAscii: false, out _));
        Assert.IsFalse(layout.TryGetByteIndexAt(new System.Windows.Point(2, 20), 2, isAscii: false, out _));
        Assert.IsFalse(layout.TryGetByteIndexAt(new System.Windows.Point(2, 27.9), 2, isAscii: true, out _));
    }

    [STATestMethod]
    public void OpenHexViewsKeepCorrespondingRowsAlignedWhenVerticalSpacingChanges()
    {
        var image = MemoryImage.FromSegments([new MemorySegment(0x1000, new byte[64])]);
        var rows = new HexImageViewModel(image).Rows;
        var editor = new HexViewControl { ItemsSource = rows };
        var compareA = new HexViewControl { ItemsSource = rows };
        var compareB = new HexViewControl { ItemsSource = rows };
        var panels = new Grid();
        for (var index = 0; index < 3; index++) panels.ColumnDefinitions.Add(new ColumnDefinition());
        Grid.SetColumn(compareA, 1);
        Grid.SetColumn(compareB, 2);
        panels.Children.Add(editor);
        panels.Children.Add(compareA);
        panels.Children.Add(compareB);
        var window = new Window
        {
            Width = 1500, Height = 400, Content = panels,
            Left = -2000, Top = -1000, WindowStartupLocation = WindowStartupLocation.Manual,
            ShowActivated = false, ShowInTaskbar = false
        };
        try
        {
            window.Show();
            editor.SetValue(HexViewControl.RowGapProperty, new Thickness());
            compareA.SetValue(HexViewControl.RowGapProperty, new Thickness());
            compareB.SetValue(HexViewControl.RowGapProperty, new Thickness());
            panels.UpdateLayout();
            var before = RowDistance(editor);
            Assert.AreEqual(before, RowDistance(compareA), 0.01);
            Assert.AreEqual(before, RowDistance(compareB), 0.01);

            var gap = new Thickness(0, 0, 0, 8);
            editor.SetValue(HexViewControl.RowGapProperty, gap);
            compareA.SetValue(HexViewControl.RowGapProperty, gap);
            compareB.SetValue(HexViewControl.RowGapProperty, gap);
            panels.UpdateLayout();

            var after = RowDistance(editor);
            Assert.AreEqual(before + 8, after, 0.01);
            Assert.AreEqual(after, RowDistance(compareA), 0.01);
            Assert.AreEqual(after, RowDistance(compareB), 0.01);
        }
        finally { window.Close(); }
    }

    [STATestMethod]
    [DoNotParallelize]
    public void RenderedByteRowsAtZeroSpacingHaveAtMostOneVisiblePixelBetweenLines()
    {
        var settings = GridSpacingSettings.Current;
        var horizontalSpacing = settings.HorizontalByteSpacing;
        var verticalSpacing = settings.VerticalRowSpacing;
        settings.SetSpacing(horizontalSpacing, 0);
        var row = new HexViewRow("00001000", "10", ".", false, null)
        {
            Cells = [new HexByteCell("10", ".", 0x1000, ByteHighlightState.None)]
        };
        var control = new HexByteRowControl { Row = row };
        try
        {
            control.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            var line = new FormattedText(
                "0", CultureInfo.InvariantCulture, FlowDirection.LeftToRight,
                new Typeface(control.FontFamily, control.FontStyle, control.FontWeight, control.FontStretch),
                control.FontSize, control.Foreground, VisualTreeHelper.GetDpi(control).PixelsPerDip);
            var visibleGap = control.DesiredSize.Height - line.Height;

            Assert.IsTrue(visibleGap >= 0 && visibleGap <= 1,
                $"Expected a rendered baseline gap from 0 to 1 pixel, but found {visibleGap:0.##} pixels.");
            Assert.IsTrue(control.TryGetAddressAt(new Point(1, line.Height - 0.1), out _));
            Assert.IsFalse(control.TryGetAddressAt(new Point(1, line.Height + 0.1), out _));
        }
        finally { settings.SetSpacing(horizontalSpacing, verticalSpacing); }
    }

    private static double RowDistance(HexViewControl view)
    {
        var first = (DataGridRow?)view.ItemsGrid.ItemContainerGenerator.ContainerFromIndex(0);
        var second = (DataGridRow?)view.ItemsGrid.ItemContainerGenerator.ContainerFromIndex(1);
        Assert.IsNotNull(first);
        Assert.IsNotNull(second);
        return second.TranslatePoint(new Point(), first).Y;
    }
}
