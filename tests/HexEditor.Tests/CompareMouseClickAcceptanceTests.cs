using HexEditor.Core;
using HexEditor.Compare;
using HexEditor.UI;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using System.Windows.Controls.Primitives;

namespace HexEditor.Tests;

[TestClass]
[DoNotParallelize]
public sealed class CompareMouseClickAcceptanceTests
{
    [TestMethod]
    public void Routed_single_click_in_either_compare_panel_selects_and_aligns_the_address()
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            Window? window = null;
            try
            {
                var baseImage = MemoryImage.FromSegments([
                    new MemorySegment(0x1000, [0x10, 0x20]),
                    new MemorySegment(0x1010, [0x30])]);
                var referenceImage = MemoryImage.FromSegments([
                    new MemorySegment(0x1000, [0x10, 0x21]),
                    new MemorySegment(0x1010, [0x31])]);
                var workspace = new CompareWorkspaceTabViewModel(baseImage, referenceImage, "base.hex", "reference.hex");
                var view = new CompareView(workspace);
                var reported = new List<string>();
                var failures = new List<(string Operation, ComparePanelSide Side, uint? Address)>();
                view.StatusRequested += reported.Add;
                view.InteractionFailed += (operation, exception, side, row, address) =>
                    failures.Add((operation, side, address));
                window = new Window
                {
                    Content = view, Width = 1100, Height = 700,
                    Left = -3000, Top = -3000, WindowStartupLocation = WindowStartupLocation.Manual,
                    ShowActivated = false, ShowInTaskbar = false
                };
                window.Show();
                window.Dispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);

                var panels = FindChildren<HexViewControl>(view).ToArray();
                Assert.HasCount(2, panels);
                foreach (var panel in panels) panel.UseWindowedScrolling = false;
                foreach (var panel in panels)
                {
                    var grid = panel.ItemsGrid;
                    var row = grid.Items[0];
                    grid.ScrollIntoView(row);
                    grid.UpdateLayout();
                    var addressCell = grid.Columns[0].GetCellContent(row);
                    Assert.IsNotNull(addressCell);
                    Assert.IsNotNull(Ancestor<DataGridCell>(addressCell));
                    Assert.IsNotNull(Ancestor<DataGridRow>(addressCell));
                    var click = RaiseClick(grid, addressCell, 1);
                    Assert.IsTrue(click.Handled, "Compare should handle an occupied address-row click.");

                    Assert.AreEqual(0, panels[0].SelectedRowIndex);
                    Assert.AreEqual(0, panels[1].SelectedRowIndex);
                    Assert.IsTrue(view.NavigateToAddress(0x1010));
                    Assert.AreEqual("00001010", ((HexViewRow)panels[0].ItemsGrid.SelectedItem).Address);
                    Assert.AreEqual("00001010", ((HexViewRow)panels[1].ItemsGrid.SelectedItem).Address);
                }
                Assert.HasCount(0, reported);
                Assert.HasCount(0, failures);

                var activated = new List<(uint Row, uint Selected)>();
                view.BaseRowActivated += (row, selected) => activated.Add((row, selected));
                RaiseClick(panels[0].ItemsGrid, panels[0].ItemsGrid.Columns[0].GetCellContent(panels[0].ItemsGrid.Items[0])!, 2);
                CollectionAssert.AreEqual(new[] { (0x1000u, 0x1000u) }, activated);
                RaiseClick(panels[1].ItemsGrid, panels[1].ItemsGrid.Columns[0].GetCellContent(panels[1].ItemsGrid.Items[0])!, 2);
                CollectionAssert.Contains(reported, "The reference image is read-only.");

                view.NavigateToAddress(0x1000);
                var next = FindChildren<Wpf.Ui.Controls.Button>(view)
                    .Single(button => Equals(button.Content, "Next difference"));
                next.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
                Assert.IsTrue(FindChildren<Wpf.Ui.Controls.TextBox>(view).Any(box => box.Text == "00001001"));
                Assert.HasCount(0, failures);

