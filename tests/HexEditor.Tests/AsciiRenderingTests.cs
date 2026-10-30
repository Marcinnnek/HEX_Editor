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

        var byteWidth = (int)(ascii.DesiredSize.Width / row.Cells.Count);
        var unchangedX = 1;
        var changedX = byteWidth + 1;
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
    }
}
