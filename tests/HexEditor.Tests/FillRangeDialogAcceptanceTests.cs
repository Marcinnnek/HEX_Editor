using HexEditor.UI;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace HexEditor.Tests;

[TestClass]
[DoNotParallelize]
public sealed class FillRangeDialogAcceptanceTests
{
    [TestMethod]
    public void Fill_dialog_shows_inclusive_count_defaults_to_keep_and_loads_whole_image_bounds()
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            RangeEditDialog? dialog = null;
            try
            {
                dialog = new RangeEditDialog(RangeEditDialogKind.Fill, 0x1000, 0x1001, (0x10u, 0x20u))
                {
                    Left = -3000, Top = -3000, WindowStartupLocation = WindowStartupLocation.Manual,
                    ShowActivated = false, ShowInTaskbar = false
                };
                dialog.Show();
                dialog.Dispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);

                var controls = FindChildren<Control>(dialog).ToArray();
                var inputs = controls.OfType<TextBox>().ToArray();
                var pattern = inputs.Single(input => input.MinWidth == 220);
                Assert.AreEqual(FormTextBoxAppearance.Padding, pattern.Padding);
                Assert.AreEqual(VerticalAlignment.Center, pattern.VerticalContentAlignment);
                Assert.AreEqual(new Thickness(0, 0, 0, 8), pattern.Margin);
                var allRegion = FindChildren<Wpf.Ui.Controls.Button>(dialog)
                    .Single(button => Equals(button.Content, "Get fill all region"));
                var keep = controls.OfType<RadioButton>().Single(button => Equals(button.Content, "Keep existing data"));
                var overwrite = controls.OfType<RadioButton>().Single(button => Equals(button.Content, "Overwrite existing data"));
                Assert.IsTrue(keep.IsChecked);
                Assert.IsFalse(overwrite.IsChecked);
                Assert.AreEqual(Visibility.Visible, Indicator(keep).Visibility);
                Assert.AreEqual(Visibility.Collapsed, Indicator(overwrite).Visibility);
                Assert.IsTrue(allRegion.IsEnabled);
                Assert.AreEqual(HorizontalAlignment.Left, allRegion.HorizontalAlignment);
                Assert.AreEqual(new Thickness(12, 6, 12, 6), allRegion.Padding);
                Assert.IsTrue(FindChildren<TextBlock>(dialog).Any(label => label.Text == "Inclusive address count: 2"));

                allRegion.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                Assert.AreEqual("00000010", inputs[0].Text);
                Assert.AreEqual("00000020", inputs[1].Text);
                Assert.IsTrue(FindChildren<TextBlock>(dialog).Any(label => label.Text == "Inclusive address count: 17"));
                overwrite.IsChecked = true;
                dialog.UpdateLayout();
                Assert.AreEqual(Visibility.Collapsed, Indicator(keep).Visibility);
                Assert.AreEqual(Visibility.Visible, Indicator(overwrite).Visibility);
                Assert.IsFalse(keep.IsChecked);
                Assert.IsTrue(overwrite.IsChecked);
            }
            catch (Exception exception) { failure = exception; }
            finally { dialog?.Close(); }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
        if (failure is not null) System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(failure).Throw();
    }

    [TestMethod]
    public void Fill_dialog_disables_whole_image_shortcut_when_image_is_empty()
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            RangeEditDialog? dialog = null;
            try
            {
                dialog = new RangeEditDialog(RangeEditDialogKind.Fill, 0, 0, null)
                {
                    Left = -3000, Top = -3000, WindowStartupLocation = WindowStartupLocation.Manual,
                    ShowActivated = false, ShowInTaskbar = false
                };
                dialog.Show();
                dialog.Dispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);
                var button = FindChildren<Wpf.Ui.Controls.Button>(dialog)
                    .Single(control => Equals(control.Content, "Get fill all region"));
                Assert.IsFalse(button.IsEnabled);
            }
            catch (Exception exception) { failure = exception; }
            finally { dialog?.Close(); }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
        if (failure is not null) System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(failure).Throw();
    }

    [TestMethod]
    public void Fill_dialog_marks_ranges_over_fifty_million_addresses_for_confirmation()
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            RangeEditDialog? dialog = null;
            try
            {
                dialog = new RangeEditDialog(RangeEditDialogKind.Fill, 0, 0x02FAF080, null)
                {
                    Left = -3000, Top = -3000, WindowStartupLocation = WindowStartupLocation.Manual,
                    ShowActivated = false, ShowInTaskbar = false
                };
                dialog.Show();
                dialog.Dispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);
                Assert.IsTrue(FindChildren<TextBlock>(dialog).Any(label => label.Text.Contains("confirmation required", StringComparison.Ordinal)));
            }
            catch (Exception exception) { failure = exception; }
            finally { dialog?.Close(); }
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

    private static Ellipse Indicator(RadioButton radio) =>
        (Ellipse)radio.Template.FindName("PART_Indicator", radio);
}
