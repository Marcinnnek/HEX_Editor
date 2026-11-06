using HexEditor.UI;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace HexEditor.Tests;

[TestClass]
public sealed class AsciiRenderingTests
{
    [STATestMethod]
    public void AsciiDataIsTintedInBothThemesAndComparisonHighlightsCoverChangedBytes()
    {
        var row = new HexViewRow("00001000", "20 20 20", "   ", false, null)
        {
            Cells =
            [
                new HexByteCell("20", " ", 0x1000, ByteHighlightState.None),
                new HexByteCell("20", " ", 0x1001, ByteHighlightState.Different),
                new HexByteCell("20", " ", 0x1002, ByteHighlightState.None)
            ]
        };
        var ascii = new HexByteRowControl { IsAscii = true, Row = row, FontFamily = new FontFamily("Consolas"), FontSize = 16 };
        ascii.Resources["HexDifferentBrush"] = new SolidColorBrush(Color.FromArgb(0x88, 0x8B, 0x45, 0x45));

        ascii.Resources["HexAsciiBackgroundBrush"] = new SolidColorBrush(Color.FromArgb(0x14, 0x6B, 0x87, 0xA6));
        var light = Render(ascii);
        ascii.Resources["HexAsciiBackgroundBrush"] = new SolidColorBrush(Color.FromArgb(0x24, 0x4C, 0x6E, 0x93));
        ascii.InvalidateVisual();
        var dark = Render(ascii);

        var byteWidth = (int)((ascii.DesiredSize.Width - 12) / row.Cells.Count);
        var unchangedX = 7;
        var changedX = byteWidth + 7;
        Assert.IsGreaterThan((byte)0, light.AlphaAt(unchangedX, 1));
        Assert.IsGreaterThan(light.AlphaAt(unchangedX, 1), light.AlphaAt(changedX, 1));
        Assert.IsGreaterThan(dark.AlphaAt(unchangedX, 1), dark.AlphaAt(changedX, 1));
        Assert.AreNotEqual(light.AlphaAt(unchangedX, 1), dark.AlphaAt(unchangedX, 1));
    }

    [STATestMethod]
    public void Find_indicator_remains_visible_alongside_difference_background_and_edited_outline()
    {
        var row = new HexViewRow("00001000", "20", " ", false, null)
        {
            Cells = [new HexByteCell("20", " ", 0x1000, ByteHighlightState.Different, IsFindMatch: true, IsEdited: true)]
        };
        foreach (var isAscii in new[] { false, true })
        {
            var control = new HexByteRowControl { Row = row, IsAscii = isAscii, FontFamily = new FontFamily("Consolas"), FontSize = 16 };
            control.Resources["HexDifferentBrush"] = new SolidColorBrush(Color.FromArgb(0xFF, 0x30, 0x30, 0x30));
            control.Resources["HexEditedByteBrush"] = new SolidColorBrush(Color.FromArgb(0xFF, 0xFF, 0x00, 0x00));
            control.Resources["HexFindMatchBrush"] = new SolidColorBrush(Color.FromArgb(0xFF, 0x00, 0x00, 0xFF));

            var pixels = Render(control);

            Assert.IsGreaterThan(0, pixels.CountExact(0xFF, 0x00, 0x00), "The edited outline must remain visible.");
            Assert.IsGreaterThan(0, pixels.CountExact(0x00, 0x00, 0xFF), "The Find indicator must remain visible.");
            Assert.IsGreaterThan(0, pixels.CountExact(0x30, 0x30, 0x30), "The difference background must remain visible.");
        }
    }

    [STATestMethod]
    public void AdjacentEditedBytesDoNotDrawAnInternalOutlineInHexOrAscii()
    {
        var row = new HexViewRow("00001000", "20 21", " !", false, null)
        {
            Cells =
            [
                new HexByteCell("20", " ", 0x1000, ByteHighlightState.None, IsEdited: true),
                new HexByteCell("21", "!", 0x1001, ByteHighlightState.None, IsEdited: true)
            ]
        };
        foreach (var isAscii in new[] { false, true })
        {
            var control = new HexByteRowControl
            {
                Row = row, IsAscii = isAscii, FontFamily = new FontFamily("Consolas"), FontSize = 16
            };
            control.Resources["HexEditedByteBrush"] = new SolidColorBrush(Colors.Red);
            var pixels = Render(control);
            var spacing = isAscii ? 0 : GridSpacingSettings.Current.HorizontalByteSpacing;
            var byteWidth = (control.DesiredSize.Width - (isAscii ? 12 : spacing)) / 2;
            var innerLeft = (int)Math.Floor((isAscii ? 6 : 0) + byteWidth) - 1;
            var innerRight = (int)Math.Ceiling((isAscii ? 6 : 0) + byteWidth + spacing) + 1;
            var middleY = (int)((control.DesiredSize.Height - GridSpacingSettings.Current.VerticalRowSpacing) / 2);

            Assert.IsGreaterThan(0, pixels.CountExact(0xFF, 0x00, 0x00), "The exterior edited outline must remain visible.");
            Assert.AreEqual(0, pixels.CountExactInArea(0xFF, 0x00, 0x00,
                innerLeft, innerRight, middleY - 2, middleY + 2),
                "Adjacent edited bytes must not have an internal outline.");
        }
    }

