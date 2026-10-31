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
    public void CompareWidthControlAcceptsCustomWidthsAndKeepsCurrentPanelAddress()
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            var left = MemoryImage.FromSegments([new MemorySegment(0x1000, Enumerable.Range(0, 32).Select(value => (byte)value).ToArray())]);
            var right = MemoryImage.FromSegments([new MemorySegment(0x1000, Enumerable.Range(0, 32).Select(value => (byte)(value + 1)).ToArray())]);
            var view = new CompareView(new CompareWorkspaceTabViewModel(left, right, "left.hex", "right.hex"));
            var window = new Window { Content = view, Width = 900, Height = 500 };
            try
            {
                window.Show();
                window.UpdateLayout();
                var widthBox = (ComboBox)view.FindName("RowWidthBox");
                Assert.IsTrue(widthBox.IsEditable);

                view.NavigateToFindMatch(ComparePanelSide.Reference, 0x1005, 0x1006);
                foreach (var width in new[] { 1, 3, 24, 80, 128 })
                {
                    widthBox.Text = width.ToString();
                    CommitWithEnter(widthBox);
                    window.UpdateLayout();

                    Assert.AreEqual(width, view.ViewModel.BytesPerRow);
                    Assert.AreEqual(width, view.FileAView.BytesPerRow);
                    Assert.AreEqual(width, view.FileBView.BytesPerRow);
                    Assert.AreEqual((ComparePanelSide.Reference, 0x1005u), view.CurrentFindCursor);
                }

                view.ViewModel.BytesPerRow = 16;
                window.UpdateLayout();
                Assert.AreEqual(16, view.FileAView.BytesPerRow);
                Assert.AreEqual(16, view.FileBView.BytesPerRow);
                Assert.AreEqual((ComparePanelSide.Reference, 0x1005u), view.CurrentFindCursor);

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
