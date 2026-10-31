using System.Globalization;
using System.Windows;
using System.Windows.Media;
using HexEditor.Core;
using HexEditor.Formats;
using HexEditor.UI;

namespace HexEditor.Tests;

[TestClass]
[DoNotParallelize]
public sealed class HexColumnGapAcceptanceTests
{
    [TestMethod]
    public void RowDoesNotRepeatBytesFromTheNextBlockAfterAnAddressGap()
    {
        var image = MemoryImage.FromSegments([
            new MemorySegment(0xA0000002, [0x63, 0xDE, 0xCC, 0xC8, 0x86, 0x7A, 0x20, 0x10, 0x48, 0xFE, 0xD9, 0x92]),
            new MemorySegment(0xA0000010, [0x9E, 0x2B, 0xDC])]);
        var model = new HexImageViewModel(image);

        var firstRow = model.Rows[0];
        Assert.AreEqual("A0000000", firstRow.Address);
        Assert.AreEqual("", firstRow.Cells[14].Hex);
        Assert.AreEqual("", firstRow.Cells[15].Hex);
        Assert.AreEqual("A0000010", model.Rows[1].Address);
        Assert.AreEqual("9E", model.Rows[1].Cells[0].Hex);
        Assert.AreEqual(1, model.Rows.Count(row => row.Cells.Any(cell => cell.Hex == "9E")));
    }

    [TestMethod]
    public void RaggedIntelHexAndSRecordRowsKeepEveryByteAtOneAddress()
    {
        var intelHex = string.Join("\n",
            IntelRecord(0x1002, Enumerable.Range(0x63, 12).Select(value => (byte)value)),
            IntelRecord(0x1010, [0x9E, 0x2B, 0xDC]),
            IntelRecord(0x1040, [0xF1, 0x90]),
            ":00000001FF");
        var sRecord = string.Join("\n",
            S1Record(0x1002, Enumerable.Range(0x63, 12).Select(value => (byte)value)),
            S1Record(0x1010, [0x9E, 0x2B, 0xDC]),
            S1Record(0x1040, [0xF1, 0x90]),
            "S9030000FC");

        foreach (var contents in new[] { intelHex, sRecord })
        {
            using var stream = new MemoryStream(System.Text.Encoding.ASCII.GetBytes(contents));
            var image = new MemoryImageFileService().PrepareImport(stream)
                .Apply(OverlapDecision.OverwriteIncoming)!.Image;
            var rows = new HexImageViewModel(image).Rows;

            AssertRowByteCount(rows, 0x1000, 12);
            AssertRowByteCount(rows, 0x1010, 3);
            AssertRowByteCount(rows, 0x1040, 2);
            Assert.AreEqual(1, rows.Sum(row => row.Cells.Count(cell => cell.Hex == "9E")),
                "A byte after a short address gap must not also appear at the end of the preceding row.");
            Assert.AreEqual(1, rows.Sum(row => row.Cells.Count(cell => cell.Hex == "F1")),
                "A byte in a later block must appear only in that block's row.");
        }
    }

    private static void AssertRowByteCount(IList<HexViewRow> rows, uint address, int expected)
    {
        var row = rows.Single(item => item.Address == address.ToString("X8"));
        Assert.AreEqual(expected, row.Cells.Count(cell => cell.Hex.Length > 0), $"Unexpected bytes in row {address:X8}.");
    }

    private static string IntelRecord(ushort address, IEnumerable<byte> data)
    {
        var bytes = data.ToArray();
        var fields = new[] { (byte)bytes.Length, (byte)(address >> 8), (byte)address, (byte)0 }.Concat(bytes).ToArray();
        var checksum = unchecked((byte)-fields.Sum(value => value));
        return $":{string.Concat(fields.Select(value => value.ToString("X2")))}{checksum:X2}";
    }

    private static string S1Record(ushort address, IEnumerable<byte> data)
    {
        var bytes = data.ToArray();
        var fields = new[] { (byte)(bytes.Length + 3), (byte)(address >> 8), (byte)address }.Concat(bytes).ToArray();
        var checksum = unchecked((byte)~fields.Sum(value => value));
        return $"S1{string.Concat(fields.Select(value => value.ToString("X2")))}{checksum:X2}";
    }

