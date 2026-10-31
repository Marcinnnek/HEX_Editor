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
public sealed class LiveCompareScrollingAcceptanceTests
{
    [TestMethod]
    public void ComparePanelsScrollLiveAndStayAlignedForHundredMillionByteImages()
    {
        Exception? failure = null;
        var elapsed = TimeSpan.Zero;
        var thread = new Thread(() =>
        {
            Window? window = null;
            try
            {
                const int imageSize = 100_000_000;
                var a = MemoryImage.FromPatternRange(0, imageSize - 1, [0x12, 0xA7, 0x00, 0xFF, 0x4C]);
                var b = MemoryImage.FromPatternRange(0, imageSize - 1, [0x93, 0x06, 0xE1, 0x58, 0x2D]);
                var workspace = new CompareWorkspaceTabViewModel(a, b, "base.hex", "reference.hex");
                var compare = new CompareView(workspace);
                window = new Window { Content = compare, Width = 1280, Height = 800, ShowInTaskbar = false };
                window.Show();
                window.UpdateLayout();
                Pump(window.Dispatcher);

                var left = (HexViewControl)compare.FindName("FileAView");
                var right = (HexViewControl)compare.FindName("FileBView");
                Assert.AreEqual(0, left.FirstVisibleRowIndex);
                Assert.AreEqual(0, right.FirstVisibleRowIndex);
                foreach (var panel in new[] { left, right })
                {
                    var bar = Descendants<ScrollBar>(panel).Single(item => item.Orientation == Orientation.Vertical && item.IsVisible);
                    Assert.IsGreaterThanOrEqualTo(30d, Descendants<Thumb>(bar).Max(thumb => thumb.ActualHeight));
                }

                var started = Stopwatch.StartNew();
                RaiseWheel(left);
                elapsed = WaitForAlignedRows(window.Dispatcher, left, right, expectedMinimum: 1, started);
                Assert.AreEqual(left.FirstVisibleRowIndex, right.FirstVisibleRowIndex);
                Assert.IsGreaterThan(0, left.FirstVisibleRowIndex);
                Assert.IsLessThan(TimeSpan.FromMilliseconds(100), elapsed,
                    $"Wheel scrolling did not update the aligned viewport within 100 ms ({elapsed.TotalMilliseconds:F1} ms).");

                var previousRow = right.FirstVisibleRowIndex;
                started = Stopwatch.StartNew();
                RaiseWheel(right);
                elapsed = WaitForAlignedRows(window.Dispatcher, right, left, previousRow + 1, started);
                Assert.IsLessThan(TimeSpan.FromMilliseconds(100), elapsed,
                    $"Wheel scrolling in panel B did not update the aligned viewport within 100 ms ({elapsed.TotalMilliseconds:F1} ms).");

                var rightBar = Descendants<ScrollBar>(right).Single(item => item.Orientation == Orientation.Vertical && item.IsVisible);
                var thumb = Descendants<Thumb>(rightBar).First(item => item.ActualHeight >= 30);
                thumb.RaiseEvent(new DragStartedEventArgs(0, 0) { RoutedEvent = Thumb.DragStartedEvent, Source = thumb });
                previousRow = left.FirstVisibleRowIndex;
                started = Stopwatch.StartNew();
                thumb.RaiseEvent(new DragDeltaEventArgs(0, 70) { RoutedEvent = Thumb.DragDeltaEvent, Source = thumb });
                elapsed = WaitForAlignedRows(window.Dispatcher, right, left, previousRow + 1, started);
                Assert.AreEqual(left.FirstVisibleRowIndex, right.FirstVisibleRowIndex,
                    "Dragging either panel's thumb keeps the aligned row mappings synchronized.");
                Assert.IsLessThan(TimeSpan.FromMilliseconds(100), elapsed);

                var leftBar = Descendants<ScrollBar>(left).Single(item => item.Orientation == Orientation.Vertical && item.IsVisible);
                var leftThumb = Descendants<Thumb>(leftBar).First(item => item.ActualHeight >= 30);
                leftThumb.RaiseEvent(new DragStartedEventArgs(0, 0) { RoutedEvent = Thumb.DragStartedEvent, Source = leftThumb });
                previousRow = right.FirstVisibleRowIndex;
                started = Stopwatch.StartNew();
                leftThumb.RaiseEvent(new DragDeltaEventArgs(0, 45) { RoutedEvent = Thumb.DragDeltaEvent, Source = leftThumb });
                elapsed = WaitForAlignedRows(window.Dispatcher, left, right, previousRow + 1, started);
                Assert.IsLessThan(TimeSpan.FromMilliseconds(100), elapsed);

                foreach (var width in new[] { 8, 16, 32 })
                foreach (var showAscii in new[] { true, false })
                {
                    workspace.Comparison.BytesPerRow = width;
                    workspace.Comparison.ShowAscii = showAscii;
                    Pump(window.Dispatcher);
                    left.ScrollToRow(100_000);
                    Pump(window.Dispatcher);
                    Assert.AreEqual(100_000, right.FirstVisibleRowIndex);
                    Assert.AreEqual((100_000L * width).ToString("X8"), left.WindowRows[0].Row.Address);
                    Assert.AreEqual(left.WindowRows[0].Row.Address, right.WindowRows[0].Row.Address);
                }
            }
            catch (Exception exception) { failure = exception; }
            finally { window?.Close(); }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
        if (failure is not null) System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(failure).Throw();
        Assert.IsLessThan(TimeSpan.FromMilliseconds(100), elapsed);
    }

    private static void RaiseWheel(HexViewControl panel)
    {
        var scroll = Descendants<ScrollViewer>(panel).Single(item => ScrollViewer.GetVerticalScrollBarVisibility(item) == ScrollBarVisibility.Disabled);
        scroll.RaiseEvent(new MouseWheelEventArgs(Mouse.PrimaryDevice, Environment.TickCount, -120)
        {
            RoutedEvent = UIElement.PreviewMouseWheelEvent,
            Source = scroll
        });
    }

    private static TimeSpan WaitForAlignedRows(Dispatcher dispatcher, HexViewControl source, HexViewControl target,
        int expectedMinimum, Stopwatch stopwatch)
    {
        var frame = new DispatcherFrame();
        EventHandler? rendering = null;
        rendering = (_, _) =>
        {
            CompositionTarget.Rendering -= rendering;
            dispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle, new Action(() =>
            {
                Assert.IsGreaterThanOrEqualTo(expectedMinimum, source.FirstVisibleRowIndex);
                Assert.AreEqual(source.FirstVisibleRowIndex, target.FirstVisibleRowIndex);
                Assert.AreEqual(source.WindowRows[0].Row.Address, target.WindowRows[0].Row.Address);
                frame.Continue = false;
            }));
        };
        CompositionTarget.Rendering += rendering;
        dispatcher.BeginInvoke(DispatcherPriority.Render, new Action(() => { }));
        Dispatcher.PushFrame(frame);
        return stopwatch.Elapsed;
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
