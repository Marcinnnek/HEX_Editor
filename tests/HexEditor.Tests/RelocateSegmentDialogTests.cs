using HexEditor.UI;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;

namespace HexEditor.Tests;

[TestClass]
public sealed class RelocateSegmentDialogTests
{
    [TestMethod]
    public void Dialog_PrefillsDestinationWithSelectedSegmentStart()
    {
        string? destination = null;
        var thread = new Thread(() =>
        {
            var dialog = new RelocateSegmentDialog(0x1234ABCD, 0x1234ABDF, 19);
            destination = dialog.DestinationAddressText;
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();

        Assert.AreEqual("1234ABCD", destination);
    }

    [TestMethod]
    public void Dialog_DeleteConfirmsTheOriginalBlockRangeAndLength()
    {
        Exception? failure = null;
        string? confirmation = null;
        bool? result = null;
        var thread = new Thread(() =>
        {
            try
            {
                var dialog = new RelocateSegmentDialog(0x1234ABCD, 0x1234ABDF, 19, canDelete: true,
                    confirmDelete: message => { confirmation = message; return true; });
                dialog.Loaded += (_, _) =>
                {
                    FindTextBoxes(dialog).Single().Text = "DEADBEEF";
                    var delete = FindButtons(dialog).Single(button => Equals(button.Content, "Delete"));
                    delete.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                };
                result = dialog.ShowDialog();
                Assert.IsTrue(dialog.DeleteRequested);
            }
            catch (Exception exception) { failure = exception; }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
        if (failure is not null) throw failure;
        Assert.IsTrue(result);
        StringAssert.Contains(confirmation, "0x1234ABCD");
        StringAssert.Contains(confirmation, "0x1234ABDF");
        StringAssert.Contains(confirmation, "19");
    }

    [TestMethod]
    public void Dialog_DecliningDeleteKeepsTheDialogOpenWithoutRequestingDeletion()
    {
        Exception? failure = null;
        bool? result = true;
        var thread = new Thread(() =>
        {
            try
            {
                var dialog = new RelocateSegmentDialog(0x1000, 0x1001, 2,
                    confirmDelete: _ => false);
                dialog.Loaded += (_, _) =>
                {
                    FindButtons(dialog).Single(button => Equals(button.Content, "Delete"))
                        .RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                    Assert.IsFalse(dialog.DeleteRequested);
                    dialog.Close();
                };
                result = dialog.ShowDialog();
            }
            catch (Exception exception) { failure = exception; }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
        if (failure is not null) throw failure;
        Assert.AreNotEqual(true, result, "Declining confirmation should not complete the relocation dialog.");
    }

    [TestMethod]
    public void Dialog_HidesDeleteWhenMoreThanOneBlockIsSelected()
    {
        Exception? failure = null;
        var deleteAvailable = true;
        var thread = new Thread(() =>
        {
            try
            {
                var dialog = new RelocateSegmentDialog(0x1000, 0x1001, 2, canDelete: false);
                dialog.Loaded += (_, _) =>
                {
                    deleteAvailable = FindButtons(dialog).Any(button => Equals(button.Content, "Delete"));
                    dialog.Close();
                };
                dialog.ShowDialog();
            }
            catch (Exception exception) { failure = exception; }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
        if (failure is not null) throw failure;
        Assert.IsFalse(deleteAvailable);
    }

    private static IEnumerable<Button> FindButtons(DependencyObject parent)
    {
        for (var index = 0; index < System.Windows.Media.VisualTreeHelper.GetChildrenCount(parent); index++)
        {
            var child = System.Windows.Media.VisualTreeHelper.GetChild(parent, index);
            if (child is Button button) yield return button;
            foreach (var nested in FindButtons(child)) yield return nested;
        }
    }

    private static IEnumerable<TextBox> FindTextBoxes(DependencyObject parent)
    {
        for (var index = 0; index < System.Windows.Media.VisualTreeHelper.GetChildrenCount(parent); index++)
        {
            var child = System.Windows.Media.VisualTreeHelper.GetChild(parent, index);
            if (child is TextBox textBox) yield return textBox;
            foreach (var nested in FindTextBoxes(child)) yield return nested;
        }
    }
}
