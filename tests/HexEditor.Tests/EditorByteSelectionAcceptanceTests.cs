using HexEditor.Core;
using HexEditor.UI;
using System.Windows;
using System.Windows.Media;

namespace HexEditor.Tests;

[TestClass]
[DoNotParallelize]
public sealed class EditorByteSelectionAcceptanceTests
{
    [TestMethod]
    public void OccupiedByteClickOutlinesHexAndAsciiAndRowClickClearsItInBothLayouts()
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
                    main.ViewModel.LoadImage(MemoryImage.FromSegments([new MemorySegment(0x1000, [0x41, 0x42])]), null, null);
                    main.Show();
                    main.UpdateLayout();
                    var view = (HexViewControl)main.FindName("HexRowsView");
                    view.UseWindowedScrolling = windowed;
                    main.UpdateLayout();
                    var hex = Descendants<HexByteRowControl>(view).Single(control => control.IsVisible && !control.IsAscii && control.Row?.Address == "00001000");
                    var ascii = Descendants<HexByteRowControl>(view).Single(control => control.IsVisible && control.IsAscii && control.Row?.Address == "00001000");

                    Assert.IsTrue(view.SelectByteAt(hex, new Point(3, 3), extendSelection: false));
                    Assert.AreEqual(0x1000u, view.SelectionStart);
                    Assert.AreEqual(0x1000u, view.SelectionEnd);
                    Assert.AreEqual(view.SelectionStart, ascii.SelectionStart);
                    Assert.AreEqual(view.SelectionEnd, ascii.SelectionEnd);
                    Assert.IsFalse(hex.TryGetAddressAt(new Point(38, 3), out _),
                        "Blank addresses after the occupied bytes remain unselectable.");

                    view.SetSelectedRow(0);
                    Assert.IsNull(hex.SelectionStart);
                    Assert.IsNull(ascii.SelectionStart);
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
