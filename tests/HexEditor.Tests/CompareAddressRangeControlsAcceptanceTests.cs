using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using HexEditor.Compare;
using HexEditor.Core;
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
                Assert.AreEqual(10, start.MaxLength);
                Assert.AreEqual(10, end.MaxLength);
                Assert.AreEqual(10, go.MaxLength);

                foreach (var box in new[] { start, end, go })
                {
                    Assert.AreEqual(CharacterCasing.Upper, box.CharacterCasing);
                    box.Text = "0xabcdef12";
                    Assert.AreEqual("0XABCDEF12", box.Text);
                    box.SelectAll();
                    Assert.IsTrue(IsInputRejected(box, "0x12G4"));
                    Assert.IsTrue(IsInputRejected(box, "100000000"));
                    Assert.IsFalse(IsInputRejected(box, "0xFFFFFFFF"));
                }
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
}
