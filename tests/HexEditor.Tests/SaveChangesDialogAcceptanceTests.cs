using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using HexEditor.UI;

namespace HexEditor.Tests;

[TestClass]
[DoNotParallelize]
public sealed class SaveChangesDialogAcceptanceTests
{
    [TestMethod]
    [DataRow(AppearanceMode.Light)]
    [DataRow(AppearanceMode.Dark)]
    public void Save_prompt_names_the_image_and_presents_readable_default_actions(AppearanceMode mode)
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            SaveChangesDialog? dialog = null;
            Window? owner = null;
            try
            {
                owner = new Window { Width = 500, Height = 300 };
                ApplicationAppearance.ApplyToWindow(owner, mode);
                owner.Show();
                dialog = new SaveChangesDialog(owner, "Untitled image")
                {
                    Left = -3000, Top = -3000, WindowStartupLocation = WindowStartupLocation.Manual,
                    ShowActivated = false, ShowInTaskbar = false
                };
                Assert.AreEqual(typeof(Window), typeof(SaveChangesDialog).BaseType,
                    "The save prompt must use the standard WPF frame so its themed surface fills the window client area.");
                Assert.AreEqual(WindowStyle.SingleBorderWindow, dialog.WindowStyle);
                ApplicationAppearance.ApplyToWindow(dialog, mode);
                var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(30) };
                timer.Tick += (_, _) =>
                {
                    timer.Stop();
                    dialog.UpdateLayout();
                    var content = (DependencyObject)dialog.Content;
                    var text = FindChildren<TextBlock>(content).ToArray();
                    Assert.IsTrue(text.Any(item => item.Text.Contains("Untitled image", StringComparison.Ordinal)), string.Join(" | ", text.Select(item => item.Text)));
                    Assert.IsTrue(text.Any(item => item.Text.Contains("!", StringComparison.Ordinal)));
                    var buttons = FindChildren<Button>(content).ToArray();
                    CollectionAssert.AreEqual(new[] { "Save", "Don't save", "Cancel" }, buttons.Select(item => item.Content?.ToString()).ToArray());
                    Assert.IsTrue(buttons[0].IsDefault);
                    Assert.IsTrue(buttons[2].IsCancel);
                    Assert.IsTrue(buttons.All(button => button.MinWidth >= 100));
                    Assert.IsTrue(FindChildren<StackPanel>(content).Any(panel =>
                        panel.Orientation == Orientation.Horizontal && buttons.All(button => IsDescendantOf(button, panel))));
                    buttons[2].RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                };
                timer.Start();
                dialog.ShowDialog();
            }
            catch (Exception exception) { failure = exception; }
            finally { dialog?.Close(); owner?.Close(); }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
        if (failure is not null) System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(failure).Throw();
    }

    [TestMethod]
    public void Closing_prompt_with_escape_or_window_x_returns_cancel()
    {
        foreach (var closeByEscape in new[] { true, false })
        {
            var result = ShowAndDismiss(closeByEscape);
            Assert.AreEqual(MessageBoxResult.Cancel, result);
        }
    }

    private static MessageBoxResult ShowAndDismiss(bool escape)
    {
        MessageBoxResult result = MessageBoxResult.None;
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try
            {
                var dialog = new SaveChangesDialog(null, "Untitled image");
                var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(30) };
                timer.Tick += (_, _) =>
                {
                    timer.Stop();
                    if (escape) dialog.RaiseEvent(new KeyEventArgs(Keyboard.PrimaryDevice, Keyboard.PrimaryDevice.ActiveSource, 0, Key.Escape)
                    { RoutedEvent = Keyboard.PreviewKeyDownEvent });
                    else dialog.Close();
                };
                timer.Start();
                dialog.ShowDialog();
                result = dialog.Choice;
            }
            catch (Exception exception) { failure = exception; }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
        if (failure is not null) System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(failure).Throw();
        return result;
    }

    private static IEnumerable<T> FindChildren<T>(DependencyObject parent) where T : DependencyObject
    {
        for (var index = 0; index < VisualTreeHelper.GetChildrenCount(parent); index++)
        {
            var child = VisualTreeHelper.GetChild(parent, index);
            if (child is T match) yield return match;
            foreach (var descendant in FindChildren<T>(child)) yield return descendant;
        }
    }

    private static bool IsDescendantOf(DependencyObject child, DependencyObject ancestor)
    {
        for (var current = VisualTreeHelper.GetParent(child); current is not null; current = VisualTreeHelper.GetParent(current))
            if (ReferenceEquals(current, ancestor)) return true;
        return false;
    }
}
