using HexEditor.Core;
using HexEditor.UI;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace HexEditor.Tests;

[TestClass]
[DoNotParallelize]
public sealed class OuterInsetRowSelectionAcceptanceTests
{
    [TestMethod]
    public void SelectingGapRowClearsByteRangeAndPreservesCurrentAddressInBothLayouts()
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
                        new MemorySegment(0x1000, Enumerable.Range(0, 32).Select(value => (byte)value).ToArray()),
                        new MemorySegment(0x1100, [0xA5])]), null, null);
                    main.Show();
                    main.UpdateLayout();
                    var view = (HexViewControl)main.FindName("HexRowsView");
                    view.UseWindowedScrolling = windowed;
                    main.UpdateLayout();
                    var firstRow = Descendants<HexByteRowControl>(view).First(control =>
                        control.IsVisible && !control.IsAscii && control.Row?.Address == "00001000");
                    Assert.IsTrue(view.SelectByteAt(firstRow, new Point(2, 4), extendSelection: false));
                    Assert.IsTrue(view.SelectByteAt(firstRow, new Point(35, 4), extendSelection: true));
                    Assert.AreEqual(4L, main.ViewModel.EditorStatus.SelectionLength);

                    var gapIndex = Enumerable.Range(0, main.ViewModel.ImageView.Rows.Count)
                        .First(index => main.ViewModel.ImageView.Rows[index].IsGapMarker);
                    var gapRow = main.ViewModel.ImageView.Rows[gapIndex];
                    view.ScrollToRow(gapIndex);
                    main.UpdateLayout();
                    RaiseAddressRowClick(view, gapRow, gapIndex);

                    Assert.AreEqual(0x1000u, main.ViewModel.EditorStatus.CurrentAddress,
                        "Selecting a gap must preserve the current occupied address.");
                    Assert.AreEqual(1L, main.ViewModel.EditorStatus.SelectionLength,
                        "Selecting a gap must collapse the previous multi-byte selection.");
                    Assert.AreEqual(gapIndex, view.SelectedRowIndex);
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
    public void AsciiOuterInsetsSelectTheRowInBothLayoutsAndKeepByteCellsClickable()
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            Window? window = null;
            try
            {
                foreach (var bytesPerRow in new[] { 8, 16, 32 })
                foreach (var windowed in new[] { false, true })
                {
                    var main = new MainWindow();
                    main.ViewModel.ActiveBytesPerRow = bytesPerRow;
                    main.ViewModel.LoadImage(MemoryImage.FromSegments([
                        new MemorySegment(0x1000, Enumerable.Range(0, 64).Select(value => (byte)value).ToArray()),
                        new MemorySegment(0x1100, [0xA5])]), null, null);
                    window = main;
                    main.Show();
                    main.UpdateLayout();
                    var view = (HexViewControl)main.FindName("HexRowsView");
                    view.UseWindowedScrolling = windowed;
                    main.UpdateLayout();
                    var row = main.ViewModel.ImageView.Rows[0];
                    var asciiControl = Descendants<HexByteRowControl>(view)
                        .Single(control => control.IsVisible && control.IsAscii && control.Row?.Address == row.Address);
                    var hexControl = Descendants<HexByteRowControl>(view)
                        .Single(control => control.IsVisible && !control.IsAscii && control.Row?.Address == row.Address);

                    RaiseOuterBlankClick(view, hexControl, 0);
                    Assert.AreEqual(0x1000u, main.ViewModel.EditorStatus.CurrentAddress,
                        "The Address cell selects the row start in either layout.");

                    var rightInsetStart = asciiControl.ActualWidth - 6;
                    Assert.IsTrue(asciiControl.IsOuterInsetAt(new Point(5.99, 4)), "The left inset ends immediately before the first byte hit area.");
                    Assert.IsFalse(asciiControl.IsOuterInsetAt(new Point(6, 4)), "The first byte hit area begins at the six-unit inset boundary.");
                    Assert.IsTrue(asciiControl.TryGetAddressAt(new Point(6, 4), out var firstAsciiByte));
                    Assert.AreEqual(0x1000u, firstAsciiByte);
                    Assert.IsFalse(asciiControl.IsOuterInsetAt(new Point(rightInsetStart - 0.01, 4)),
                        "The final byte hit area ends immediately before the right inset.");
                    Assert.IsTrue(asciiControl.IsOuterInsetAt(new Point(rightInsetStart, 4)),
                        "The rendered right inset begins six units before the column edge.");

                    Assert.IsTrue(view.SelectByteAt(hexControl, new Point(3, 4), extendSelection: false));
                    Assert.IsTrue(view.SelectByteAt(hexControl, new Point(18, 4), extendSelection: true));
                    RaiseOuterBlankClick(view, hexControl, 1);
                    Assert.AreEqual(0x1000u, main.ViewModel.EditorStatus.CurrentAddress,
                        "Clicking the HEX column's outer margin selects the row start.");
                    Assert.AreEqual(1u, main.ViewModel.EditorStatus.SelectionLength);

                    Assert.IsTrue(view.SelectByteAt(hexControl, new Point(3, 4), extendSelection: false));
                    Assert.IsTrue(view.SelectByteAt(hexControl, new Point(18, 4), extendSelection: true));
                    Assert.AreEqual(2u, main.ViewModel.EditorStatus.SelectionLength,
                        "The byte cells remain independently clickable before selecting the outer inset.");
                    var visibleStart = view.FirstVisibleRowIndex;
                    Assert.IsTrue(view.SelectRowFromOuterInset(asciiControl, new Point(2, 4)),
                        "The six-unit ASCII outer inset should resolve to row selection.");
                    Assert.AreEqual(0, view.SelectedRowIndex);
                    Assert.AreEqual(0x1000u, main.ViewModel.EditorStatus.CurrentAddress);
                    Assert.AreEqual(1u, main.ViewModel.EditorStatus.SelectionLength,
                        "An inset row click clears the previous multi-byte selection.");
                    Assert.AreEqual(visibleStart, view.FirstVisibleRowIndex,
                        "Selecting the row does not move the viewport.");

                    var gapIndex = Enumerable.Range(0, main.ViewModel.ImageView.Rows.Count)
                        .First(index => main.ViewModel.ImageView.Rows[index].IsGapMarker);
                    view.ScrollToRow(gapIndex);
                    main.UpdateLayout();
                    var gapAscii = Descendants<HexByteRowControl>(view)
                        .Single(control => control.IsVisible && control.IsAscii && control.Row?.IsGapMarker == true);
                    Assert.IsTrue(view.SelectRowFromOuterInset(gapAscii, new Point(2, 4)));
                    Assert.AreEqual(gapIndex, view.SelectedRowIndex);
                    Assert.AreEqual(0x1000u, main.ViewModel.EditorStatus.CurrentAddress,
                        "Selecting an address-gap outer inset leaves the occupied current address unchanged.");
                    var editWindowsBefore = main.OwnedWindows.OfType<EditRowWindow>().Count();
                    view.RaiseWindowedByteDoubleClick(gapAscii, DoubleClick(gapAscii), new Point(2, 4));
                    Assert.AreEqual(editWindowsBefore, main.OwnedWindows.OfType<EditRowWindow>().Count(),
                        "An address-gap inset double-click does not open Edit Row.");

                    window.Close();
                    window = null;
                }

                foreach (var bytesPerRow in new[] { 8, 16, 32 })
                foreach (var windowed in new[] { false, true })
                {
                    var left = MemoryImage.FromSegments([
                        new MemorySegment(0x2000, Enumerable.Range(0, 64).Select(value => (byte)value).ToArray()),
                        new MemorySegment(0x2100, [0xA5])]);
                    var right = MemoryImage.FromSegments([
                        new MemorySegment(0x2000, Enumerable.Range(0, 64).Select(value => (byte)value).ToArray()),
                        new MemorySegment(0x2100, [0xA5])]);
                    var compare = new CompareView(new CompareWorkspaceTabViewModel(left, right, "base.hex", "reference.hex"));
                    compare.ViewModel.BytesPerRow = bytesPerRow;
                    window = new Window { Content = compare, Width = 1200, Height = 500, Left = -3000, Top = -3000, ShowInTaskbar = false };
                    window.Show();
                    window.Dispatcher.Invoke(() => { }, System.Windows.Threading.DispatcherPriority.ApplicationIdle);
                    compare.UpdateLayout();
                    var panels = Descendants<HexViewControl>(compare).ToArray();
                    foreach (var panel in panels) panel.UseWindowedScrolling = windowed;
                    compare.UpdateLayout();
                    var control = Descendants<HexByteRowControl>(panels[0])
                        .Single(item => item.IsAscii && item.Row?.Address == "00002000");

                    RaiseOuterBlankClick(panels[0], Descendants<HexByteRowControl>(panels[0])
                        .Single(item => !item.IsAscii && item.Row?.Address == "00002000"), 1);
                    Assert.AreEqual(0, panels[0].SelectedRowIndex);
                    Assert.AreEqual(0, panels[1].SelectedRowIndex,
                        "Compare mirrors a HEX outer-margin row selection in both panels.");

                    Assert.IsTrue(panels[0].SelectRowFromOuterInset(control, new Point(2, 4)));
                    Assert.AreEqual(0, panels[0].SelectedRowIndex);
                    Assert.AreEqual(0, panels[1].SelectedRowIndex,
                        "Compare mirrors an outer-inset row selection in both panels.");
                    Assert.IsTrue(Descendants<System.Windows.Controls.TextBox>(compare)
                        .Any(box => box.Text == "00002000"));
                    RaiseOuterBlankClick(panels[0], Descendants<HexByteRowControl>(panels[0])
                        .Single(item => item.IsVisible && !item.IsAscii && item.Row?.Address == "00002000"), 0);
                    Assert.AreEqual(0, panels[0].SelectedRowIndex);
                    Assert.AreEqual(0, panels[1].SelectedRowIndex,
                        "The Address cell selects the matching row in both Compare panels.");

                    var gapIndex = Enumerable.Range(0, compare.ViewModel.FileARows.Count)
                        .First(index => compare.ViewModel.FileARows[index].IsGapMarker);
                    panels[0].ScrollToRow(gapIndex);
                    compare.UpdateLayout();
                    var gapAscii = Descendants<HexByteRowControl>(panels[0])
                        .Single(item => item.IsVisible && item.IsAscii && item.Row?.IsGapMarker == true);
                    Assert.IsTrue(panels[0].SelectRowFromOuterInset(gapAscii, new Point(2, 4)));
                    Assert.AreEqual(gapIndex, panels[0].SelectedRowIndex);
                    Assert.AreEqual(gapIndex, panels[1].SelectedRowIndex,
                        "Compare highlights the address-gap row in both panels.");
                    Assert.IsTrue(Descendants<System.Windows.Controls.TextBox>(compare)
                        .Any(box => box.Text == "00002000"),
                        "Selecting an address-gap inset leaves the current address unchanged.");
                    var activated = new List<(uint Row, uint Address)>();
                    compare.BaseRowActivated += (row, address) => activated.Add((row, address));
                    panels[0].RaiseWindowedByteDoubleClick(gapAscii, DoubleClick(gapAscii), new Point(2, 4));
                    Assert.HasCount(0, activated, "An address-gap inset does not activate Edit Row in Compare.");

                    window.Close();
                    window = null;
                }
            }
            catch (Exception exception) { failure = exception; }
            finally { window?.Close(); }
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

    private static void RaiseOuterBlankClick(HexViewControl view, HexByteRowControl byteControl, int columnIndex)
    {
        if (view.UseWindowedScrolling)
        {
            var rowGrid = VisualTreeHelper.GetParent(byteControl) as UIElement;
            Assert.IsNotNull(rowGrid);
            var windowedView = (UIElement)view.FindName("WindowedView");
            windowedView.RaiseEvent(new System.Windows.Input.MouseButtonEventArgs(
                System.Windows.Input.Mouse.PrimaryDevice, Environment.TickCount, System.Windows.Input.MouseButton.Left)
            {
                RoutedEvent = UIElement.PreviewMouseLeftButtonDownEvent,
                Source = rowGrid
            });
            return;
        }

        var grid = view.ItemsGrid;
        var row = grid.Items.Cast<HexViewRow>().First(item => item.Address == byteControl.Row?.Address);
        grid.ScrollIntoView(row);
        grid.UpdateLayout();
        var content = grid.Columns[columnIndex].GetCellContent(row);
        Assert.IsNotNull(content);
        var cell = Ancestor<DataGridCell>(content);
        Assert.IsNotNull(cell);
        grid.RaiseEvent(new System.Windows.Input.MouseButtonEventArgs(
            System.Windows.Input.Mouse.PrimaryDevice, Environment.TickCount, System.Windows.Input.MouseButton.Left)
        {
            RoutedEvent = UIElement.PreviewMouseLeftButtonDownEvent,
            Source = cell
        });
    }

    private static void RaiseAddressRowClick(HexViewControl view, HexViewRow row, int rowIndex)
    {
        if (view.UseWindowedScrolling)
        {
            var address = Descendants<HexRowAddressControl>(view).Single(control => control.IsVisible &&
                control.Row?.IsGapMarker == row.IsGapMarker && control.Row?.GapDescription == row.GapDescription);
            address.RaiseEvent(new System.Windows.Input.MouseButtonEventArgs(
                System.Windows.Input.Mouse.PrimaryDevice, Environment.TickCount, System.Windows.Input.MouseButton.Left)
            {
                RoutedEvent = UIElement.MouseLeftButtonDownEvent,
                Source = address
            });
            return;
        }

        var grid = view.ItemsGrid;
        var content = grid.Columns[0].GetCellContent(grid.Items[rowIndex]);
        Assert.IsNotNull(content);
        var cell = Ancestor<DataGridCell>(content);
        Assert.IsNotNull(cell);
        grid.RaiseEvent(new System.Windows.Input.MouseButtonEventArgs(
            System.Windows.Input.Mouse.PrimaryDevice, Environment.TickCount, System.Windows.Input.MouseButton.Left)
        {
            RoutedEvent = UIElement.PreviewMouseLeftButtonDownEvent,
            Source = cell
        });
    }

    private static T? Ancestor<T>(DependencyObject child) where T : DependencyObject
    {
        for (var parent = VisualTreeHelper.GetParent(child); parent is not null; parent = VisualTreeHelper.GetParent(parent))
            if (parent is T match) return match;
        return null;
    }

    private static System.Windows.Input.MouseButtonEventArgs DoubleClick(HexByteRowControl source)
    {
        var click = new System.Windows.Input.MouseButtonEventArgs(System.Windows.Input.Mouse.PrimaryDevice,
            Environment.TickCount, System.Windows.Input.MouseButton.Left)
        {
            RoutedEvent = UIElement.MouseLeftButtonDownEvent,
            Source = source
        };
        typeof(System.Windows.Input.MouseButtonEventArgs).GetProperty(nameof(click.ClickCount))!.SetValue(click, 2);
        return click;
    }

}
