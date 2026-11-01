using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using HexEditor.Compare;
using HexEditor.Core;
using HexEditor.Formats;
using HexEditor.UI;

namespace HexEditor.Tests;

[TestClass]
[DoNotParallelize]
public sealed class CompareAddressRangeControlsAcceptanceTests
{
    [TestMethod]
    public void RangeControlsAreAvailableOnlyInAddressRangeMode()
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            var app = new App();
            app.InitializeComponent();
            Window? window = null;
            try
            {
                var image = MemoryImage.FromSegments([new MemorySegment(0x1000, [0x10])]);
                var view = new CompareView(new CompareWorkspaceTabViewModel(image, image, "base.hex", "reference.hex"));
                window = new Window { Content = view, Width = 1100, Height = 600, ShowInTaskbar = false };
                window.Show();
                window.UpdateLayout();

                var rangeControls = (FrameworkElement)view.FindName("RangeControls");
                var start = (TextBox)view.FindName("RangeStartBox");
                var end = (TextBox)view.FindName("RangeEndBox");
                var apply = (Button)view.FindName("ApplyRangeButton");
                var go = (TextBox)view.FindName("AddressBox");

                foreach (var mode in new[] { CompareViewMode.AllRegions, CompareViewMode.DifferencesOnly })
                {
                    view.ViewModel.ViewMode = mode;
                    Assert.IsFalse(rangeControls.IsEnabled);
                    Assert.IsFalse(start.IsEnabled);
                    Assert.IsFalse(end.IsEnabled);
                    Assert.IsFalse(apply.IsEnabled);
                    Assert.IsTrue(ToolTipService.GetShowOnDisabled(rangeControls));
                    StringAssert.Contains(rangeControls.ToolTip?.ToString(), "Address Range");
                    Assert.IsTrue(go.IsEnabled);
                }

                view.ViewModel.ViewMode = CompareViewMode.AddressRange;
                Assert.IsTrue(rangeControls.IsEnabled);
                Assert.IsTrue(start.IsEnabled);
                Assert.IsTrue(end.IsEnabled);
                Assert.IsTrue(apply.IsEnabled);
                Assert.IsNull(rangeControls.ToolTip);
                Assert.AreEqual(8, start.MaxLength);
                Assert.AreEqual(8, end.MaxLength);
                Assert.AreEqual(8, go.MaxLength);

                foreach (var box in new[] { start, end, go })
                {
                    Assert.AreEqual(HexAddressInput.FieldWidth, box.Width);
                    Assert.AreEqual(HexAddressInput.FieldFontSize, box.FontSize);
                    Assert.AreEqual(CharacterCasing.Upper, box.CharacterCasing);
                    box.Text = "abcdef12";
                    Assert.AreEqual("ABCDEF12", box.Text);
                    box.SelectAll();
                    Assert.IsTrue(IsInputRejected(box, "0x1234"));
                    Assert.IsTrue(IsInputRejected(box, "100000000"));
                    Assert.IsFalse(IsInputRejected(box, "FFFFFFFF"));
                }

                Check(new GoToAddressDialog(), 1, 1);
                Check(new RelocateSegmentDialog(0x1000, 0x1000, 1), 1, 1);
                Check(new RangeEditDialog(RangeEditDialogKind.Insert, 0x1000, 0x1000), 1, 1);
                Check(new RangeEditDialog(RangeEditDialogKind.SelectRange, 0x1000, 0x1000), 2, 2);
                Check(new RangeEditDialog(RangeEditDialogKind.Fill, 0x1000, 0x1000), 2, 2);
                Check(new ExportOptionsDialog([], [], 16, ExportScope.AddressRange), 2, 2);
                Check(new ChecksumDialog(new MainViewModel(image), (_, _) => OverlapDecision.Cancel), 3, 2);
                Check(new MergeWindow(image, image, "base.hex", "reference.hex"), 2);
            }
            catch (Exception exception) { failure = exception; }
            finally { window?.Close(); app.Shutdown(); }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
        if (failure is not null) System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(failure).Throw();
    }

    private static bool IsInputRejected(TextBox box, string input)
    {
        var composition = new TextComposition(InputManager.Current, box, input);
        var args = new TextCompositionEventArgs(InputManager.Current.PrimaryKeyboardDevice, composition)
        {
            RoutedEvent = TextCompositionManager.PreviewTextInputEvent
        };
        box.RaiseEvent(args);
        return args.Handled;
    }

    private static void Check(Window window, int expectedAddressFields, int expectedAddressRows = 0)
    {
        try
        {
            window.Left = -3000;
            window.Top = -3000;
            window.WindowStartupLocation = WindowStartupLocation.Manual;
            window.ShowInTaskbar = false;
            window.ShowActivated = false;
            window.Show();
            window.UpdateLayout();

            var fields = Descendants<TextBox>(window).Where(HexAddressInput.GetApply).ToArray();
            Assert.HasCount(expectedAddressFields, fields, window.GetType().Name);
            foreach (var field in fields)
            {
                Assert.AreEqual(HexAddressInput.FieldWidth, field.Width);
                Assert.AreEqual(HexAddressInput.FieldFontSize, field.FontSize);
                Assert.AreEqual(8, field.MaxLength);
                Assert.AreEqual(CharacterCasing.Upper, field.CharacterCasing);
                field.Text = "abcdef12";
                Assert.AreEqual("ABCDEF12", field.Text);
            }

            var rows = Descendants<Grid>(window)
                .Where(grid => grid.ColumnDefinitions.Count == 2 &&
                    grid.ColumnDefinitions[0].Width.Value == AddressFieldRow.LabelWidth &&
                    grid.Children.OfType<TextBox>().Any(HexAddressInput.GetApply))
                .ToArray();
            Assert.HasCount(expectedAddressRows, rows, window.GetType().Name);
            var fieldPositions = new List<double>();
            foreach (var row in rows)
            {
                var label = row.Children.OfType<TextBlock>().Single();
                var field = row.Children.OfType<TextBox>().Single();
                Assert.AreEqual(0, Grid.GetColumn(label));
                Assert.AreEqual(1, Grid.GetColumn(field));
                var labelY = label.TranslatePoint(new Point(0, label.ActualHeight / 2), window).Y;
                var fieldY = field.TranslatePoint(new Point(0, field.ActualHeight / 2), window).Y;
                Assert.IsTrue(Math.Abs(labelY - fieldY) <= 1, $"{window.GetType().Name}: label and field must share one line.");
                fieldPositions.Add(field.TranslatePoint(new Point(), window).X);
            }
            Assert.IsTrue(fieldPositions.Count < 2 || fieldPositions.Max() - fieldPositions.Min() <= 1,
                $"{window.GetType().Name}: address fields must align vertically.");
        }
        finally { window.Close(); }
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
}
