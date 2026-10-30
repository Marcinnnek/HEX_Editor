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
                    MemoryImage.FromSegments([new MemorySegment(0x1000, [0x10]), new MemorySegment(0x1010, [0x11])]),
                    MemoryImage.FromSegments([new MemorySegment(0x2000, [0x20]), new MemorySegment(0x2010, [0x21])]),
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

                Assert.IsTrue(segmentLists.All(grid => !grid.CanUserSortColumns));
                Assert.IsTrue(segmentLists.All(grid => grid.RowHeight == 20));
                Assert.IsTrue(segmentLists.All(grid => grid.HorizontalScrollBarVisibility == ScrollBarVisibility.Auto));
                foreach (var grid in segmentLists) AssertRenderedRowGap(grid);
                Assert.IsTrue(workspace.BlocksA.All(row => row.ByteCountText == $"0x{row.ByteCount:X} = {row.ByteCount}"));

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

    private static void AssertRenderedRowGap(DataGrid grid)
    {
        var firstRow = (DataGridRow)grid.ItemContainerGenerator.ContainerFromIndex(0);
        var secondRow = (DataGridRow)grid.ItemContainerGenerator.ContainerFromIndex(1);
        var firstText = Descendants<TextBlock>(firstRow).First();
        var secondText = Descendants<TextBlock>(secondRow).First();
        var firstBounds = firstText.TransformToAncestor(grid).TransformBounds(new Rect(firstText.RenderSize));
        var secondBounds = secondText.TransformToAncestor(grid).TransformBounds(new Rect(secondText.RenderSize));
        var gapInCharacterHeights = (secondBounds.Top - firstBounds.Bottom) / firstText.FontSize;
        Assert.IsTrue(gapInCharacterHeights is >= 0.35 and <= 0.65,
            $"Expected about half a character of visible text-to-text gap, got {gapInCharacterHeights:F2}.");
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
