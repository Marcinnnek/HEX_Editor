using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using HexEditor.Core;
using HexEditor.UI;

namespace HexEditor.Tests;

[TestClass]
[DoNotParallelize]
public sealed class EditorSegmentListLayoutTests
{
    [TestMethod]
    public void EditorBlocksAreOneLineOrderedDescriptionsWithHorizontalScrollingAndExtendedSelection()
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try
            {
                var window = new MainWindow { Width = 360 };
                window.ViewModel.LoadImage(MemoryImage.FromSegments([
                    new MemorySegment(0x08002000, [0x01]),
                    new MemorySegment(0x08000000, new byte[0x15BC])]), null, null);
                window.Show();
                window.UpdateLayout();

                var blocks = (ListBox)window.FindName("SegmentsGrid");
                Assert.AreEqual(SelectionMode.Extended, blocks.SelectionMode);
                Assert.AreEqual(ScrollBarVisibility.Auto, ScrollViewer.GetVerticalScrollBarVisibility(blocks));
                var horizontalScroll = Descendants<ScrollViewer>(blocks).Single();
                Assert.AreEqual(ScrollBarVisibility.Auto, ScrollViewer.GetHorizontalScrollBarVisibility(blocks));
                Assert.AreEqual("Block 0 Starts at: 0x08000000 Ends at: 0x080015BB (Length: 0x15BC = 5564)",
                    ((MemorySegmentRow)blocks.Items[0]).Description);
                Assert.AreEqual("Block 1 Starts at: 0x08002000 Ends at: 0x08002000 (Length: 0x1 = 1)",
                    ((MemorySegmentRow)blocks.Items[1]).Description);

                var firstRow = (ListBoxItem)blocks.ItemContainerGenerator.ContainerFromIndex(0);
                var text = Descendants<TextBlock>(firstRow).Single();
                Assert.AreEqual(TextWrapping.NoWrap, text.TextWrapping);
                Assert.AreEqual(((MemorySegmentRow)blocks.Items[0]).Description, text.Text);
                Assert.IsGreaterThan(0, horizontalScroll.ScrollableWidth,
                    "The full one-line descriptions should remain reachable when the Editor is narrow.");
                blocks.SelectedIndex = 1;
                window.UpdateLayout();
                var rowsScroll = Descendants<ScrollViewer>(window.FindName("HexRowsView") as DependencyObject ?? window)
                    .First(viewer => ScrollViewer.GetCanContentScroll(viewer));
                Assert.IsGreaterThan(0, rowsScroll.VerticalOffset,
                    "Selecting a Block should scroll the data view to its start address.");
                blocks.SelectAll();
                Assert.AreEqual(2, blocks.SelectedItems.Count);
                window.Close();
            }
            catch (Exception exception) { failure = exception; }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
        if (failure is not null) throw failure;
    }

    [TestMethod]
    public void DoubleClickingAnEditorBlockOpensItsRelocationDialog()
    {
        Exception? failure = null;
        var openedRelocation = false;
        var thread = new Thread(() =>
        {
            try
            {
                var window = new MainWindow();
                window.ViewModel.LoadImage(MemoryImage.FromSegments([new MemorySegment(0x1000, [0x01])]), null, null);
                window.Show();
                window.UpdateLayout();
                var blocks = (ListBox)window.FindName("SegmentsGrid");
                blocks.SelectedIndex = 0;
                var item = (ListBoxItem)blocks.ItemContainerGenerator.ContainerFromIndex(0);
                var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(50) };
                timer.Tick += (_, _) =>
                {
                    var dialog = window.OwnedWindows.OfType<RelocateSegmentDialog>().FirstOrDefault();
                    if (dialog is null) return;
                    openedRelocation = true;
                    timer.Stop();
                    dialog.Close();
                };
                timer.Start();
                blocks.RaiseEvent(new MouseButtonEventArgs(Mouse.PrimaryDevice, Environment.TickCount, MouseButton.Left)
                {
                    RoutedEvent = Control.MouseDoubleClickEvent,
                    Source = item
                });
                timer.Stop();
                window.Close();
            }
            catch (Exception exception) { failure = exception; }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
        if (failure is not null) throw failure;
        Assert.IsTrue(openedRelocation, "A Block double-click should open the relocation workflow.");
    }

    [TestMethod]
    public void EditorBlockListDisplaysAndNavigatesAFullLogicalRange()
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try
            {
                var window = new MainWindow();
                window.ViewModel.LoadImage(MemoryImage.FromRepeatedRange(0, uint.MaxValue, 0xA5), null, null);
                window.Show();
                window.UpdateLayout();

                var blocks = (ListBox)window.FindName("SegmentsGrid");
                Assert.HasCount(1, blocks.Items);
                var block = (MemorySegmentRow)blocks.Items[0];
                Assert.AreEqual("Block 0 Starts at: 0x00000000 Ends at: 0xFFFFFFFF (Length: 0x100000000 = 4294967296)",
                    block.Description);
                Assert.IsTrue(window.ViewModel.ImageView.TryNavigateToAddress(uint.MaxValue, out var rowIndex));
                Assert.AreEqual("FFFFFFF0", window.ViewModel.ImageView.Rows[rowIndex].Address);
                Assert.AreEqual("A5", window.ViewModel.ImageView.Rows[rowIndex].Cells[15].Hex);

                blocks.SelectedIndex = 0;
                Assert.AreEqual("00000000", window.ViewModel.ImageView.Rows[0].Address);
                window.Close();
            }
            catch (Exception exception) { failure = exception; }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
        if (failure is not null) throw failure;
    }

    [TestMethod]
    public void EditorBlockListCombinesAdjacentBackingRangesAndUsesZeroBasedNumber()
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try
            {
                var first = MemoryImage.FromRepeatedRange(0x1000, 0x1000 + 600_000 - 1, 0xA1).Ranges[0];
                var second = MemoryImage.FromRepeatedRange(0x1000 + 600_000, 0x1000 + 1_200_000 - 1, 0xB2).Ranges[0];
                var window = new MainWindow();
                window.ViewModel.LoadImage(MemoryImage.FromRanges([first, second]), null, null);
                window.Show();
                window.UpdateLayout();

                var blocks = (ListBox)window.FindName("SegmentsGrid");
                Assert.HasCount(1, blocks.Items);
                Assert.AreEqual("Block 0 Starts at: 0x00001000 Ends at: 0x00125F7F (Length: 0x124F80 = 1200000)",
                    ((MemorySegmentRow)blocks.Items[0]).Description);
                blocks.SelectedIndex = 0;
                Assert.AreEqual("00001000", window.ViewModel.ImageView.Rows[0].Address);
                window.Close();
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

}
