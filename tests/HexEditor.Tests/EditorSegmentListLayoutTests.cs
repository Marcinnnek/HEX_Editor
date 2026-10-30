using System.Windows;
using System.Windows.Controls;
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
        var thread = new Thread(() =>
        {
            var window = new MainWindow();
            var segmentGrid = (DataGrid)window.FindName("SegmentsGrid");
            alignment = segmentGrid.HorizontalAlignment;
            columnsAreAuto = segmentGrid.Columns.All(column => column.Width.IsAuto);
            window.Close();
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();

        Assert.AreEqual(HorizontalAlignment.Left, alignment);
        Assert.IsTrue(columnsAreAuto,
            "Segment columns should size to their displayed address and count content.");
    }

}
