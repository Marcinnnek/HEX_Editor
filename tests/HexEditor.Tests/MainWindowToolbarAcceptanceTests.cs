using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Input;
using HexEditor.UI;

namespace HexEditor.Tests;

[TestClass]
[DoNotParallelize]
public sealed class MainWindowToolbarAcceptanceTests
{
    [TestMethod]
    public void SingleCommandBarKeepsMenusAndViewControlsAtMinimumWidth()
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            var window = new MainWindow { Width = 640 };
            try
            {
                window.Show();
                window.UpdateLayout();

                var commandBar = (StackPanel)window.FindName("MainCommandBar");
                Assert.AreEqual(2, commandBar.Children.Count);
                Assert.IsInstanceOfType<Menu>(commandBar.Children[0]);
                var viewSettings = (StackPanel)commandBar.Children[1];
                var menu = (Menu)commandBar.Children[0];
                CollectionAssert.AreEqual(new[] { "_File", "_Edit", "_View", "_Tools", "_Preferences..." },
                    menu.Items.Cast<MenuItem>().Select(item => item.Header.ToString()).ToArray());
                Assert.IsNull(window.FindName("PrimaryToolBarTray"));
                var widthBox = (ComboBox)window.FindName("RowWidthBox");
                var ascii = (CheckBox)window.FindName("AsciiCheckBox");
                Assert.IsTrue(widthBox.IsVisible);
                Assert.IsTrue(ascii.IsVisible);
                var rightEdge = viewSettings.TranslatePoint(new Point(viewSettings.ActualWidth, 0), commandBar).X;
                Assert.IsLessThanOrEqualTo(commandBar.ActualWidth, rightEdge,
                    "The view controls after Preferences must remain reachable at the minimum supported window width.");

                var fileItems = ((MenuItem)menu.Items[0]).Items.OfType<MenuItem>().ToArray();
                CollectionAssert.AreEqual(new[]
                {
                    "Open HEX / S-Record...", "Save", "Save As...", "Close Image",
                    "Compare images...", "Merge images...", "Export Image...",
                    "Export Selected Segments...", "Export Address Range..."
                }, fileItems.Select(item => item.Header.ToString()).ToArray());

                var menuItems = LogicalDescendants<MenuItem>(window).ToArray();
                Assert.IsTrue(menuItems.Any(item => item.Header?.ToString() == "Go to address" && item.InputGestureText == "Ctrl+G"),
                    "Go to address remains reachable from View with its keyboard shortcut.");
                foreach (var command in new[]
                {
                    "Open HEX / S-Record...", "Save As...", "Close Image", "Compare images...", "Merge images...",
                    "Undo", "Redo", "Select range", "Edit / Insert", "Fill range", "Copy range", "Cut range",
                    "Paste at start", "Delete range", "Relocate selected segment", "Checksum / Calculate...",
                    "Export Image...", "Export Selected Segments...", "Export Address Range..."
                })
                    Assert.IsTrue(menuItems.Any(item => item.Header?.ToString() == command), $"'{command}' remains available in a menu.");

                Assert.AreEqual(1, menuItems.Count(item => item.Header?.ToString() == "Export Image..."));

                var tabs = (TabControl)window.FindName("WorkspaceTabs");
                tabs.SelectedIndex = 0;
                Assert.IsNotNull(tabs.SelectedContent, "The Editor workspace remains selectable.");
            }
            catch (Exception exception) { failure = exception; }
            finally { window.Close(); }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
        if (failure is not null) throw failure;
    }

    [TestMethod]
    public void EditorRowWidthControlAcceptsCustomDecimalWidthsAndRestoresInvalidInput()
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            var window = new MainWindow();
            try
            {
                window.Show();
                window.UpdateLayout();
                var widthBox = (ComboBox)window.FindName("RowWidthBox");

                Assert.IsTrue(widthBox.IsEditable);
                Assert.AreEqual(16, window.ViewModel.ActiveBytesPerRow);
                widthBox.Text = "24";
                CommitWithEnter(widthBox);
                Assert.AreEqual(24, window.ViewModel.ActiveBytesPerRow);

                widthBox.Text = "129";
                CommitWithEnter(widthBox);
                Assert.AreEqual(24, window.ViewModel.ActiveBytesPerRow);
                Assert.AreEqual("24", widthBox.Text);

                widthBox.Text = string.Empty;
                CommitWithEnter(widthBox);
                Assert.AreEqual(24, window.ViewModel.ActiveBytesPerRow);
                Assert.AreEqual("24", widthBox.Text);
            }
            catch (Exception exception) { failure = exception; }
            finally { window.Close(); }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
        if (failure is not null) throw failure;
    }

    [TestMethod]
    public void EditorRowWidthTextAreaIsEditableAndArrowOpensPresets()
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            var app = new App();
            app.InitializeComponent();
            var window = new MainWindow();
            try
            {
                window.Show();
                window.UpdateLayout();
                var widthBox = (ComboBox)window.FindName("RowWidthBox");
                widthBox.ApplyTemplate();
                var editor = (TextBox)widthBox.Template.FindName("PART_EditableTextBox", widthBox);
                Assert.IsTrue(editor.IsVisible);
                Assert.IsFalse(editor.IsReadOnly);
                Assert.IsTrue(HasAncestor<TextBox>(widthBox.InputHitTest(new Point(12, widthBox.ActualHeight / 2)) as DependencyObject),
                    "The value area must receive mouse input for editing.");
                Assert.IsTrue(HasAncestor<System.Windows.Controls.Primitives.ToggleButton>(
                    widthBox.InputHitTest(new Point(widthBox.ActualWidth - 10, widthBox.ActualHeight / 2)) as DependencyObject),
                    "The arrow area must receive mouse input for the preset list.");

                editor.Text = "15";
                Assert.AreEqual("15", widthBox.Text);
                CommitWithEnter(widthBox);
                Assert.AreEqual(15, window.ViewModel.ActiveBytesPerRow);

                var toggle = (System.Windows.Controls.Primitives.ToggleButton)widthBox.Template.FindName("DropDownToggle", widthBox);
                Assert.IsTrue(toggle.ActualWidth >= 24 && toggle.ActualHeight >= 28,
                    "The arrow button must fill its click area in the application template.");
                toggle.IsChecked = true;
                Assert.IsTrue(widthBox.IsDropDownOpen);
                CollectionAssert.AreEqual(new[] { 8, 16, 32 }, widthBox.Items.Cast<int>().ToArray());
            }
            catch (Exception exception) { failure = exception; }
            finally { window.Close(); app.Shutdown(); }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
        if (failure is not null) System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(failure).Throw();
    }

    private static bool HasAncestor<T>(DependencyObject? element) where T : DependencyObject
    {
        while (element is not null)
        {
            if (element is T) return true;
            element = VisualTreeHelper.GetParent(element);
        }
        return false;
    }

    private static void CommitWithEnter(ComboBox combo)
    {
        var source = PresentationSource.FromVisual(combo);
        var args = new KeyEventArgs(Keyboard.PrimaryDevice, source, Environment.TickCount, Key.Enter)
        {
            RoutedEvent = Keyboard.PreviewKeyDownEvent
        };
        combo.RaiseEvent(args);
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
