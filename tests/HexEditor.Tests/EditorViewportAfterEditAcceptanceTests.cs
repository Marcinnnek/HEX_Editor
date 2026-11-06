using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;
using HexEditor.Core;
using HexEditor.UI;

namespace HexEditor.Tests;

[TestClass]
[DoNotParallelize]
public sealed class EditorViewportAfterEditAcceptanceTests
{
    [TestMethod]
    public void EditingASelectedByteKeepsItsViewportAndSelection() => RunAtSelectedByte((window, view, address) =>
    {
        var edit = window.ViewModel.Workspace.BeginEditRow(window.ViewModel.Workspace.Document,
            address, address, window.ViewModel.ImageView.BytesPerRow);
        Assert.IsTrue(edit.SetHex(address, "13"));
        Assert.IsTrue(edit.Commit());
        Pump(window);

        Assert.AreEqual(50, view.FirstVisibleRowIndex,
            "Editing a selected byte must not return the viewport to the file start.");
        Assert.AreEqual(address, view.SelectionStart);
        Assert.AreEqual(address, view.SelectionEnd);
        Assert.AreEqual(address, window.ViewModel.EditorStatus.CurrentAddress);
    });

    [TestMethod]
    public void ZeroFillDeleteUndoAndRedoKeepTheAffectedAreaVisible() => RunAtSelectedByte((window, view, address) =>
    {
        var editing = window.ViewModel.Workspace.Document.Editing;
        void Check() { Pump(window); Assert.AreEqual(50, view.FirstVisibleRowIndex); }

        editing.ZeroOccupiedRange(address, address);
        Check();
        Assert.AreEqual(address, window.ViewModel.EditorStatus.CurrentAddress);
        Assert.IsTrue(editing.Undo());
        Check();
        Assert.IsTrue(editing.Redo());
        Check();

        Task? fill = null;
        window.Dispatcher.BeginInvoke(new Action(() =>
            fill = editing.FillRangeAsync(address, address, [0x34], FillPolicy.OverwriteExisting,
                CancellationToken.None, progress: null)));
        var deadline = DateTime.UtcNow.AddSeconds(5);
        while (fill?.IsCompleted != true && DateTime.UtcNow < deadline) Pump(window);
        Assert.IsNotNull(fill, "The Fill command should start on the UI dispatcher.");
        Assert.IsTrue(fill.IsCompleted, "The asynchronous Fill operation should complete promptly.");
        fill.GetAwaiter().GetResult();
        Check();
        Assert.AreEqual(address, window.ViewModel.EditorStatus.CurrentAddress);

        editing.DeleteRange(address, address);
        Check();
        Assert.IsFalse(window.ViewModel.CurrentImage.TryGetByte(address, out _));
        Assert.IsTrue(editing.Undo());
        Check();
    });

    [TestMethod]
    public void RemovingSeveralVisibleRowsKeepsTheViewportNearTheRemovedRange() => RunAtSelectedByte((window, view, _) =>
    {
        var rowWidth = (uint)window.ViewModel.ImageView.BytesPerRow;
        window.ViewModel.DeleteRange(50 * rowWidth, 59 * rowWidth - 1);
        Pump(window);

        Assert.IsTrue(view.FirstVisibleRowIndex is >= 40 and <= 55,
            $"Removing visible rows should stay nearby; actual first row: {view.FirstVisibleRowIndex}.");
    });

    [TestMethod]
    public void OpeningAnotherDocumentStillStartsAtItsFirstRow() => RunAtSelectedByte((window, view, _) =>
    {
        window.ViewModel.LoadImage(MemoryImage.FromPatternRange(0x10000, 0x11FFF, [0x56]), null, null);
        Pump(window);

        Assert.AreEqual(0, view.FirstVisibleRowIndex);
        Assert.AreEqual("00010000", view.WindowRows[0].Row.Address);
    });

    private static void RunAtSelectedByte(Action<MainWindow, HexViewControl, uint> verify)
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            MainWindow? window = null;
            try
            {
                window = new MainWindow();
                window.ViewModel.LoadImage(MemoryImage.FromPatternRange(0, 8191, [0x12]), null, null);
                window.Show();
                window.UpdateLayout();
                var view = (HexViewControl)window.FindName("HexRowsView");
                view.ScrollToRow(50);
                Pump(window);
                var address = 54u * (uint)window.ViewModel.ImageView.BytesPerRow;
                var row = Descendants<HexByteRowControl>(view)
                    .Single(item => !item.IsAscii && item.Row?.Address == address.ToString("X8"));
                Assert.IsTrue(view.SelectByteAt(row, new Point(3, 3), extendSelection: false));
                Assert.AreEqual(50, view.FirstVisibleRowIndex);
                Assert.AreEqual(address, view.SelectionStart);

                verify(window, view, address);
            }
            catch (Exception exception) { failure = exception; }
            finally
            {
                if (window is not null)
                {
                    var editing = window.ViewModel.Workspace.Document.Editing;
                    while (window.ViewModel.Workspace.Document.IsModified && editing.CanUndo)
                        editing.Undo();
                    window.Close();
                }
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
        if (failure is not null) System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(failure).Throw();
    }

    private static void Pump(Window window) =>
        window.Dispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);

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
