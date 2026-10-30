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
