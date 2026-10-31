using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using HexEditor.Core;
using HexEditor.UI;

namespace HexEditor.Tests;

[TestClass]
[DoNotParallelize]
public sealed class LiveEditorScrollingAcceptanceTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public void EditorWindowPreservesByteAndShiftRangeSelectionAndByteEditing()
    {
        Exception? failure = null;
        var selected = new List<(uint Start, uint End)>();
        var editDialogOpened = false;
        var editorDoubleClickRaised = false;
        var thread = new Thread(() =>
        {
            MainWindow? main = null;
            try
            {
                main = new MainWindow();
                main.ViewModel.LoadImage(MemoryImage.FromSegments([new MemorySegment(0x1000, Enumerable.Range(0, 64).Select(value => (byte)value).ToArray())]), null, null);
                main.Show();
                main.UpdateLayout();
                var view = (HexViewControl)main.FindName("HexRowsView");
                Assert.IsTrue(main.ViewModel.CanEdit);
                view.WindowedByteDoubleClicked += (_, _) => editorDoubleClickRaised = true;
                view.AddressRangeSelected += (_, range) => selected.Add((range.StartAddress, range.EndAddress));
                var firstHexRow = Descendants<HexByteRowControl>(view).First(item => !item.IsAscii && item.Row?.Address == "00001000");

                Assert.IsTrue(view.SelectByteAt(firstHexRow, new Point(3, 3), extendSelection: false));
                Assert.IsTrue(view.SelectByteAt(firstHexRow, new Point(50, 3), extendSelection: true));
                Assert.HasCount(2, selected);
                Assert.AreEqual((0x1000u, 0x1000u), selected[0]);
                Assert.AreEqual((0x1000u, 0x1003u), selected[1]);

                var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(25) };
                timer.Tick += (_, _) =>
                {
                    var dialog = main.OwnedWindows.OfType<EditRowWindow>().FirstOrDefault();
                    if (dialog is null) return;
                    editDialogOpened = true;
                    timer.Stop();
                    dialog.Close();
                };
                timer.Start();
                var doubleClick = new MouseButtonEventArgs(Mouse.PrimaryDevice, Environment.TickCount, MouseButton.Left)
                {
                    RoutedEvent = UIElement.MouseLeftButtonDownEvent,
                    Source = firstHexRow
                };
                typeof(MouseButtonEventArgs).GetProperty(nameof(MouseButtonEventArgs.ClickCount))!.SetValue(doubleClick, 2);
                view.RaiseWindowedByteDoubleClick(firstHexRow, doubleClick, new Point(50, 3));
                timer.Stop();
                Assert.IsTrue(editorDoubleClickRaised, "The byte double-click should reach HexViewControl.");
                Assert.IsTrue(editDialogOpened, "A double-click on a visible byte opens the row editor.");
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
    public void ShiftSelectionAcrossRowsOutlinesOnlyOccupiedBytesInSparseImage()
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            Window? window = null;
            try
            {
                var model = new HexImageViewModel(MemoryImage.FromSegments([
                    new MemorySegment(0x1000, [0x11, 0x22]),
                    new MemorySegment(0x1010, [0x33, 0x44])]), bytesPerRow: 8);
                var view = new HexViewControl { ItemsSource = model.Rows, BytesPerRow = 8, Width = 1100, Height = 500 };
                window = new Window { Content = view, Width = 1120, Height = 520, ShowInTaskbar = false };
                window.Show();
                window.UpdateLayout();
                Pump(window.Dispatcher);

                var startRow = Descendants<HexByteRowControl>(view)
                    .Single(item => item.IsVisible && !item.IsAscii && item.Row?.Address == "00001000");
                var endRow = Descendants<HexByteRowControl>(view)
                    .Single(item => item.IsVisible && !item.IsAscii && item.Row?.Address == "00001010");
                Assert.IsTrue(view.SelectByteAt(startRow, new Point(18, 3), extendSelection: false));
                Assert.IsTrue(view.SelectByteAt(endRow, new Point(3, 3), extendSelection: true));

                Assert.AreEqual(0x1001u, view.SelectionStart);
                Assert.AreEqual(0x1010u, view.SelectionEnd);
                Assert.AreEqual(0x1001u, startRow.SelectionStart);
                Assert.AreEqual(0x1010u, endRow.SelectionEnd);
                Assert.IsTrue(model.Rows.SelectMany(row => row.Cells)
                    .Where(cell => cell.Address is >= 0x1002 and < 0x1010)
                    .All(cell => cell.Hex.Length == 0), "Address gaps stay blank inside the selected range.");

                Assert.IsTrue(view.SelectByteAt(endRow, new Point(3, 3), extendSelection: false));
                Assert.AreEqual(0x1010u, view.SelectionStart, "A plain byte click replaces the Shift range.");
                Assert.AreEqual(0x1010u, view.SelectionEnd);
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
    public void AsciiColumnUsesRowCapacityAndCollapsesWhenHiddenInBothLayouts()
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            Window? window = null;
            try
            {
                var model = new HexImageViewModel(MemoryImage.FromSegments([
                    new MemorySegment(0x1000, Enumerable.Range(0, 19).Select(value => (byte)value).ToArray())]), 16, true);
                var view = new HexViewControl { ItemsSource = model.Rows, BytesPerRow = 16, Width = 1100, Height = 400 };
                window = new Window { Content = view, Width = 1120, Height = 420, ShowInTaskbar = false };
                window.Show();
                window.UpdateLayout();
                Pump(window.Dispatcher);

                var asciiRows = Descendants<HexByteRowControl>(view).Where(control => control.IsAscii).ToArray();
                Assert.IsGreaterThanOrEqualTo(2, asciiRows.Length);
                Assert.AreEqual(asciiRows[0].DesiredSize.Width, asciiRows[1].DesiredSize.Width, 0.1,
                    "A short final row must retain the active row capacity width.");
                var sample = new FormattedText("0", System.Globalization.CultureInfo.InvariantCulture,
                    FlowDirection.LeftToRight, new Typeface(view.ItemsGrid.FontFamily, view.ItemsGrid.FontStyle,
                        view.ItemsGrid.FontWeight, view.ItemsGrid.FontStretch), view.ItemsGrid.FontSize,
                    view.Foreground, VisualTreeHelper.GetDpi(view).PixelsPerDip);
                var targetAsciiWidth = view.BytesPerRow * sample.WidthIncludingTrailingWhitespace + 12;
                Assert.AreEqual(targetAsciiWidth, view.ItemsGrid.Columns[2].ActualWidth, 1,
                    "The DataGrid ASCII column must use the calculated width, matching windowed layout.");

                view.ShowAscii = false;
                window.UpdateLayout();
                Assert.AreEqual(Visibility.Collapsed, view.ItemsGrid.Columns[2].Visibility);
                view.ShowAscii = true;
                view.UseWindowedScrolling = true;
                window.UpdateLayout();
                Pump(window.Dispatcher);
                asciiRows = Descendants<HexByteRowControl>(view).Where(control => control.IsAscii).ToArray();
                Assert.IsTrue(asciiRows.Length > 0);
                Assert.IsTrue(asciiRows.All(control => Math.Abs(control.DesiredSize.Width - asciiRows[0].DesiredSize.Width) < 0.1),
                    "Windowed rows, including a short final row, must use one ASCII width.");
                Assert.AreEqual(targetAsciiWidth, asciiRows[0].DesiredSize.Width, 1,
                    "The windowed ASCII column must use the same calculated width.");
                view.ShowAscii = false;
                window.UpdateLayout();
                Assert.IsFalse(Descendants<HexByteRowControl>(view).Any(control => control.IsAscii && control.IsVisible));
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
    public void ClickingVisibleEditorByteDoesNotMoveTheViewport()
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            MainWindow? main = null;
            try
            {
                main = new MainWindow();
                main.ViewModel.LoadImage(MemoryImage.FromPatternRange(0x1000, 0x10FFF, [0xAB]), null, null);
                main.Show();
                main.UpdateLayout();
                var view = (HexViewControl)main.FindName("HexRowsView");
                view.ScrollToRow(50);
                main.Dispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);
                Assert.AreEqual(50, view.FirstVisibleRowIndex);

                var address = 0x1000u + (54u * (uint)main.ViewModel.ImageView.BytesPerRow);
                var row = Descendants<HexByteRowControl>(view)
                    .Single(item => !item.IsAscii && item.Row?.Address == address.ToString("X8"));
                Assert.IsTrue(view.SelectByteAt(row, new Point(3, 3), extendSelection: false));

                Assert.AreEqual(address, main.ViewModel.EditorStatus.CurrentAddress);
                Assert.AreEqual(50, view.FirstVisibleRowIndex,
                    "Selecting an already visible byte must not move that row to the top.");
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
    public void RoutedHexInterbyteClickSelectsExpectedNeighborInBothViewModes()
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            Window? window = null;
            try
            {
                var model = new HexImageViewModel(MemoryImage.FromSegments([new MemorySegment(0x1000, [0x10, 0x20])]), 8, showAscii: false);
                var view = new HexViewControl { ItemsSource = model.Rows, BytesPerRow = 8, ShowAscii = false, Width = 600, Height = 240 };
                uint? selectedAddress = null;
                view.AddressRangeSelected += (_, range) => selectedAddress = range.EndAddress;
                window = new Window { Content = view, Width = 620, Height = 260, Left = 100, Top = 100, ShowInTaskbar = false };
                window.Show();
                Pump(window.Dispatcher);
                foreach (var windowed in new[] { false, true })
                {
                    view.UseWindowedScrolling = windowed;
                    Pump(window.Dispatcher);
                    var row = Descendants<HexByteRowControl>(view).Single(item => !item.IsAscii && item.Row?.Address == "00001000");
                    var spacing = GridSpacingSettings.Current.HorizontalByteSpacing;
                    var line = new FormattedText("0", System.Globalization.CultureInfo.InvariantCulture,
                        FlowDirection.LeftToRight, new Typeface(row.FontFamily, row.FontStyle, row.FontWeight, row.FontStretch),
                        row.FontSize, row.Foreground, VisualTreeHelper.GetDpi(row).PixelsPerDip);
                    var byteWidth = line.WidthIncludingTrailingWhitespace * 2;
                    var clickPoint = new Point(byteWidth + spacing / 2d, line.Height / 2d);
                    var handled = view.SelectByteAt(row, clickPoint, extendSelection: false);
                    Assert.IsTrue(handled, "The HEX row handles clicks in the separator.");
                    Assert.AreEqual(0x1001u, selectedAddress,
                        $"A click at {clickPoint} (row size {row.DesiredSize}, spacing {spacing}) selects byte two in {(windowed ? "windowed" : "DataGrid")} mode.");
                    selectedAddress = null;
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

    [STATestMethod]
    [DoNotParallelize]
    public void HexDataColumnTracksRowCapacityAndSpacingInBothLayouts()
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            Window? window = null;
            var settings = GridSpacingSettings.Current;
            var oldHorizontal = settings.HorizontalByteSpacing;
            var oldVertical = settings.VerticalRowSpacing;
            try
            {
                settings.SetSpacing(2, oldVertical);
                var image = MemoryImage.FromSegments([new MemorySegment(0, Enumerable.Range(0, 2048).Select(x => (byte)x).ToArray())]);
                var model = new HexImageViewModel(image, 8, showAscii: false);
                var view = new HexViewControl { ItemsSource = model.Rows, BytesPerRow = 8, ShowAscii = false, UseWindowedScrolling = true, Width = 360, Height = 300 };
                window = new Window { Content = view, Width = 380, Height = 320, ShowInTaskbar = false };
                window.Show();
                Pump(window.Dispatcher);

                var width8 = view.HexDataColumnWidth;
                model.BytesPerRow = 16;
                view.ItemsSource = model.Rows;
                view.BytesPerRow = 16;
                Pump(window.Dispatcher);
                var width16 = view.HexDataColumnWidth;
                model.BytesPerRow = 32;
                view.ItemsSource = model.Rows;
                view.BytesPerRow = 32;
                Pump(window.Dispatcher);
                var width32 = view.HexDataColumnWidth;
                Assert.IsTrue(width8 < width16 && width16 < width32, "HEX width follows the configured bytes per row.");
                var horizontalScroll = Descendants<ScrollViewer>(view).Single(item => item.IsVisible && item.HorizontalScrollBarVisibility == ScrollBarVisibility.Auto);
                Assert.IsGreaterThan(horizontalScroll.ViewportWidth, horizontalScroll.ExtentWidth,
                    "A 32-byte row overflows a narrow view and remains horizontally reachable.");

                view.UseWindowedScrolling = false;
                Pump(window.Dispatcher);
                Assert.AreEqual(width32, view.ItemsGrid.Columns[1].ActualWidth, 0.1,
                    "The DataGrid uses the same computed HEX width as the windowed rows.");
                Assert.IsFalse(view.ItemsGrid.CanUserResizeColumns);
                view.UseWindowedScrolling = true;
                Pump(window.Dispatcher);

                view.ScrollToRow(20);
                horizontalScroll.ScrollToHorizontalOffset(150);
                Pump(window.Dispatcher);
                Assert.IsGreaterThan(0, horizontalScroll.HorizontalOffset);
                var beforeSpacing = width32;
                settings.SetSpacing(5, oldVertical);
                Pump(window.Dispatcher);
                Assert.AreEqual(31 * 3, view.HexDataColumnWidth - beforeSpacing, 0.1,
                    "Each of the 31 inter-byte gaps contributes the configured spacing change.");
                Assert.AreEqual(0, horizontalScroll.HorizontalOffset, 0.1);
                Assert.AreEqual(20, view.FirstVisibleRowIndex, "Recalculating HEX width keeps the vertical row in place.");
            }
            catch (Exception exception) { failure = exception; }
            finally
            {
                window?.Close();
                settings.SetSpacing(oldHorizontal, oldVertical);
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
        if (failure is not null) System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(failure).Throw();
    }

    [TestMethod]
    public void EditorAddressClickSelectsOccupiedRowAndGapWithoutMovingTheWindow()
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            MainWindow? main = null;
            try
            {
                main = new MainWindow();
                main.ViewModel.LoadImage(MemoryImage.FromSegments([
                    new MemorySegment(0x1000, Enumerable.Range(0, 64).Select(value => (byte)value).ToArray()),
                    new MemorySegment(0x1100, [0xA5])]), null, null);
                main.Show();
                main.UpdateLayout();
                var view = (HexViewControl)main.FindName("HexRowsView");
                var rowIndex = Enumerable.Range(0, main.ViewModel.ImageView.Rows.Count)
                    .First(index => main.ViewModel.ImageView.Rows[index].Address == "00001020");
                view.ScrollToRow(rowIndex);
                Pump(main.Dispatcher);
                var occupiedViewport = view.FirstVisibleRowIndex;
                var byteRow = Descendants<HexByteRowControl>(view).First(row => !row.IsAscii && row.Row?.Address == "00001020");
                Assert.IsTrue(view.SelectByteAt(byteRow, new Point(3, 3), extendSelection: false));
                var firstAddress = Descendants<HexRowAddressControl>(view).Single(address => address.IsVisible && address.Row?.Address == "00001020");
                firstAddress.RaiseEvent(new MouseButtonEventArgs(Mouse.PrimaryDevice, Environment.TickCount, MouseButton.Left)
                {
                    RoutedEvent = UIElement.MouseLeftButtonDownEvent,
                    Source = firstAddress
                });
                Assert.AreEqual(rowIndex, view.SelectedRowIndex);
                Assert.AreEqual(0x1020u, main.ViewModel.EditorStatus.CurrentAddress);
                Assert.AreEqual(1u, main.ViewModel.EditorStatus.SelectionLength);
                Assert.AreEqual(occupiedViewport, view.FirstVisibleRowIndex);

                var gapIndex = Enumerable.Range(0, main.ViewModel.ImageView.Rows.Count)
                    .First(index => main.ViewModel.ImageView.Rows[index].IsGapMarker);
                view.ScrollToRow(gapIndex);
                Pump(main.Dispatcher);
                var gapViewport = view.FirstVisibleRowIndex;
                var gapAddress = Descendants<HexRowAddressControl>(view).Single(address => address.IsVisible && address.Row?.IsGapMarker == true);
                gapAddress.RaiseEvent(new MouseButtonEventArgs(Mouse.PrimaryDevice, Environment.TickCount, MouseButton.Left)
                {
                    RoutedEvent = UIElement.MouseLeftButtonDownEvent,
                    Source = gapAddress
                });
                Assert.AreEqual(gapIndex, view.SelectedRowIndex);
                Assert.AreEqual(0x1020u, main.ViewModel.EditorStatus.CurrentAddress,
                    "Selecting an address-gap row leaves the current address unchanged.");
                Assert.AreEqual(gapViewport, view.FirstVisibleRowIndex);
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
    public void DataGridAddressClickRaisesRowSelectionWithoutChangingItsViewport()
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            Window? window = null;
            try
            {
                var image = MemoryImage.FromPatternRange(0x1000, 0x2FFF, [0x2A]);
                var viewModel = new HexImageViewModel(image, 16, showAscii: true);
                var view = new HexViewControl { ItemsSource = viewModel.Rows, Width = 900, Height = 340 };
                AddressRowSelectedEventArgs? selected = null;
                view.AddressRowSelected += (_, args) => selected = args;
                window = new Window { Content = view, Width = 920, Height = 360, Left = -3000, Top = -3000, ShowInTaskbar = false };
                window.Show();
                window.UpdateLayout();
                var grid = view.ItemsGrid;
                grid.ScrollIntoView(grid.Items[80]);
                grid.UpdateLayout();
                var scroll = Descendants<ScrollViewer>(grid).First();
                var offset = scroll.VerticalOffset;
                var address = grid.Columns[0].GetCellContent(grid.Items[80]);
                Assert.IsNotNull(address);
                var click = new MouseButtonEventArgs(Mouse.PrimaryDevice, Environment.TickCount, MouseButton.Left)
                {
                    RoutedEvent = UIElement.PreviewMouseLeftButtonDownEvent,
                    Source = address
                };
                grid.RaiseEvent(click);
                Assert.AreEqual(80, view.SelectedRowIndex);
                Assert.AreEqual("00001500", selected?.Row.Address);
                Assert.AreEqual(offset, scroll.VerticalOffset, 0.1,
                    "Selecting an already visible address row does not scroll the DataGrid.");
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
    public void EditorWindowMapsWheelAndThumbToGlobalRowsAndRendersWithinOneHundredMilliseconds()
    {
        Exception? failure = null;
        var wheelElapsed = TimeSpan.Zero;
        var thumbElapsed = TimeSpan.Zero;
        var thread = new Thread(() =>
        {
            Window? window = null;
            try
            {
                const int width = 32;
                var pattern = Enumerable.Range(0, 4096).Select(index => (byte)((index * 37 + (index >> 4) * 19) & 0xFF)).ToArray();
                var image = MemoryImage.FromPatternRange(0, 99_999_999, pattern);
                var viewModel = new HexImageViewModel(image, width, showAscii: true);
                var view = new HexViewControl { ItemsSource = viewModel.Rows, UseWindowedScrolling = true, Width = 720, Height = 480 };
                window = new Window { Content = view, Width = 740, Height = 500, ShowInTaskbar = false };
                window.Show();
                window.UpdateLayout();
                Pump(window.Dispatcher);

                var bar = Descendants<ScrollBar>(view).Single(item => item.Orientation == Orientation.Vertical && item.IsVisible);
                Assert.IsGreaterThanOrEqualTo(30d, Descendants<Thumb>(bar).Max(thumb => thumb.ActualHeight),
                    "The visible thumb must remain usable for the full logical address range.");
                Assert.AreEqual(0, view.FirstVisibleRowIndex);

                var started = Stopwatch.StartNew();
                RaiseWheel(view, window);
                wheelElapsed = WaitForRenderedRow(window.Dispatcher, view, started, "00000060");
                Assert.AreEqual(3, view.FirstVisibleRowIndex);
                Assert.AreEqual("00000060", view.WindowRows[0].Row.Address);
                Assert.IsLessThan(TimeSpan.FromMilliseconds(100), wheelElapsed,
                    $"Wheel scrolling did not show the expected address within 100 ms ({wheelElapsed.TotalMilliseconds:F1} ms).");

                var thumb = Descendants<Thumb>(bar).First(item => item.ActualHeight >= 30);
                thumb.RaiseEvent(new DragStartedEventArgs(0, 0) { RoutedEvent = Thumb.DragStartedEvent, Source = thumb });
                var thumbStarted = Stopwatch.StartNew();
                thumb.RaiseEvent(new DragDeltaEventArgs(0, 70) { RoutedEvent = Thumb.DragDeltaEvent, Source = thumb });
                var expectedThumbAddress = (view.FirstVisibleRowIndex * width).ToString("X8");
                Assert.IsGreaterThan(3, view.FirstVisibleRowIndex, "Dragging the thumb must advance the global row window.");
                thumbElapsed = WaitForRenderedRow(window.Dispatcher, view, thumbStarted, expectedThumbAddress);
                Assert.AreEqual(expectedThumbAddress, view.WindowRows[0].Row.Address,
                    "Thumb drag maps to the same global row index used by wheel movement.");
                Assert.IsLessThan(TimeSpan.FromMilliseconds(100), thumbElapsed,
                    $"Thumb dragging did not show the expected address within 100 ms ({thumbElapsed.TotalMilliseconds:F1} ms).");

                foreach (var bytesPerRow in new[] { 8, 16, 32 })
                foreach (var showAscii in new[] { false, true })
                {
                    viewModel.BytesPerRow = bytesPerRow;
                    viewModel.ShowAscii = showAscii;
                    view.ItemsSource = viewModel.Rows;
                    view.ScrollToRow(10_000);
                    Pump(window.Dispatcher);
                    Assert.AreEqual(10_000, view.FirstVisibleRowIndex);
                    Assert.AreEqual((10_000L * bytesPerRow).ToString("X8"), view.WindowRows[0].Row.Address);
                }
            }
            catch (Exception exception) { failure = exception; }
            finally { window?.Close(); }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
        if (failure is not null) System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(failure).Throw();
        TestContext.WriteLine($"Actual post-frame visible-row time: wheel={wheelElapsed.TotalMilliseconds:F1} ms, thumb={thumbElapsed.TotalMilliseconds:F1} ms.");
        Assert.IsLessThan(TimeSpan.FromMilliseconds(100), wheelElapsed);
        Assert.IsLessThan(TimeSpan.FromMilliseconds(100), thumbElapsed);
    }

    [DataRow(15)]
    [DataRow(32)]
    [TestMethod]
    public void LastWindowedRowIsFullyVisibleAtTheBottomOfTheScrollbar(int bytesPerRow)
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            Window? window = null;
            try
            {
                var image = MemoryImage.FromPatternRange(0, 99_999_999, [0x5A]);
                var model = new HexImageViewModel(image, bytesPerRow, showAscii: true);
                var view = new HexViewControl
                {
                    ItemsSource = model.Rows, BytesPerRow = bytesPerRow, UseWindowedScrolling = true,
                    Width = 720, Height = 480
                };
                view.ItemsGrid.FontSize = 18;
                window = new Window { Content = view, Width = 740, Height = 500, ShowInTaskbar = false };
                window.Show();
                window.UpdateLayout();
                Pump(window.Dispatcher);

                var bar = Descendants<ScrollBar>(view).Single(item => item.Orientation == Orientation.Vertical && item.IsVisible);
                bar.Value = bar.Maximum;
                window.UpdateLayout();
                Pump(window.Dispatcher);

                Assert.AreEqual(model.Rows.Count - 1, view.WindowRows[^1].RowIndex,
                    $"maximum={bar.Maximum}, viewport={bar.ViewportSize}, value={bar.Value}, first={view.FirstVisibleRowIndex}, visible={view.WindowRows.Count}");
                Assert.AreEqual(model.Rows[^1].Address, view.WindowRows[^1].Row.Address,
                    "The last address must remain at the end of the grid after scrolling to the bottom.");
                var lastAddress = Descendants<HexRowAddressControl>(view)
                    .Single(item => item.Row?.Address == view.WindowRows[^1].Row.Address);
                var viewport = Descendants<ScrollContentPresenter>(view).Single(item => item.IsVisible);
                var top = lastAddress.TranslatePoint(new Point(), viewport).Y;
                Assert.IsGreaterThanOrEqualTo(-0.5, top, "The last address row must enter the viewport.");
                Assert.IsLessThanOrEqualTo(viewport.ActualHeight + 0.5, top + lastAddress.ActualHeight,
                    $"The last row is clipped below the viewport (top={top:F1}, height={lastAddress.ActualHeight:F1}, viewport={viewport.ActualHeight:F1}).");
            }
            catch (Exception exception) { failure = exception; }
            finally { window?.Close(); }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
        if (failure is not null) System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(failure).Throw();
    }

    [DataRow(99_999)]
    [DataRow(9_999_999)]
    [TestMethod]
    public void ChangingEditorWidthToFifteenAtTheEndKeepsTheLastRowAtTheEnd(int lastAddress)
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            MainWindow? main = null;
            try
            {
                main = new MainWindow();
                main.ViewModel.LoadImage(MemoryImage.FromPatternRange(0, (uint)lastAddress, [0x5A]), null, null);
                main.Show();
                Pump(main.Dispatcher);
                var view = (HexViewControl)main.FindName("HexRowsView");
                Assert.IsTrue(view.UseWindowedScrolling);
                var bar = Descendants<ScrollBar>(view).Single(item => item.Orientation == Orientation.Vertical && item.IsVisible);
                bar.Value = bar.Maximum;
                Pump(main.Dispatcher);

                main.ViewModel.ActiveBytesPerRow = 15;
                Pump(main.Dispatcher);

                Assert.AreEqual(main.ViewModel.ImageView.Rows.Count - 1, view.WindowRows[^1].RowIndex);
                Assert.AreEqual(((uint)lastAddress / 15 * 15).ToString("X8"), view.WindowRows[^1].Row.Address,
                    "The final 15-byte row must stay at the end instead of appearing at the beginning.");

                main.ViewModel.LoadImage(MemoryImage.FromPatternRange(0x1000, 0x1FFF, [0xA5]), null, null);
                Pump(main.Dispatcher);
                Assert.AreEqual(0, view.FirstVisibleRowIndex,
                    "Opening a different image starts at its first row.");
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
    public void WindowedEditorScrollbarHasLineButtonsAtBothEnds()
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            Window? window = null;
            try
            {
                var image = MemoryImage.FromPatternRange(0, 99_999_999, [0]);
                var viewModel = new HexImageViewModel(image, 32, showAscii: true);
                var view = new HexViewControl { ItemsSource = viewModel.Rows, UseWindowedScrolling = true, Width = 720, Height = 480 };
                window = new Window { Content = view, Width = 740, Height = 500, ShowInTaskbar = false };
                window.Show();
                window.UpdateLayout();
                Pump(window.Dispatcher);

                var bar = Descendants<ScrollBar>(view).Single(item => item.Orientation == Orientation.Vertical && item.IsVisible);
                bar.Value = bar.Maximum / 2;
                window.UpdateLayout();
                var track = (Track)bar.Template.FindName("PART_Track", bar);
                var thumbBottom = track.Thumb.TranslatePoint(new Point(0, track.Thumb.ActualHeight), bar).Y;
                var pageDownTop = track.IncreaseRepeatButton.TranslatePoint(new Point(), bar).Y;
                Assert.IsGreaterThanOrEqualTo(thumbBottom - 0.5, pageDownTop,
                    $"The Page Down area must not cover the bottom of the visible thumb (thumb={track.Thumb.ActualHeight}, viewport={bar.ViewportSize}, track={track.ActualHeight}).");
                var lineUp = bar.Template.FindName("PART_LineUpButton", bar) as RepeatButton;
                var lineDown = bar.Template.FindName("PART_LineDownButton", bar) as RepeatButton;
                Assert.IsNotNull(lineUp, "The custom scrollbar template must include its top line-up button.");
                Assert.IsNotNull(lineDown, "The custom scrollbar template must include its bottom line-down button.");
                Assert.AreEqual(ScrollBar.LineUpCommand, lineUp.Command);
                Assert.AreEqual(ScrollBar.LineDownCommand, lineDown.Command);
                Assert.AreSame(bar, lineUp.CommandTarget);
                Assert.AreSame(bar, lineDown.CommandTarget);
                Assert.IsGreaterThan(0d, lineUp.ActualHeight);
                Assert.IsGreaterThan(0d, lineDown.ActualHeight);
                Assert.AreEqual(0d, lineUp.TranslatePoint(new Point(0, 0), bar).Y, 1d,
                    "Line Up must remain anchored to the top edge of the scrollbar.");
                Assert.AreEqual(bar.ActualHeight,
                    lineDown.TranslatePoint(new Point(0, lineDown.ActualHeight), bar).Y, 1d,
                    "Line Down must remain anchored to the bottom edge of the scrollbar.");
                bar.Value = 20;
                ((RoutedCommand)lineUp.Command).Execute(null, bar);
                Assert.IsLessThan(20d, bar.Value, "The top button must scroll toward earlier rows.");
                ((RoutedCommand)lineDown.Command).Execute(null, bar);
                Assert.AreEqual(20d, bar.Value, "The bottom button must scroll toward later rows.");

                view.Height = 320;
                window.Height = 340;
                window.UpdateLayout();
                Pump(window.Dispatcher);
                bar.Value = bar.Maximum / 2;
                window.UpdateLayout();
                thumbBottom = track.Thumb.TranslatePoint(new Point(0, track.Thumb.ActualHeight), bar).Y;
                pageDownTop = track.IncreaseRepeatButton.TranslatePoint(new Point(), bar).Y;
                Assert.IsGreaterThanOrEqualTo(thumbBottom - 0.5, pageDownTop,
                    "Resizing the window must keep the full thumb visible.");
            }
            catch (Exception exception) { failure = exception; }
            finally { window?.Close(); }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
        if (failure is not null) System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(failure).Throw();
    }

    private static void RaiseWheel(HexViewControl view, Window window)
    {
        var target = Descendants<ScrollViewer>(view).Single(item => ScrollViewer.GetVerticalScrollBarVisibility(item) == ScrollBarVisibility.Disabled);
        target.RaiseEvent(new MouseWheelEventArgs(Mouse.PrimaryDevice, Environment.TickCount, -120)
        {
            RoutedEvent = UIElement.PreviewMouseWheelEvent,
            Source = target
        });
    }

    private static TimeSpan WaitForRenderedRow(Dispatcher dispatcher, HexViewControl view, Stopwatch stopwatch, string expectedAddress)
    {
        var frame = new DispatcherFrame();
        var elapsed = TimeSpan.Zero;
        EventHandler? rendering = null;
        rendering = (_, _) =>
        {
            CompositionTarget.Rendering -= rendering;
            dispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle, new Action(() =>
            {
                Assert.AreEqual(expectedAddress, view.WindowRows[0].Row.Address,
                    "The expected address must still be present after the rendered frame.");
                Assert.IsTrue(Descendants<HexRowAddressControl>(view).Any(address => address.IsVisible && address.Row?.Address == expectedAddress),
                    "The post-frame visual tree must display the expected address row.");
                elapsed = stopwatch.Elapsed;
                frame.Continue = false;
            }));
        };
        CompositionTarget.Rendering += rendering;
        dispatcher.BeginInvoke(DispatcherPriority.Render, new Action(() => { }));
        Dispatcher.PushFrame(frame);
        return elapsed;
    }

    private static void Pump(Dispatcher dispatcher) => dispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);

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