    [STATestMethod]
    public void SelectedAndEditedBackgroundsLayerBehindOutlinesInHexAndAscii()
    {
        var row = new HexViewRow("00001000", "20 20 20", "   ", false, null)
        {
            Cells =
            [
                new HexByteCell("20", " ", 0x1000, ByteHighlightState.Different, IsEdited: true),
                new HexByteCell("20", " ", 0x1001, ByteHighlightState.None, IsFindMatch: true, IsEdited: true),
                new HexByteCell("20", " ", 0x1002, ByteHighlightState.None)
            ]
        };
        foreach (var isAscii in new[] { false, true })
        {
            var control = new HexByteRowControl
            {
                Row = row, IsAscii = isAscii, SelectionStart = 0x1001, SelectionEnd = 0x1002,
                FontFamily = new FontFamily("Consolas"), FontSize = 16
            };
            control.Resources["HexDifferentBrush"] = new SolidColorBrush(Colors.Gray);
            control.Resources["HexEditedByteBackgroundBrush"] = new SolidColorBrush(Colors.Green);
            control.Resources["HexEditedByteBrush"] = new SolidColorBrush(Colors.Red);
            control.Resources["HexSelectionBrush"] = new SolidColorBrush(Colors.Blue);
            control.Resources["HexSelectedByteBorderBrush"] = new SolidColorBrush(Colors.Yellow);
            control.Resources["HexFindMatchBrush"] = new SolidColorBrush(Colors.Magenta);

            var pixels = Render(control);
            var spacing = isAscii ? 0 : GridSpacingSettings.Current.HorizontalByteSpacing;
            var byteWidth = (control.DesiredSize.Width - (isAscii ? 12 : 2 * spacing)) / 3;
            var stride = byteWidth + spacing;
            var startX = isAscii ? 6 : 0;
            var middleY = (int)((control.DesiredSize.Height - GridSpacingSettings.Current.VerticalRowSpacing) / 2);
            int FillCount(int cell, byte red, byte green, byte blue) => pixels.CountExactInArea(red, green, blue,
                (int)(startX + cell * stride + 3), (int)(startX + cell * stride + byteWidth - 3),
                middleY - 2, middleY + 2);

            Assert.IsGreaterThan(0, FillCount(0, 0, 0x80, 0), "Edited byte uses its background color.");
            Assert.IsGreaterThan(0, FillCount(1, 0, 0, 0xFF), "Selection background covers an edited byte.");
            Assert.AreEqual(0, FillCount(1, 0, 0x80, 0), "Edited background must not cover selection.");
            Assert.IsGreaterThan(0, pixels.CountExact(0xFF, 0, 0), "Edited border remains visible.");
            Assert.IsGreaterThan(0, pixels.CountExact(0xFF, 0xFF, 0), "Selected border remains visible.");
            Assert.IsGreaterThan(0, pixels.CountExact(0xFF, 0, 0xFF), "Find marker remains visible.");
        }
    }

    private static PixelBuffer Render(HexByteRowControl control)
    {
        control.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        control.Arrange(new Rect(new Point(), control.DesiredSize));
        control.UpdateLayout();
        var width = (int)Math.Ceiling(control.DesiredSize.Width);
        var height = (int)Math.Ceiling(control.DesiredSize.Height);
        var bitmap = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(control);
        var pixels = new byte[width * height * 4];
        bitmap.CopyPixels(pixels, width * 4, 0);
        return new PixelBuffer(pixels, width);
    }

    private sealed record PixelBuffer(byte[] Pixels, int Width)
    {
        public byte AlphaAt(int x, int y) => Pixels[(y * Width + x) * 4 + 3];
        public int CountExact(byte red, byte green, byte blue) => Enumerable.Range(0, Pixels.Length / 4)
            .Count(index => Pixels[index * 4 + 2] == red && Pixels[index * 4 + 1] == green && Pixels[index * 4] == blue);

        public int CountExactInArea(byte red, byte green, byte blue, int left, int right, int top, int bottom)
        {
            var count = 0;
            for (var y = top; y <= bottom; y++)
                for (var x = left; x <= right; x++)
                {
                    var index = (y * Width + x) * 4;
                    if (Pixels[index + 2] == red && Pixels[index + 1] == green && Pixels[index] == blue)
                        count++;
                }
            return count;
        }
    }
}
