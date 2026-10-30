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
    public void CompareBlocksStayWithTheirImagesAndSupportHorizontalScrollingAndSelection(int width)
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try
            {
                var workspace = new CompareWorkspaceTabViewModel(
                    MemoryImage.FromSegments([new MemorySegment(0x1000, [0x10]), new MemorySegment(0x1010, new byte[0x15BC])]),
                    MemoryImage.FromSegments([new MemorySegment(0x2000, [0x20]), new MemorySegment(0x2010, new byte[0x15BC])]),
                    "base.hex", "reference.hex");
                var view = new CompareView(workspace);
                view.Measure(new Size(width, 650));
                view.Arrange(new Rect(0, 0, width, 650));
                view.UpdateLayout();

                var segmentLists = Descendants<ListBox>(view)
                    .Where(list => ReferenceEquals(list.ItemsSource, workspace.BlocksA) ||
                                   ReferenceEquals(list.ItemsSource, workspace.BlocksB))
                    .ToArray();
                var dataPanels = Descendants<HexViewControl>(view).ToArray();
                Assert.HasCount(2, segmentLists);
                Assert.HasCount(2, dataPanels);
                var baseBlocks = segmentLists.Single(list => ReferenceEquals(list.ItemsSource, workspace.BlocksA));
                var referenceBlocks = segmentLists.Single(list => ReferenceEquals(list.ItemsSource, workspace.BlocksB));
                Assert.AreEqual(SelectionMode.Extended, baseBlocks.SelectionMode);
                Assert.AreEqual(SelectionMode.Single, referenceBlocks.SelectionMode);
                Assert.AreEqual("Block 0 Starts at: 0x00001000 Ends at: 0x00001000 (Length: 0x1 = 1)",
                    ((CompareMemoryBlockRow)baseBlocks.Items[0]).Description);
                Assert.AreEqual("Block 1 Starts at: 0x00001010 Ends at: 0x000025CB (Length: 0x15BC = 5564)",
                    ((CompareMemoryBlockRow)baseBlocks.Items[1]).Description);

                foreach (var list in segmentLists)
                {
                    Assert.AreEqual(ScrollBarVisibility.Auto, ScrollViewer.GetHorizontalScrollBarVisibility(list));
                    var horizontalScroll = Descendants<ScrollViewer>(list).Single();
                    Assert.IsGreaterThan(0, horizontalScroll.ScrollableWidth,
                        "The full one-line Block description should remain reachable in a narrow Compare column.");
                    var firstRow = (ListBoxItem)list.ItemContainerGenerator.ContainerFromIndex(0);
                    var text = Descendants<TextBlock>(firstRow).Single();
                    Assert.AreEqual(TextWrapping.NoWrap, text.TextWrapping);
                    Assert.AreEqual(((CompareMemoryBlockRow)list.Items[0]).Description, text.Text);
                }

                AssertAligned(baseBlocks, dataPanels.Single(panel => ReferenceEquals(panel.ItemsSource, workspace.Comparison.FileARows)));
                AssertAligned(referenceBlocks, dataPanels.Single(panel => ReferenceEquals(panel.ItemsSource, workspace.Comparison.FileBRows)));

                baseBlocks.SelectedIndex = 1;
                Assert.AreEqual(0x1010u, ParseSelectedAddress(view));
                Assert.HasCount(1, baseBlocks.SelectedItems);
                AssertPanelsNavigateTo("00001010");
                baseBlocks.SelectAll();
                Assert.HasCount(2, view.SelectedBaseSegmentIndexes,
                    "Ctrl/Shift selection in the base list supplies multiple segment indexes for export.");

                referenceBlocks.SelectedIndex = 1;
                Assert.AreEqual(0x2010u, ParseSelectedAddress(view));
                Assert.HasCount(1, referenceBlocks.SelectedItems);
                AssertPanelsNavigateTo("00002010");

                uint ParseSelectedAddress(CompareView compareView) =>
                    HexAddressParser.Parse(compareView.FindName("AddressBox") is TextBox box ? box.Text : "");

                void AssertPanelsNavigateTo(string expectedAddress)
                {
                    foreach (var panel in dataPanels)
                    {
                        Assert.IsInstanceOfType<HexViewRow>(panel.ItemsGrid.SelectedItem);
                        Assert.AreEqual(expectedAddress, ((HexViewRow)panel.ItemsGrid.SelectedItem).Address);
                    }
                }

                void AssertAligned(ListBox list, HexViewControl panel)
                {
                    var group = Ancestor<GroupBox>(list);
                    Assert.IsNotNull(group);
                    var listBounds = group.TransformToAncestor(view).TransformBounds(new Rect(group.RenderSize));
                    var panelBounds = panel.TransformToAncestor(view).TransformBounds(new Rect(panel.RenderSize));
                    Assert.AreEqual(panelBounds.Left, listBounds.Left, 1.0);
                    Assert.AreEqual(panelBounds.Width, listBounds.Width, 1.0);
                    Assert.IsLessThanOrEqualTo(panelBounds.Top, listBounds.Bottom);
                    Assert.IsLessThanOrEqualTo(12, panelBounds.Top - listBounds.Bottom);
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
