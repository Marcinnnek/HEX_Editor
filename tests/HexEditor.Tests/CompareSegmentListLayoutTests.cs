using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using HexEditor.Core;
using HexEditor.UI;

namespace HexEditor.Tests;

[TestClass]
[DoNotParallelize]
public sealed class CompareSegmentListLayoutTests
{
    [DataRow(1100)]
    [DataRow(650)]
    [TestMethod]
    public void EachMemorySegmentListStaysAboveItsOwnCompareDataPanel(int width)
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try
            {
                var workspace = new CompareWorkspaceTabViewModel(
                    MemoryImage.FromSegments([new MemorySegment(0x1000, [0x10])]),
                    MemoryImage.FromSegments([new MemorySegment(0x2000, [0x20])]),
                    "base.hex", "reference.hex");
                var view = new CompareView(workspace);
                view.Measure(new Size(width, 650));
                view.Arrange(new Rect(0, 0, width, 650));
                view.UpdateLayout();

                var segmentLists = Descendants<DataGrid>(view)
                    .Where(grid => ReferenceEquals(grid.ItemsSource, workspace.BlocksA) ||
                                   ReferenceEquals(grid.ItemsSource, workspace.BlocksB))
                    .ToArray();
                var dataPanels = Descendants<HexViewControl>(view).ToArray();
                Assert.HasCount(2, segmentLists);
                Assert.HasCount(2, dataPanels);

                AssertAligned(workspace.BlocksA, workspace.Comparison.FileARows);
                AssertAligned(workspace.BlocksB, workspace.Comparison.FileBRows);

                // A moved divider changes the shared column width while preserving ownership.
                var compareGrid = Ancestor<Grid>(dataPanels[0]);
                Assert.IsNotNull(compareGrid);
                compareGrid.ColumnDefinitions[0].Width = new GridLength(width * 0.4, GridUnitType.Pixel);
                view.Measure(new Size(width, 650));
                view.Arrange(new Rect(0, 0, width, 650));
                view.UpdateLayout();
                AssertAligned(workspace.BlocksA, workspace.Comparison.FileARows);
                AssertAligned(workspace.BlocksB, workspace.Comparison.FileBRows);

                // Both segment lists still navigate their aligned data panels by address.
                SelectSegment(workspace.BlocksA, workspace.Comparison.FileARows, "00001000");
                SelectSegment(workspace.BlocksB, workspace.Comparison.FileBRows, "00002000");

                void SelectSegment(object blocks, object rows, string expectedAddress)
                {
                    var list = segmentLists.Single(grid => ReferenceEquals(grid.ItemsSource, blocks));
                    list.SelectedIndex = 0;
                    var panel = dataPanels.Single(control => ReferenceEquals(control.ItemsSource, rows));
                    Assert.AreEqual(expectedAddress, ((HexViewRow)panel.ItemsGrid.SelectedItem).Address);
                }

                void AssertAligned(object blocks, object rows)
                {
                    var list = segmentLists.Single(grid => ReferenceEquals(grid.ItemsSource, blocks));
                    var panel = dataPanels.Single(control => ReferenceEquals(control.ItemsSource, rows));
                    var group = Ancestor<GroupBox>(list);
                    Assert.IsNotNull(group);
                    var listBounds = group.TransformToAncestor(view)
                        .TransformBounds(new Rect(group.RenderSize));
                    var panelBounds = panel.TransformToAncestor(view)
                        .TransformBounds(new Rect(panel.RenderSize));
                    Assert.AreEqual(panelBounds.Left, listBounds.Left, 1.0);
                    Assert.AreEqual(panelBounds.Width, listBounds.Width, 1.0);
                    Assert.IsLessThanOrEqualTo(panelBounds.Top, listBounds.Bottom);
                    Assert.IsLessThanOrEqualTo(12, panelBounds.Top - listBounds.Bottom);
                    Assert.IsGreaterThan(200, listBounds.Width);
                }
            }
            catch (Exception exception) { failure = exception; }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
        if (failure is not null) throw failure;
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

    private static T? Ancestor<T>(DependencyObject child) where T : DependencyObject
    {
        for (var parent = VisualTreeHelper.GetParent(child); parent is not null; parent = VisualTreeHelper.GetParent(parent))
            if (parent is T match) return match;
        return null;
    }
}
