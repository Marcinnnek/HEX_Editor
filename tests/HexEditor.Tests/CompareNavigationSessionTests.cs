using HexEditor.Core;
using HexEditor.UI;

namespace HexEditor.Tests;

[TestClass]
public sealed class CompareNavigationSessionTests
{
    [TestMethod]
    public void SelectedByteTakesPriorityOverDraftUntilScrollingChangesTheCursor()
    {
        var session = CreateSession();
        session.SelectByte(ComparePanelSide.Reference, 0x1005);
        session.SetAddressDraft("not an address");

        Assert.AreEqual((ComparePanelSide.Reference, 0x1005u),
            session.CurrentCursor(0x1000, 16, windowed: true));

        session.ScrollToFirstRow(0x1000);
        Assert.AreEqual("00001000", session.AddressText);
        session.SetAddressDraft("1006");
        Assert.AreEqual((ComparePanelSide.Reference, 0x1006u),
            session.CurrentCursor(0x1000, 16, windowed: true));
    }

    [TestMethod]
    public void DifferenceNavigationUsesDraftAsAnchorAndKeepsStateOnFailure()
    {
        var session = CreateSession();
        session.SetAddressDraft("1000");

        var found = session.NavigateDifference(forward: true, visibleRowAddress: null, 16, windowed: true);
        Assert.AreEqual(CompareDifferenceNavigationKind.Found, found.Kind);
        Assert.AreEqual((ComparePanelSide.Base, 0x1002u),
            session.CurrentCursor(null, 16, windowed: true));
        Assert.AreEqual("00001002", session.AddressText);

        var missing = session.NavigateDifference(forward: true, visibleRowAddress: null, 16, windowed: true);
        Assert.AreEqual(CompareDifferenceNavigationKind.NoDifference, missing.Kind);
        Assert.AreEqual("00001002", session.AddressText);

        session.ScrollToFirstRow(null);
        session.SetAddressDraft("invalid");
        var invalid = session.NavigateDifference(forward: false, visibleRowAddress: null, 16, windowed: true);
        Assert.AreEqual(CompareDifferenceNavigationKind.InvalidAddress, invalid.Kind);
        Assert.AreEqual("invalid", session.AddressText);
    }

    [TestMethod]
    public void RowWidthChangePreservesSelectedAddressAndSide()
    {
        var comparison = CreateComparison();
        var session = new CompareNavigationSession(comparison);
        session.SelectByte(ComparePanelSide.Reference, 0x1005);
        session.BeginRowWidthChange(0x1000, 16, windowed: true);

        comparison.BytesPerRow = 24;
        Assert.IsTrue(session.FinishRowWidthChange(out var rowIndex));
        Assert.IsGreaterThanOrEqualTo(rowIndex, 0);
        Assert.AreEqual((ComparePanelSide.Reference, 0x1005u),
            session.CurrentCursor(0x1000, 24, windowed: true));
    }

    [TestMethod]
    public void GoRejectsInvalidAddressWithoutChangingSelectedByte()
    {
        var session = CreateSession();
        session.SelectByte(ComparePanelSide.Base, 0x1000);
        session.SetAddressDraft("invalid");

        Assert.IsFalse(session.TryGo(out _));
        Assert.AreEqual((ComparePanelSide.Base, 0x1000u),
            session.CurrentCursor(0x1000, 16, windowed: true));

        session.SetAddressDraft("1005");
        Assert.IsTrue(session.TryGo(out var rowIndex));
        Assert.IsGreaterThanOrEqualTo(rowIndex, 0);
        Assert.AreEqual((ComparePanelSide.Base, 0x1005u),
            session.CurrentCursor(0x1000, 16, windowed: true));
    }

    private static CompareNavigationSession CreateSession() => new(CreateComparison());

    private static CompareViewModel CreateComparison() => new(
        MemoryImage.FromSegments([new MemorySegment(0x1000, [0x10, 0x20, 0x30, 0x40, 0x50, 0x60])]),
        MemoryImage.FromSegments([new MemorySegment(0x1000, [0x11, 0x20, 0x31, 0x40, 0x50, 0x60])]));
}
