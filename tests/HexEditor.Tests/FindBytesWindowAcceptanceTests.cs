using HexEditor.UI;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using HexEditor.Core;

namespace HexEditor.Tests;

[TestClass]
[DoNotParallelize]
public sealed class FindBytesWindowAcceptanceTests
{
    [TestMethod]
    public void Find_window_is_nonmodal_and_preserves_each_mode_draft()
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            FindBytesWindow? window = null;
            try
            {
                window = new FindBytesWindow
                {
                    Left = -3000, Top = -3000, WindowStartupLocation = WindowStartupLocation.Manual,
                    ShowActivated = false, ShowInTaskbar = false
                };
                window.Show();
                window.Dispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);

                window.SetDraft(FindPatternMode.Hex, "DE AD");
                window.ModeSelector.SelectedIndex = 1;
                window.Dispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);
                window.PatternInput.Text = "Case";
                window.ModeSelector.SelectedIndex = 0;
                window.Dispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);
                window.PatternInput.Text = "BEEF";
                window.ModeSelector.SelectedIndex = 1;
                window.Dispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);

                Assert.IsTrue(window.IsVisible);
                Assert.AreEqual("BEEF", window.HexDraft);
                Assert.AreEqual("Case", window.AsciiDraft);
                Assert.AreEqual("Case", window.PatternInput.Text);
                Assert.AreEqual(ResizeMode.NoResize, window.ResizeMode);
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
    public void View_menu_opens_one_find_window_and_f3_repeats_after_close()
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            var main = new MainWindow();
            FindBytesWindow? find = null;
            try
            {
                main.ViewModel.LoadImage(MemoryImage.FromSegments([
                    new MemorySegment(0x5000, [0xDE, 0xAD, 0xDE, 0xAD])]), null, null);
                main.Show();
                main.UpdateLayout();
                var findMenu = LogicalDescendants<MenuItem>(main).Single(item => item.Header?.ToString() == "Find bytes...");
                findMenu.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
                main.Dispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);
                find = main.OwnedWindows.OfType<FindBytesWindow>().Single();
                Assert.IsTrue(main.IsEnabled, "Opening Find leaves the Editor interactive.");

                findMenu.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
                main.Dispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);
                Assert.AreEqual(1, main.OwnedWindows.OfType<FindBytesWindow>().Count());

                find.PatternInput.Text = "DEAD";
                var next = Descendants<Button>(find).Single(button => button.Content?.ToString() == "Find Next");
                next.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
                WaitForSelection(main, 0x5000);
                find.Close();
                find = null;

                var source = PresentationSource.FromVisual(main);
                main.RaiseEvent(new KeyEventArgs(Keyboard.PrimaryDevice, source, Environment.TickCount, Key.F3)
                {
                    RoutedEvent = Keyboard.PreviewKeyDownEvent
                });
                WaitForSelection(main, 0x5002);
            }
            catch (Exception exception) { failure = exception; }
            finally { find?.Close(); main.Close(); }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
        if (failure is not null) System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(failure).Throw();
    }

    [TestMethod]
    public void Editor_find_places_match_at_top_when_possible_and_keeps_full_match_visible()
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            var main = new MainWindow();
            FindBytesWindow? find = null;
            try
            {
                const int bytesPerRow = 32;
                const int rowCount = 80;
                const int matchRow = 40;
                var bytes = new byte[rowCount * bytesPerRow];
                var matchOffset = matchRow * bytesPerRow + bytesPerRow - 2;
                bytes[matchOffset] = 0xDE;
                bytes[matchOffset + 1] = 0xAD;
                bytes[matchOffset + 2] = 0xBE;
                bytes[matchOffset + 3] = 0xEF;
                var finalMatchOffset = bytes.Length - 4;
                bytes[finalMatchOffset] = 0xDE;
                bytes[finalMatchOffset + 1] = 0xAD;
                bytes[finalMatchOffset + 2] = 0xBE;
                bytes[finalMatchOffset + 3] = 0xEF;
                main.ViewModel.LoadImage(MemoryImage.FromSegments([new MemorySegment(0x1000, bytes)]), null, null);
                main.ViewModel.ImageView.BytesPerRow = bytesPerRow;
                main.Width = 640;
                main.Show();
                main.UpdateLayout();

                var rowsView = (HexViewControl)main.FindName("HexRowsView");
                var scroll = Descendants<ScrollViewer>(rowsView).Single(viewer => ScrollViewer.GetCanContentScroll(viewer));
                Assert.IsGreaterThan(0d, scroll.ScrollableWidth, "The wide row must make a horizontal viewport available.");
                scroll.ScrollToHorizontalOffset(scroll.ScrollableWidth);
                rowsView.UpdateLayout();
                main.Dispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);
                Assert.IsGreaterThan(0d, scroll.HorizontalOffset, "The setup must move away from the first HEX byte.");

                var findMenu = LogicalDescendants<MenuItem>(main).Single(item => item.Header?.ToString() == "Find bytes...");
                findMenu.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
                main.Dispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);
                find = main.OwnedWindows.OfType<FindBytesWindow>().Single();
                find.PatternInput.Text = "DE AD BE EF";
                Descendants<Button>(find).Single(button => button.Content?.ToString() == "Find Next")
                    .RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
                WaitForSelection(main, (uint)(0x1000 + matchOffset));
                main.Dispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);
                rowsView.UpdateLayout();

                Assert.AreEqual(0d, scroll.HorizontalOffset,
                    "Find must return horizontal scrolling to the left edge where the first HEX byte follows Address.");
                var visibleRows = Enumerable.Range(0, rowsView.ItemsGrid.Items.Count)
                    .Select(index => (Index: index, Row: rowsView.ItemsGrid.ItemContainerGenerator.ContainerFromIndex(index) as DataGridRow))
                    .Where(item => item.Row is { IsVisible: true, ActualHeight: > 0 } &&
                        RowIntersectsViewport(item.Row, scroll))
                    .Select(item => item.Index)
                    .ToArray();
                Assert.IsNotEmpty(visibleRows, "The Editor must render rows after Find.");
                Assert.AreEqual(matchRow, visibleRows[0],
                    $"The first match row should lead the viewport when there is room below it (offset {scroll.VerticalOffset}, viewport {scroll.ViewportHeight}).");

                var matchedCells = main.ViewModel.ImageView.Rows
                    .SelectMany(row => row.Cells)
                    .Where(cell => cell.Address >= 0x1000 + matchOffset && cell.Address <= 0x1000 + matchOffset + 3)
                    .ToArray();
                Assert.HasCount(4, matchedCells);
                Assert.IsTrue(matchedCells.All(cell => cell.IsFindMatch), "Every matched byte, including the next row, stays highlighted.");
                Assert.IsTrue(visibleRows.Contains(matchRow + 1), "The following row containing the rest of the match must also be visible.");

                var next = Descendants<Button>(find).Single(button => button.Content?.ToString() == "Find Next");
                next.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
                var finalAddress = (uint)(0x1000 + finalMatchOffset);
                WaitForSelection(main, finalAddress);
                AssertEditorMatchVisible(rowsView, scroll, rowCount - 1, finalAddress);

                next.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
                WaitForSelection(main, (uint)(0x1000 + matchOffset));
                AssertEditorMatchVisible(rowsView, scroll, matchRow, (uint)(0x1000 + matchOffset));

                var previous = Descendants<Button>(find).Single(button => button.Content?.ToString() == "Find Previous");
                previous.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
                WaitForSelection(main, finalAddress);
                AssertEditorMatchVisible(rowsView, scroll, rowCount - 1, finalAddress);

                find.Close();
                find = null;
                var source = PresentationSource.FromVisual(main);
                main.RaiseEvent(new KeyEventArgs(Keyboard.PrimaryDevice, source, Environment.TickCount, Key.F3)
                {
                    RoutedEvent = Keyboard.PreviewKeyDownEvent
                });
                WaitForSelection(main, (uint)(0x1000 + matchOffset));
                AssertEditorMatchVisible(rowsView, scroll, matchRow, (uint)(0x1000 + matchOffset));
            }
            catch (Exception exception) { failure = exception; }
            finally { find?.Close(); main.Close(); }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
        if (failure is not null) System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(failure).Throw();
    }

    [TestMethod]
    public void Compare_find_starts_after_selected_panel_and_f3_continues_after_close()
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            var main = new MainWindow();
            FindBytesWindow? find = null;
            try
            {
                main.Show();
                main.UpdateLayout();
                var tabs = Descendants<TabControl>(main).Single();
                var imageA = MemoryImage.FromSegments([new MemorySegment(0x1000, [0xAA, 0xAA, 0xAA])]);
                var imageB = MemoryImage.FromSegments([new MemorySegment(0x1000, [0xAA, 0xAA, 0xAA])]);
                var workspace = new CompareWorkspaceTabViewModel(imageA, imageB, "A", "B");
                var compare = new CompareView(workspace);
                tabs.Items.Add(new TabItem { Header = "Compare", Content = compare });
                tabs.SelectedIndex = tabs.Items.Count - 1;
                main.Dispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);
                Assert.IsTrue(compare.NavigateToAddress(0x1001));

                var findMenu = LogicalDescendants<MenuItem>(main).Single(item => item.Header?.ToString() == "Find bytes...");
                findMenu.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
                main.Dispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);
                find = main.OwnedWindows.OfType<FindBytesWindow>().Single();
                find.PatternInput.Text = "AA";
                Descendants<Button>(find).Single(button => button.Content?.ToString() == "Find Next")
                    .RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
                WaitForCompareMatch(compare, fileA: false, 0x1001);
                find.Close();
                find = null;

                var source = PresentationSource.FromVisual(main);
                main.RaiseEvent(new KeyEventArgs(Keyboard.PrimaryDevice, source, Environment.TickCount, Key.F3)
                {
                    RoutedEvent = Keyboard.PreviewKeyDownEvent
                });
                WaitForCompareMatch(compare, fileA: true, 0x1002);
            }
            catch (Exception exception) { failure = exception; }
            finally { find?.Close(); main.Close(); }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
        if (failure is not null) System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(failure).Throw();
    }

    [TestMethod]
    public void Compare_find_navigates_full_range_final_match_and_orders_base_before_reference()
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            var main = new MainWindow();
            FindBytesWindow? find = null;
            try
            {
                main.Show();
                main.UpdateLayout();
                var tabs = Descendants<TabControl>(main).Single();
                var imageA = MemoryImage.FromSegments([
                    new MemorySegment(0, [0xAA, 0xAA]),
                    new MemorySegment(uint.MaxValue - 1, [0xAA, 0xAA])]);
                var imageB = MemoryImage.FromSegments([
                    new MemorySegment(0, [0xAA, 0xAA]),
                    new MemorySegment(uint.MaxValue - 1, [0xAA, 0xAA])]);
                var compare = new CompareView(new CompareWorkspaceTabViewModel(imageA, imageB, "A", "B"));
                tabs.Items.Add(new TabItem { Header = "Full range", Content = compare });
                tabs.SelectedIndex = tabs.Items.Count - 1;
                main.Dispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);
                Assert.IsTrue(compare.NavigateToAddress(uint.MaxValue));

                var findMenu = LogicalDescendants<MenuItem>(main).Single(item => item.Header?.ToString() == "Find bytes...");
                findMenu.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
                main.Dispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);
                find = main.OwnedWindows.OfType<FindBytesWindow>().Single();
                find.PatternInput.Text = "AAAA";
                Descendants<Button>(find).Single(button => button.Content?.ToString() == "Find Previous")
                    .RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
                WaitForCompareMatch(compare, fileA: true, uint.MaxValue - 1);
                WaitForCompareAddress(compare, uint.MaxValue - 1);

                var previous = Descendants<Button>(find).Single(button => button.Content?.ToString() == "Find Previous");
                previous.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
                WaitForCompareAddress(compare, 0);
                previous.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
                WaitForCompareAddress(compare, uint.MaxValue - 1);
            }
            catch (Exception exception) { failure = exception; }
            finally { find?.Close(); main.Close(); }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
        if (failure is not null) System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(failure).Throw();
    }

    private static void WaitForSelection(MainWindow window, uint address)
    {
        var deadline = DateTime.UtcNow.AddSeconds(5);
        while (window.ViewModel.EditorStatus.CurrentAddress != address && DateTime.UtcNow < deadline)
        {
            window.Dispatcher.Invoke(() => { }, DispatcherPriority.Background);
            Thread.Sleep(10);
        }
        Assert.AreEqual(address, window.ViewModel.EditorStatus.CurrentAddress);
    }

    private static bool RowIntersectsViewport(DataGridRow row, ScrollViewer scroll)
    {
        var bounds = row.TransformToAncestor(scroll).TransformBounds(new Rect(row.RenderSize));
        return bounds.Bottom > 0 && bounds.Top < scroll.ActualHeight;
    }

    private static void AssertEditorMatchVisible(HexViewControl rowsView, ScrollViewer scroll, int matchRow, uint matchStart)
    {
        rowsView.UpdateLayout();
        rowsView.Dispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);
        Assert.AreEqual(0d, scroll.HorizontalOffset, "Editor Find keeps the first HEX byte at the left edge.");
        var visibleRows = Enumerable.Range(0, rowsView.ItemsGrid.Items.Count)
            .Select(index => (Index: index, Row: rowsView.ItemsGrid.ItemContainerGenerator.ContainerFromIndex(index) as DataGridRow))
            .Where(item => item.Row is { IsVisible: true, ActualHeight: > 0 } && RowIntersectsViewport(item.Row, scroll))
            .Select(item => item.Index)
            .ToArray();
        Assert.IsTrue(visibleRows.Contains(matchRow), $"The result row {matchRow} must be in the rendered viewport.");
        var matchedCells = rowsView.ItemsGrid.Items.Cast<HexViewRow>()
            .SelectMany(row => row.Cells)
            .Where(cell => cell.Address >= matchStart && cell.Address <= matchStart + 3)
            .ToArray();
        Assert.HasCount(4, matchedCells);
        Assert.IsTrue(matchedCells.All(cell => cell.IsFindMatch), "The complete current sequence must stay highlighted.");
    }

    private static void WaitForCompareMatch(CompareView view, bool fileA, uint address)
    {
        var deadline = DateTime.UtcNow.AddSeconds(5);
        while (!view.ViewModel.FileARows.Concat(view.ViewModel.FileBRows).Any(row =>
                   row.Cells.Any(cell => cell.Address == address && cell.IsFindMatch)) && DateTime.UtcNow < deadline)
        {
            view.Dispatcher.Invoke(() => { }, DispatcherPriority.Background);
            Thread.Sleep(10);
        }
        var rows = fileA ? view.ViewModel.FileARows : view.ViewModel.FileBRows;
        Assert.IsTrue(rows.Any(row => row.Cells.Any(cell => cell.Address == address && cell.IsFindMatch)));
    }

    private static void WaitForCompareAddress(CompareView view, uint address)
    {
        var deadline = DateTime.UtcNow.AddSeconds(5);
        while (!Descendants<TextBox>(view).Any(box => box.Text == address.ToString("X8")) && DateTime.UtcNow < deadline)
        {
            view.Dispatcher.Invoke(() => { }, DispatcherPriority.Background);
            Thread.Sleep(10);
        }
        Assert.IsTrue(Descendants<TextBox>(view).Any(box => box.Text == address.ToString("X8")));
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

    private static IEnumerable<T> LogicalDescendants<T>(DependencyObject parent) where T : DependencyObject
    {
        foreach (var child in LogicalTreeHelper.GetChildren(parent).OfType<DependencyObject>())
        {
            if (child is T match) yield return match;
            foreach (var descendant in LogicalDescendants<T>(child)) yield return descendant;
        }
    }
}
