using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using HexEditor.Core;
using HexEditor.UI;

namespace HexEditor.Tests;

[TestClass]
[DoNotParallelize]
public sealed class ByteSelectionClipboardAcceptanceTests
{
    [TestMethod]
    public void EditorCopyCommandCopiesOnlyOccupiedBytesInTheSelectedRangeInBothLayouts()
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            MainWindow? main = null;
            try
            {
                foreach (var windowed in new[] { false, true })
                {
                    main = new MainWindow();
                    main.ViewModel.LoadImage(MemoryImage.FromSegments([
                        new MemorySegment(0x1000, [0xB6, 0x5A]),
                        new MemorySegment(0x1010, [0xC6])]), null, null);
                    main.Show();
                    main.UpdateLayout();
                    var view = (HexViewControl)main.FindName("HexRowsView");
                    view.UseWindowedScrolling = windowed;
                    main.UpdateLayout();

                    var first = FindHexRow(view, "00001000");
                    var last = FindHexRow(view, "00001010");
                    Assert.IsTrue(view.SelectByteAt(first, new Point(3, 3), extendSelection: false));
                    Assert.AreEqual("B6", CopyAndRead(main));
                    Assert.IsTrue(view.SelectByteAt(last, new Point(3, 3), extendSelection: true));
                    Assert.AreEqual("B6 5A C6", CopyAndRead(main));

                    main.Close();
                    main = null;
                }
            }
            catch (Exception exception) { failure = exception; }
            finally { main?.Close(); }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
        if (failure is not null) System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(failure).Throw();
    }

    [TestMethod]
    public void CompareCopyCommandUsesThePanelOwningTheActiveSelection()
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            Window? window = null;
            try
            {
                var baseImage = MemoryImage.FromSegments([new MemorySegment(0x1000, [0x12, 0x34])]);
                var referenceImage = MemoryImage.FromSegments([new MemorySegment(0x1000, [0xAB, 0xCD])]);
                var compare = new CompareView(new CompareWorkspaceTabViewModel(baseImage, referenceImage, "base.hex", "reference.hex"));
                window = new Window { Content = compare, Width = 1200, Height = 760, ShowInTaskbar = false };
                window.Show();
                window.UpdateLayout();
                var left = (HexViewControl)compare.FindName("FileAView");
                var right = (HexViewControl)compare.FindName("FileBView");
                var leftRow = FindHexRow(left, "00001000");
                var rightRow = FindHexRow(right, "00001000");

                Assert.IsTrue(right.SelectByteAt(rightRow, new Point(3, 3), extendSelection: false));
                Assert.AreEqual("AB", CopyAndRead(compare));
                Assert.IsTrue(left.SelectByteAt(leftRow, new Point(3, 3), extendSelection: false));
                Assert.IsTrue(left.SelectByteAt(leftRow, new Point(18, 3), extendSelection: true));
                Assert.AreEqual("12 34", CopyAndRead(compare));
                Assert.AreEqual(0x1000u, left.SelectionStart);
                Assert.AreEqual(0x1001u, left.SelectionEnd);
                Assert.IsNull(right.SelectionStart);
            }
            catch (Exception exception) { failure = exception; }
            finally { window?.Close(); }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
        if (failure is not null) System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(failure).Throw();
    }

    private static string CopyAndRead(UIElement target)
    {
        ApplicationCommands.Copy.Execute(null, target);
        return Clipboard.GetText(TextDataFormat.UnicodeText);
    }

    private static HexByteRowControl FindHexRow(HexViewControl view, string address) =>
        Descendants<HexByteRowControl>(view).Single(item => item.IsVisible && !item.IsAscii && item.Row?.Address == address);

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