    [TestMethod]
    public void FullHexRowEndsTwelveUnitsBeforeFirstAsciiGlyph()
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            Window? window = null;
            try
            {
                foreach (var bytesPerRow in new[] { 8, 16, 32 })
                foreach (var windowed in new[] { false, true })
                {
                    var image = MemoryImage.FromSegments([
                        new MemorySegment(0x1000, Enumerable.Range(0, 64).Select(value => (byte)value).ToArray())]);
                    var model = new HexImageViewModel(image, bytesPerRow, showAscii: true);
                    var view = new HexViewControl
                    {
                        ItemsSource = model.Rows,
                        BytesPerRow = bytesPerRow,
                        ShowAscii = true,
                        UseWindowedScrolling = windowed,
                        Width = 1100,
                        Height = 200
                    };
                    window = new Window { Content = view, Width = 1120, Height = 220, ShowInTaskbar = false };
                    window.Show();
                    window.UpdateLayout();

                    AssertGap(view, bytesPerRow, windowed);

                    window.Close();
                    window = null;
                }
            }
            catch (Exception exception) { failure = exception; }
            finally { window?.Close(); }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
        if (failure is not null) System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(failure).Throw();
    }

    [TestMethod]
    public void MainWindowFullHexRowEndsTwelveUnitsBeforeFirstAsciiGlyph()
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            MainWindow? main = null;
            try
            {
                foreach (var bytesPerRow in new[] { 8, 16, 32 })
                foreach (var windowed in new[] { false, true })
                {
                    main = new MainWindow();
                    main.ViewModel.LoadImage(MemoryImage.FromSegments([
                        new MemorySegment(0x1000, Enumerable.Range(0, 64).Select(value => (byte)value).ToArray())]), null, null);
                    main.ViewModel.ActiveBytesPerRow = bytesPerRow;
                    main.Width = 1200;
                    main.Show();
                    main.UpdateLayout();
                    var view = (HexViewControl)main.FindName("HexRowsView");
                    view.UseWindowedScrolling = windowed;
                    main.UpdateLayout();
                    AssertGap(view, bytesPerRow, windowed);
                    main.Close();
                    main = null;
                }
            }
            catch (Exception exception) { failure = exception; }
            finally { main?.Close(); }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
        if (failure is not null) System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(failure).Throw();
    }

    [TestMethod]
    public void ComparePanelsFullHexRowsEndTwelveUnitsBeforeFirstAsciiGlyph()
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            Window? window = null;
            try
            {
                foreach (var bytesPerRow in new[] { 8, 16, 32 })
                foreach (var windowed in new[] { false, true })
                {
                    var imageA = MemoryImage.FromSegments([
                        new MemorySegment(0x1000, Enumerable.Range(0, 64).Select(value => (byte)value).ToArray())]);
                    var imageB = MemoryImage.FromSegments([
                        new MemorySegment(0x1000, Enumerable.Range(0, 64).Select(value => (byte)(value + 1)).ToArray())]);
                    var compare = new CompareView(new CompareWorkspaceTabViewModel(imageA, imageB, "a.hex", "b.hex"));
                    compare.ViewModel.BytesPerRow = bytesPerRow;
                    window = new Window { Content = compare, Width = 1500, Height = 500, ShowInTaskbar = false };
                    window.Show();
                    window.UpdateLayout();
                    var panels = Descendants<HexViewControl>(compare).ToArray();
                    Assert.HasCount(2, panels);
                    foreach (var panel in panels)
                    {
                        panel.UseWindowedScrolling = windowed;
                        window.UpdateLayout();
                        AssertGap(panel, bytesPerRow, windowed);
                    }
                    window.Close();
                    window = null;
                }
            }
            catch (Exception exception) { failure = exception; }
            finally { window?.Close(); }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
        if (failure is not null) System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(failure).Throw();
    }

    [TestMethod]
    public void WindowedRowsUseTheSameFontMetricsAsTheDataGrid()
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            Window? window = null;
            try
            {
                var image = MemoryImage.FromSegments([
                    new MemorySegment(0x1000, Enumerable.Range(0, 64).Select(value => (byte)value).ToArray())]);
                var model = new HexImageViewModel(image, 32, showAscii: true);
                var view = new HexViewControl
                {
                    ItemsSource = model.Rows,
                    BytesPerRow = 32,
                    ShowAscii = true,
                    UseWindowedScrolling = true,
                    Width = 1100,
                    Height = 200
                };
                view.ItemsGrid.FontSize = 18;
                window = new Window { Content = view, Width = 1120, Height = 220, ShowInTaskbar = false };
                window.Show();
                window.UpdateLayout();
                AssertGap(view, 32, windowed: true);
            }
            catch (Exception exception) { failure = exception; }
            finally { window?.Close(); }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
        if (failure is not null) System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(failure).Throw();
    }

    private static void AssertGap(HexViewControl view, int bytesPerRow, bool windowed)
    {
        var hex = Descendants<HexByteRowControl>(view).First(control =>
            control.IsVisible && !control.IsAscii && control.Row?.Address == "00001000");
        var ascii = Descendants<HexByteRowControl>(view).First(control =>
            control.IsVisible && control.IsAscii && control.Row?.Address == "00001000");
        Assert.AreEqual(bytesPerRow, hex.Row?.Cells.Count, "The first data row must be full for the gap measurement.");
        var sample = new FormattedText("0", CultureInfo.InvariantCulture, FlowDirection.LeftToRight,
            new Typeface(hex.FontFamily, hex.FontStyle, hex.FontWeight, hex.FontStretch),
            hex.FontSize, Brushes.Black, VisualTreeHelper.GetDpi(hex).PixelsPerDip);
        var renderedHexWidth = bytesPerRow * 2 * sample.WidthIncludingTrailingWhitespace +
            (bytesPerRow - 1) * GridSpacingSettings.Current.HorizontalByteSpacing;
        var hexEnd = hex.TranslatePoint(new Point(renderedHexWidth, 0), view).X;
        var asciiStart = ascii.TranslatePoint(new Point(6, 0), view).X;
        Assert.AreEqual(12, asciiStart - hexEnd, 1,
            $"BytesPerRow={bytesPerRow}, windowed={windowed}, calculated={view.HexDataContentWidth}, rendered={renderedHexWidth}, control={hex.ActualWidth}, " +
            $"hexOrigin={hex.TranslatePoint(new Point(0, 0), view).X}, asciiOrigin={ascii.TranslatePoint(new Point(0, 0), view).X}, " +
            $"column={view.ItemsGrid.Columns[1].ActualWidth}, hexFont={hex.FontSize}, gridFont={view.ItemsGrid.FontSize}");
    }

    private static IEnumerable<T> Descendants<T>(DependencyObject parent) where T : DependencyObject
    {
        for (var index = 0; index < VisualTreeHelper.GetChildrenCount(parent); index++)
        {
            var child = VisualTreeHelper.GetChild(parent, index);
            if (child is T match) yield return match;
            foreach (var descendant in Descendants<T>(child)) yield return descendant;
        }
    }
}
