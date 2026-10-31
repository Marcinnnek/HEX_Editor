using HexEditor.Core;
using HexEditor.UI;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;

namespace HexEditor.Tests;

[TestClass]
[DoNotParallelize]
public sealed class EditRowWindowAcceptanceTests
{
    [TestMethod]
    [DataRow(8)]
    [DataRow(16)]
    [DataRow(32)]
    public void Edit_row_presents_compact_centered_inputs_in_hex_then_ascii_tab_order(int bytesPerRow)
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            EditRowWindow? window = null;
            try
            {
                var workspace = new EditorDocumentWorkspace();
                workspace.OpenImage(MemoryImage.FromSegments([
                    new MemorySegment(0x1000, [0x41]), new MemorySegment(0x1003, [0x44])]), null, null);
                window = new EditRowWindow(workspace.BeginEditRow(0x1000, 0x1003, bytesPerRow))
                {
                    Left = -3000, Top = -3000, WindowStartupLocation = WindowStartupLocation.Manual,
                    ShowActivated = false, ShowInTaskbar = false
                };
                window.Show();
                window.Dispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);

                var inputs = FindChildren<TextBox>(window).ToArray();
                var editable = inputs.Where(input => input.IsEnabled).ToArray();
                Assert.HasCount(4, editable);
                var tabOrder = editable.OrderBy(KeyboardNavigation.GetTabIndex).ToArray();
                CollectionAssert.AreEqual(new[] { 0, 1, 2, 3 }, tabOrder.Select(KeyboardNavigation.GetTabIndex).ToArray());
                Assert.IsTrue(editable.All(input => input.IsTabStop));
                Assert.IsTrue(inputs.Where(input => !input.IsEnabled).All(input => !input.IsTabStop));
                Assert.IsTrue(inputs.All(input => input.HorizontalContentAlignment == HorizontalAlignment.Center));
                Assert.IsFalse(FindChildren<TextBlock>(window).Any(text => text.Text.Contains("Selected byte")));
                Assert.AreEqual(ResizeMode.NoResize, window.ResizeMode);
                Assert.IsTrue(window.ActualHeight < SystemParameters.WorkArea.Height);
                Assert.IsTrue(tabOrder[1].IsKeyboardFocused);
                Assert.AreEqual(tabOrder[1].Text.Length, tabOrder[1].SelectionLength);
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
    public void Wide_sparse_edit_row_uses_groups_of_at_most_32_and_skips_gaps_in_tab_order()
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            EditRowWindow? window = null;
            try
            {
                const int width = 80;
                var workspace = new EditorDocumentWorkspace();
                workspace.OpenImage(MemoryImage.FromSegments([
                    new MemorySegment(0x1000, [0x41]), new MemorySegment(0x104F, [0x5A])]), null, null);
                window = new EditRowWindow(workspace.BeginEditRow(0x1000, 0x104F, width))
                {
                    Left = -3000, Top = -3000, WindowStartupLocation = WindowStartupLocation.Manual,
                    ShowActivated = false, ShowInTaskbar = false
                };
                window.Show();
                window.Dispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);

                var inputs = FindChildren<TextBox>(window).Where(input => input.IsEnabled).OrderBy(KeyboardNavigation.GetTabIndex).ToArray();
                Assert.HasCount(4, inputs);
                CollectionAssert.AreEqual(new[] { "41", "A", "5A", "Z" }, inputs.Select(input => input.Text).ToArray(),
                    $"Unexpected grouped Tab order: {string.Join(",", inputs.Select(input => $"{input.TabIndex}:{input.Text}"))}");
                Assert.AreEqual(3, FindChildren<TextBlock>(window).Count(text => text.Text == "Original HEX"));
                Assert.AreEqual(3, FindChildren<TextBlock>(window).Count(text => text.Text == "Proposed ASCII"));
                Assert.IsTrue(FindChildren<ScrollViewer>(window).Any(scroll => scroll.VerticalScrollBarVisibility == ScrollBarVisibility.Auto));
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
    public void Full_128_byte_edit_row_has_four_groups_and_tab_visits_hex_then_ascii_per_group()
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            EditRowWindow? window = null;
            try
            {
                const int width = 128;
                var workspace = new EditorDocumentWorkspace();
                var bytes = Enumerable.Range(0, width).Select(value => (byte)value).ToArray();
                workspace.OpenImage(MemoryImage.FromSegments([new MemorySegment(0x1000, bytes)]), null, null);
                window = new EditRowWindow(workspace.BeginEditRow(0x1000, 0x1000, width))
                {
                    Left = -3000, Top = -3000, WindowStartupLocation = WindowStartupLocation.Manual,
                    ShowActivated = false, ShowInTaskbar = false
                };
                window.Show();
                window.Dispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);

                var inputs = FindChildren<TextBox>(window).Where(input => input.IsEnabled).OrderBy(KeyboardNavigation.GetTabIndex).ToArray();
                Assert.HasCount(256, inputs);
                Assert.AreEqual(4, FindChildren<TextBlock>(window).Count(text => text.Text == "Original HEX"));
                for (var group = 0; group < 4; group++)
                {
                    var start = group * 32;
                    var values = inputs.Skip(group * 64).Take(64).Select(input => input.Text).ToArray();
                    CollectionAssert.AreEqual(
                        Enumerable.Range(start, 32).Select(value => value.ToString("X2"))
                            .Concat(Enumerable.Range(start, 32).Select(value => value is >= 0x20 and <= 0x7E ? ((char)value).ToString() : "."))
                            .ToArray(),
                        values);
                }
                Assert.IsTrue(window.ActualHeight <= SystemParameters.WorkArea.Height);
                Assert.IsTrue(FindChildren<ScrollViewer>(window).Any(scroll => scroll.VerticalScrollBarVisibility == ScrollBarVisibility.Auto));
            }
            catch (Exception exception) { failure = exception; }
            finally { window?.Close(); }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
        if (failure is not null) System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(failure).Throw();
    }

    private static IEnumerable<T> FindChildren<T>(DependencyObject parent) where T : DependencyObject
    {
        for (var index = 0; index < System.Windows.Media.VisualTreeHelper.GetChildrenCount(parent); index++)
        {
            var child = System.Windows.Media.VisualTreeHelper.GetChild(parent, index);
            if (child is T match) yield return match;
            foreach (var descendant in FindChildren<T>(child)) yield return descendant;
        }
    }
}