                view.BaseRowActivated += (_, _) => throw new InvalidOperationException("simulated editor failure");
                RaiseClick(panels[0].ItemsGrid,
                    panels[0].ItemsGrid.Columns[0].GetCellContent(panels[0].ItemsGrid.Items[0])!, 2);
                CollectionAssert.AreEqual(new[] { ("double-click", ComparePanelSide.Base, (uint?)0x1000) }, failures);
                Assert.IsTrue(reported.Any(message => message.Contains("Base double-click failed at 0x00001000")));
                Assert.AreSame(baseImage, workspace.Comparison.FileA);
                Assert.AreSame(referenceImage, workspace.Comparison.FileB);
            }
            catch (Exception exception) { failure = exception; }
            finally { window?.Close(); }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
        if (failure is not null) System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(failure).Throw();
    }

    [TestMethod]
    public void Address_click_selects_gap_row_without_changing_address_or_scrolling_in_windowed_compare()
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            Window? window = null;
            try
            {
                var a = MemoryImage.FromSegments([new MemorySegment(0x1000, [0x11]), new MemorySegment(0x1100, [0x22])]);
                var b = MemoryImage.FromSegments([new MemorySegment(0x1000, [0x11]), new MemorySegment(0x1100, [0x22])]);
                var view = new CompareView(new CompareWorkspaceTabViewModel(a, b, "a.hex", "b.hex"));
                view.ViewModel.SetAddressRange(0x1000, 0x1100);
                view.ViewModel.ViewMode = CompareViewMode.AddressRange;
                window = new Window { Content = view, Width = 1100, Height = 700, Left = -3000, Top = -3000, ShowInTaskbar = false };
                window.Show();
                window.Dispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);
                var panels = FindChildren<HexViewControl>(view).ToArray();
                foreach (var panel in panels) panel.UseWindowedScrolling = true;
                view.NavigateToAddress(0x1000);
                var gapIndex = Enumerable.Range(0, view.ViewModel.FileARows.Count)
                    .First(index => view.ViewModel.FileARows[index].IsGapMarker);
                var beforeA = panels[0].FirstVisibleRowIndex;
                var beforeB = panels[1].FirstVisibleRowIndex;
                panels[0].ScrollToRow(gapIndex);
                window.Dispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);
                var addressControl = FindChildren<HexRowAddressControl>(panels[0]).Single(control => control.IsVisible && control.Row?.IsGapMarker == true);
                addressControl.RaiseEvent(new MouseButtonEventArgs(Mouse.PrimaryDevice, Environment.TickCount, MouseButton.Left)
                {
                    RoutedEvent = UIElement.MouseLeftButtonDownEvent,
                    Source = addressControl
                });
                Assert.AreEqual(gapIndex, panels[0].SelectedRowIndex);
                Assert.AreEqual(gapIndex, panels[1].SelectedRowIndex);
                Assert.AreEqual(beforeA, panels[0].FirstVisibleRowIndex);
                Assert.AreEqual(beforeB, panels[1].FirstVisibleRowIndex);
                Assert.IsTrue(FindChildren<Wpf.Ui.Controls.TextBox>(view).Any(box => box.Text == "00001000"));
            }
            catch (Exception exception) { failure = exception; }
            finally { window?.Close(); }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
        if (failure is not null) System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(failure).Throw();
    }

    private static MouseButtonEventArgs RaiseClick(DataGrid grid, UIElement source, int count)
    {
        var click = new MouseButtonEventArgs(Mouse.PrimaryDevice, Environment.TickCount, MouseButton.Left)
        {
            RoutedEvent = UIElement.PreviewMouseLeftButtonDownEvent,
            Source = source
        };
        typeof(MouseButtonEventArgs).GetProperty(nameof(MouseButtonEventArgs.ClickCount))!.SetValue(click, count);
        grid.RaiseEvent(click); // WPF re-raises this direct mouse event on each element.
        return click;
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

    private static T? Ancestor<T>(DependencyObject child) where T : DependencyObject
    {
        for (var parent = System.Windows.Media.VisualTreeHelper.GetParent(child); parent is not null;
            parent = System.Windows.Media.VisualTreeHelper.GetParent(parent))
            if (parent is T match) return match;
        return null;
    }
}
