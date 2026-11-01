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
    public TestContext TestContext { get; set; } = null!;

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

                var arrowBar = Descendants<ScrollBar>(left).Single(item => item.Orientation == Orientation.Vertical && item.IsVisible);
                var arrowStart = left.FirstVisibleRowIndex;
                ScrollBar.LineDownCommand.Execute(null, arrowBar);
                Pump(window.Dispatcher);
                Assert.AreEqual(arrowStart + 1, left.FirstVisibleRowIndex);
                Assert.AreEqual(left.FirstVisibleRowIndex, right.FirstVisibleRowIndex);
                ScrollBar.LineUpCommand.Execute(null, arrowBar);
                Pump(window.Dispatcher);
                Assert.AreEqual(arrowStart, left.FirstVisibleRowIndex);
                Assert.AreEqual(left.FirstVisibleRowIndex, right.FirstVisibleRowIndex);

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

    [TestMethod]
    public void DraggingSixtyFiveKilobyteCompareToEndKeepsUpWithTheThumb()
    {
        Exception? failure = null;
        var elapsed = TimeSpan.Zero;
        var eventMilliseconds = 0d;
        var layoutMilliseconds = 0d;
        var steps = 0;
        var refreshes = 0;
        var thread = new Thread(() =>
        {
            Window? window = null;
            try
            {
                var a = MemoryImage.FromPatternRange(0, 65_535, [0x12, 0xA7]);
                var b = MemoryImage.FromPatternRange(0, 65_535, [0x93, 0x06]);
                var compare = new CompareView(new CompareWorkspaceTabViewModel(a, b, "base.hex", "reference.hex"));
                window = new Window { Content = compare, Width = 1280, Height = 800, ShowInTaskbar = false };
                window.Show();
                window.UpdateLayout();
                Pump(window.Dispatcher);

                var left = (HexViewControl)compare.FindName("FileAView");
                var right = (HexViewControl)compare.FindName("FileBView");
                left.FirstVisibleRowChanged += (_, _) => refreshes++;
                var bar = Descendants<ScrollBar>(left).Single(item => item.Orientation == Orientation.Vertical && item.IsVisible);
                var thumb = Descendants<Thumb>(bar).Single();
                thumb.RaiseEvent(new DragStartedEventArgs(0, 0) { RoutedEvent = Thumb.DragStartedEvent, Source = thumb });

                var started = Stopwatch.StartNew();
                for (var step = 0; step < 80 && bar.Value < bar.Maximum; step++)
                {
                    var eventStarted = Stopwatch.GetTimestamp();
                    thumb.RaiseEvent(new DragDeltaEventArgs(0, 10) { RoutedEvent = Thumb.DragDeltaEvent, Source = thumb });
                    eventMilliseconds += Stopwatch.GetElapsedTime(eventStarted).TotalMilliseconds;
                    var layoutStarted = Stopwatch.GetTimestamp();
                    window.UpdateLayout();
                    Pump(window.Dispatcher);
                    layoutMilliseconds += Stopwatch.GetElapsedTime(layoutStarted).TotalMilliseconds;
                    steps++;
                }
                thumb.RaiseEvent(new DragCompletedEventArgs(0, 0, false)
                    { RoutedEvent = Thumb.DragCompletedEvent, Source = thumb });
                window.UpdateLayout();
                Pump(window.Dispatcher);
                elapsed = started.Elapsed;
                Assert.AreEqual((int)bar.Maximum, left.FirstVisibleRowIndex, "The drag should reach the final row window.");
                Assert.AreEqual(left.FirstVisibleRowIndex, right.FirstVisibleRowIndex);
                Assert.AreEqual(left.WindowRows[0].Row.Address, right.WindowRows[0].Row.Address);
            }
            catch (Exception exception) { failure = exception; }
            finally { window?.Close(); }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
        if (failure is not null) System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(failure).Throw();
        TestContext.WriteLine($"65 KB drag: {elapsed.TotalMilliseconds:F1} ms, {steps} events, {refreshes} viewport updates; " +
            $"event handling {eventMilliseconds:F1} ms, layout {layoutMilliseconds:F1} ms.");
        Assert.IsLessThan(TimeSpan.FromMilliseconds(500), elapsed,
            $"Dragging both 65 KB panels to the end took {elapsed.TotalMilliseconds:F1} ms " +
            $"in {steps} steps and {refreshes} refreshes " +
            $"(events {eventMilliseconds:F1} ms, layout {layoutMilliseconds:F1} ms).");
    }

    [TestMethod]
    public void ClickingVisibleCompareBytesKeepsBothViewportsAndTheSelectedAddress()
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            Window? window = null;
            try
            {
                var baseImage = MemoryImage.FromPatternRange(0, 999_999, [0x12]);
                var referenceImage = MemoryImage.FromPatternRange(0, 999_999, [0x34]);
                var compare = new CompareView(new CompareWorkspaceTabViewModel(
                    baseImage, referenceImage, "base.hex", "reference.hex"));
                window = new Window { Content = compare, Width = 1280, Height = 800, ShowInTaskbar = false };
                window.Show();
                window.UpdateLayout();
                Pump(window.Dispatcher);

                var left = (HexViewControl)compare.FindName("FileAView");
                var right = (HexViewControl)compare.FindName("FileBView");
                Assert.IsTrue(left.UseWindowedScrolling);
                left.ScrollToRow(50);
                Pump(window.Dispatcher);
                Assert.AreEqual(50, right.FirstVisibleRowIndex);

                var address = 54u * (uint)compare.ViewModel.BytesPerRow;
                foreach (var (panel, side) in new[]
                {
                    (left, ComparePanelSide.Base),
                    (right, ComparePanelSide.Reference)
                })
                {
                    var row = Descendants<HexByteRowControl>(panel)
                        .Single(item => !item.IsAscii && item.Row?.Address == address.ToString("X8"));
                    Assert.IsTrue(panel.SelectByteAt(row, new Point(3, 3), extendSelection: false));
                    Assert.AreEqual(50, left.FirstVisibleRowIndex);
                    Assert.AreEqual(50, right.FirstVisibleRowIndex);
                    Assert.AreEqual(side, compare.CurrentFindCursor?.Side);
                    Assert.AreEqual(address, compare.CurrentFindCursor?.Address);
                }

                var next = Descendants<Wpf.Ui.Controls.Button>(compare)
                    .Single(button => Equals(button.Content, "Next difference"));
                next.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
                var addressBox = (Wpf.Ui.Controls.TextBox)compare.FindName("AddressBox");
                Assert.AreEqual((address + 1).ToString("X8"), addressBox.Text);
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
    public void ByteSelectionBelongsOnlyToTheClickedComparePanel()
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            Window? window = null;
            try
            {
                var baseImage = MemoryImage.FromSegments([
                    new MemorySegment(0x1000, [0x12, 0x13]),
                    new MemorySegment(0x1010, [0x36, 0x37])]);
                var referenceImage = MemoryImage.FromSegments([
                    new MemorySegment(0x1000, [0x34, 0x35]),
                    new MemorySegment(0x1010, [0x38, 0x39])]);
                var compare = new CompareView(new CompareWorkspaceTabViewModel(baseImage, referenceImage, "base.hex", "reference.hex"));
                window = new Window { Content = compare, Width = 1280, Height = 800, ShowInTaskbar = false };
                window.Show();
                window.UpdateLayout();
                var left = (HexViewControl)compare.FindName("FileAView");
                var right = (HexViewControl)compare.FindName("FileBView");
                var leftHex = Descendants<HexByteRowControl>(left).Single(item => item.IsVisible && !item.IsAscii && item.Row?.Address == "00001000");
                var rightHex = Descendants<HexByteRowControl>(right).Single(item => item.IsVisible && !item.IsAscii && item.Row?.Address == "00001000");

                Assert.IsTrue(right.SelectByteAt(rightHex, new Point(3, 3), extendSelection: false));
                Assert.AreEqual(0x1000u, rightHex.SelectionStart);
                Assert.IsTrue(left.SelectByteAt(leftHex, new Point(3, 3), extendSelection: false));
                Assert.AreEqual(0x1000u, leftHex.SelectionStart);
                Assert.IsNull(rightHex.SelectionStart, "Selecting panel A clears panel B's previous byte outline.");

                var leftLastRow = Descendants<HexByteRowControl>(left)
                    .Single(item => item.IsVisible && !item.IsAscii && item.Row?.Address == "00001010");
                Assert.IsTrue(left.SelectByteAt(leftHex, new Point(3, 3), extendSelection: false));
                Assert.IsTrue(left.SelectByteAt(leftLastRow, new Point(3, 3), extendSelection: true));
                Assert.AreEqual(0x1000u, leftHex.SelectionStart);
                Assert.AreEqual(0x1010u, leftLastRow.SelectionEnd);
                Assert.IsNull(right.SelectionStart, "A Shift range remains local to the clicked Compare panel.");
                Assert.IsTrue(leftHex.Row!.Cells.Where(cell => cell.Address is >= 0x1002 and < 0x1010)
                    .All(cell => cell.Hex.Length == 0), "Unoccupied addresses do not acquire selected byte cells.");
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
    public void DoubleClickingWindowedCompareByteActivatesBaseRowOnly()
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            Window? window = null;
            try
            {
                var baseImage = MemoryImage.FromPatternRange(0, 999_999, [0x12]);
                var referenceImage = MemoryImage.FromPatternRange(0, 999_999, [0x34]);
                var workspace = new CompareWorkspaceTabViewModel(
                    baseImage, referenceImage, "base.hex", "reference.hex");
                var compare = new CompareView(workspace);
                var activated = new List<(uint Row, uint Byte)>();
                var statuses = new List<string>();
                var raisedDoubleClicks = 0;
                compare.BaseRowActivated += (row, address) => activated.Add((row, address));
                compare.StatusRequested += statuses.Add;
                window = new Window { Content = compare, Width = 1280, Height = 800, ShowInTaskbar = false };
                window.Show();
                window.UpdateLayout();
                Pump(window.Dispatcher);

                var left = (HexViewControl)compare.FindName("FileAView");
                var right = (HexViewControl)compare.FindName("FileBView");
                left.WindowedByteDoubleClicked += (_, _) => raisedDoubleClicks++;
                Assert.IsTrue(left.UseWindowedScrolling);
                left.ScrollToRow(50);
                Pump(window.Dispatcher);
                var rowAddress = 54u * (uint)compare.ViewModel.BytesPerRow;

                foreach (var ascii in new[] { false, true })
                {
                    var row = Descendants<HexByteRowControl>(left)
                        .Single(item => item.IsAscii == ascii && item.Row?.Address == rowAddress.ToString("X8"));
                    var bytePoint = ascii ? new Point(9, 3) : new Point(3, 3);
                    Assert.IsTrue(row.TryGetAddressAt(bytePoint, out var hitAddress));
                    Assert.AreEqual(rowAddress, hitAddress);
                    Assert.AreEqual(CompareRowActivationKind.EditableRow,
                        workspace.ActivateRow(ComparePanelSide.Base, 54, hitAddress).Kind);
                    var click = DoubleClick(row);
                    left.RaiseWindowedByteDoubleClick(row, click, bytePoint);
                    Assert.AreEqual(ascii ? 2 : 1, raisedDoubleClicks);
                    Assert.HasCount(ascii ? 2 : 1, activated,
                        "A double-click in the windowed base panel must activate Edit Row.");
                    Assert.AreEqual((rowAddress, rowAddress), activated[^1]);
                    Assert.IsTrue(click.Handled);
                    Assert.AreEqual(50, left.FirstVisibleRowIndex);
                    Assert.AreEqual(50, right.FirstVisibleRowIndex);
                }

                var referenceRow = Descendants<HexByteRowControl>(right)
                    .Single(item => !item.IsAscii && item.Row?.Address == rowAddress.ToString("X8"));
                right.RaiseWindowedByteDoubleClick(referenceRow, DoubleClick(referenceRow), new Point(3, 3));
                Assert.HasCount(2, activated);
                CollectionAssert.Contains(statuses, "The reference image is read-only.");
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
    public void EditingVisibleCompareByteRefreshesItsHighlightWithoutScrolling()
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            Window? window = null;
            try
            {
                var baseImage = MemoryImage.FromPatternRange(0, 999_999, [0x12]);
                var referenceImage = MemoryImage.FromPatternRange(0, 999_999, [0x34]);
                var owner = new MainViewModel();
                owner.LoadImage(baseImage, null, null);
                var compare = new CompareView(new CompareWorkspaceTabViewModel(
                    baseImage, referenceImage, "base.hex", "reference.hex",
                    baseDocument: owner.Workspace.Document));
                window = new Window { Content = compare, Width = 1280, Height = 800, ShowInTaskbar = false };
                window.Show();
                window.UpdateLayout();
                Pump(window.Dispatcher);

                var left = (HexViewControl)compare.FindName("FileAView");
                left.ScrollToRow(50);
                Pump(window.Dispatcher);
                var address = 54u * (uint)compare.ViewModel.BytesPerRow;
                var rowControls = Descendants<HexByteRowControl>(left)
                    .Where(item => item.Row?.Address == address.ToString("X8")).ToArray();
                Assert.HasCount(2, rowControls);
                Assert.IsTrue(rowControls.All(item => item.Row!.Cells[0].IsEdited == false));

                var edit = owner.Workspace.BeginEditRow(owner.Workspace.Document,
                    address, address, compare.ViewModel.BytesPerRow);
                Assert.IsTrue(edit.SetHex(address, "13"));
                Assert.IsTrue(edit.Commit());
                Pump(window.Dispatcher);

                Assert.IsTrue(owner.Workspace.Document.IsEditedByte(address));
                Assert.IsTrue(compare.ViewModel.FileARows[54].Cells[0].IsEdited,
                    "The Compare model must have the new edit marker before its visible row is checked.");
                Assert.AreEqual(50, left.FirstVisibleRowIndex);
                foreach (var row in rowControls)
                    Assert.IsTrue(row.Row!.Cells[0].IsEdited,
                        "The edited byte must be highlighted in both visible HEX and ASCII rows immediately.");

                Assert.IsTrue(owner.Workspace.Document.Editing.Undo());
                Pump(window.Dispatcher);
                Assert.AreEqual(50, left.FirstVisibleRowIndex);
                Assert.IsTrue(rowControls.All(item => !item.Row!.Cells[0].IsEdited),
                    "Undo must clear the visible edit marker without scrolling.");
            }
            catch (Exception exception) { failure = exception; }
            finally { window?.Close(); }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
        if (failure is not null) System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(failure).Throw();
    }

    private static MouseButtonEventArgs DoubleClick(HexByteRowControl row)
    {
        var click = new MouseButtonEventArgs(Mouse.PrimaryDevice, Environment.TickCount, MouseButton.Left)
        {
            RoutedEvent = UIElement.MouseLeftButtonDownEvent,
            Source = row
        };
        typeof(MouseButtonEventArgs).GetProperty(nameof(MouseButtonEventArgs.ClickCount))!.SetValue(click, 2);
        return click;
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
