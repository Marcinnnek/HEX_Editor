using HexEditor.UI;

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
}
