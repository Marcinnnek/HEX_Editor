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
