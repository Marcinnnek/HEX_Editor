using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using HexEditor.UI;

namespace HexEditor.Tests;

[TestClass]
public sealed class EditorSegmentListLayoutTests
{
    [TestMethod]
    public void EditorSegmentList_UsesOnlyItsContentWidth()
    {
        var alignment = HorizontalAlignment.Stretch;
        var columnsAreAuto = false;
        var sortingDisabled = false;
        var rowHeight = 0d;
        var renderedGap = double.NaN;
        var thread = new Thread(() =>
        {
            var window = new MainWindow();
            window.ViewModel.Segments.Add(new MemorySegmentRow("00001000", "00001000", 1));
            window.ViewModel.Segments.Add(new MemorySegmentRow("00001010", "00001010", 1));
            window.Measure(new Size(1100, 760));
            window.Arrange(new Rect(0, 0, 1100, 760));
            window.UpdateLayout();
            var segmentGrid = (DataGrid)window.FindName("SegmentsGrid");
            alignment = segmentGrid.HorizontalAlignment;
            columnsAreAuto = segmentGrid.Columns.All(column => column.Width.IsAuto);
            sortingDisabled = !segmentGrid.CanUserSortColumns;
            rowHeight = segmentGrid.RowHeight;
            renderedGap = MeasureTextGap(segmentGrid);
            window.Close();
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();

        Assert.AreEqual(HorizontalAlignment.Left, alignment);
        Assert.IsTrue(columnsAreAuto,
            "Segment columns should size to their displayed address and count content.");
        Assert.IsTrue(sortingDisabled, "Segment order should remain stable when a header is clicked.");
        Assert.AreEqual(20d, rowHeight, "Compact segment rows should retain readable text and selection targets.");
        Assert.IsTrue(renderedGap is >= 0.35 and <= 0.65,
            $"Expected about half a character of rendered text-to-text gap, got {renderedGap:F2}.");
    }

    private static double MeasureTextGap(DataGrid grid)
    {
        var firstRow = (DataGridRow)grid.ItemContainerGenerator.ContainerFromIndex(0);
        var secondRow = (DataGridRow)grid.ItemContainerGenerator.ContainerFromIndex(1);
        Assert.IsNotNull(firstRow);
        Assert.IsNotNull(secondRow);
        var firstText = Descendants<TextBlock>(firstRow).First();
        var secondText = Descendants<TextBlock>(secondRow).First();
        var firstBounds = firstText.TransformToAncestor(grid).TransformBounds(new Rect(firstText.RenderSize));
        var secondBounds = secondText.TransformToAncestor(grid).TransformBounds(new Rect(secondText.RenderSize));
        return (secondBounds.Top - firstBounds.Bottom) / firstText.FontSize;
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
