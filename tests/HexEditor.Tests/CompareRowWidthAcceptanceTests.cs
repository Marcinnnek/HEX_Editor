using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using HexEditor.Core;
using HexEditor.UI;

namespace HexEditor.Tests;

[TestClass]
[DoNotParallelize]
public sealed class CompareRowWidthAcceptanceTests
{
    [TestMethod]
    public void MainCommandBarControlsCompareWidthAndAsciiWhileKeepingCurrentPanelAddress()
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            var left = MemoryImage.FromSegments([new MemorySegment(0x1000, Enumerable.Range(0, 32).Select(value => (byte)value).ToArray())]);
            var right = MemoryImage.FromSegments([new MemorySegment(0x1000, Enumerable.Range(0, 32).Select(value => (byte)(value + 1)).ToArray())]);
            var view = new CompareView(new CompareWorkspaceTabViewModel(left, right, "left.hex", "right.hex"));
            view.ViewModel.BytesPerRow = 24;
            var window = new MainWindow { Width = 900, Height = 500 };
            try
            {
                var tabs = (TabControl)window.FindName("WorkspaceTabs");
                tabs.Items.Add(new TabItem { Header = "Compare", Content = view });
                window.Show();
                tabs.SelectedIndex = 1;
                window.UpdateLayout();
                var widthBox = (ComboBox)window.FindName("RowWidthBox");
                var ascii = (CheckBox)window.FindName("AsciiCheckBox");
                Assert.IsTrue(widthBox.IsEditable);
                Assert.IsTrue(widthBox.IsEnabled);
                Assert.IsNull(view.FindName("RowWidthBox"));
                Assert.AreEqual(view.ViewModel.ShowAscii, ascii.IsChecked);
                Assert.AreEqual("24", widthBox.Text);
                tabs.SelectedIndex = 0;
                Assert.AreEqual("16", widthBox.Text);
                tabs.SelectedIndex = 1;
                Assert.AreEqual("24", widthBox.Text);

                view.NavigateToFindMatch(ComparePanelSide.Reference, 0x1005, 0x1006);
                foreach (var width in new[] { 1, 3, 24, 80, 128 })
                {
                    widthBox.Text = width.ToString();
                    CommitWithEnter(widthBox);
                    window.UpdateLayout();

                    Assert.AreEqual(width, view.ViewModel.BytesPerRow);
                    Assert.AreEqual(width, window.ViewModel.ActiveBytesPerRow);
                    Assert.AreEqual(width, view.FileAView.BytesPerRow);
                    Assert.AreEqual(width, view.FileBView.BytesPerRow);
                    Assert.AreEqual((ComparePanelSide.Reference, 0x1005u), view.CurrentFindCursor);
                }

                view.ViewModel.BytesPerRow = 16;
                window.UpdateLayout();
                Assert.AreEqual(16, view.FileAView.BytesPerRow);
                Assert.AreEqual(16, view.FileBView.BytesPerRow);
                Assert.AreEqual((ComparePanelSide.Reference, 0x1005u), view.CurrentFindCursor);

                ascii.IsChecked = false;
                Assert.IsFalse(view.ViewModel.ShowAscii);
                ascii.IsChecked = true;
                Assert.IsTrue(view.ViewModel.ShowAscii);

                widthBox.Text = "129";
                CommitWithEnter(widthBox);
                Assert.AreEqual(16, view.ViewModel.BytesPerRow);
                Assert.AreEqual("16", widthBox.Text);

                widthBox.Text = string.Empty;
                CommitWithEnter(widthBox);
                Assert.AreEqual(16, view.ViewModel.BytesPerRow);
                Assert.AreEqual("16", widthBox.Text);
            }
            catch (Exception exception) { failure = exception; }
            finally { window.Close(); }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
        if (failure is not null) throw failure;
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
}
