using HexEditor.Core;
using HexEditor.UI;
using System.Windows;

namespace HexEditor.Tests;

[TestClass]
public sealed class CompareRowActivationTests
{
    [TestMethod]
    public void OccupiedBaseRow_RecognizesAddressWithoutChangingEitherImage()
    {
        var left = MemoryImage.FromSegments([new MemorySegment(0x1001, [0x10, 0x00])]);
        var right = MemoryImage.FromSegments([new MemorySegment(0x1001, [0x20])]);
        var workspace = new CompareWorkspaceTabViewModel(left, right, "left.hex", "right.hex");

        var result = workspace.ActivateRow(ComparePanelSide.Base, 0, 0x1002);

        Assert.AreEqual(CompareRowActivationKind.EditableRow, result.Kind);
        Assert.AreEqual(0x1002u, result.Address);
        var addressColumnResult = workspace.ActivateRow(ComparePanelSide.Base, 0, null);
        Assert.AreEqual(CompareRowActivationKind.EditableRow, addressColumnResult.Kind);
        Assert.AreEqual(0x1001u, addressColumnResult.Address);
        Assert.AreSame(left, workspace.Comparison.FileA);
        Assert.AreSame(right, workspace.Comparison.FileB);
    }

    [TestMethod]
    public void ReferencePanelActivation_ExplainsReadOnlyBehaviorWithoutChangingImages()
    {
        var left = MemoryImage.FromSegments([new MemorySegment(0x1000, [0x10])]);
        var right = MemoryImage.FromSegments([new MemorySegment(0x1000, [0x20])]);
        var workspace = new CompareWorkspaceTabViewModel(left, right, "left.hex", "right.hex");

        var result = workspace.ActivateRow(ComparePanelSide.Reference, -1, null);

        Assert.AreEqual(CompareRowActivationKind.ReferenceReadOnly, result.Kind);
        Assert.AreEqual("The reference image is read-only.", result.StatusMessage);
        Assert.AreSame(left, workspace.Comparison.FileA);
        Assert.AreSame(right, workspace.Comparison.FileB);
    }

    [TestMethod]
    public void InvalidBaseTargets_AreIgnoredWithoutChangingImages()
    {
        var left = MemoryImage.FromSegments([
            new MemorySegment(0x1000, [0x10]),
            new MemorySegment(0x1100, [0x20])]);
        var right = MemoryImage.FromSegments([new MemorySegment(0x1080, [0x30])]);
        var workspace = new CompareWorkspaceTabViewModel(left, right, "left.hex", "right.hex");
        var rows = workspace.Comparison.FileARows;
        var gapIndex = Enumerable.Range(0, rows.Count).First(index => rows[index].IsGapMarker);
        var referenceOnlyIndex = Enumerable.Range(0, rows.Count).First(index =>
            rows[index].Cells.Any(cell => cell.Address == 0x1080));

        var targets = new (int RowIndex, uint? Address)[]
        {
            (-1, null), // header
            (rows.Count, null), // empty or unrealized row
            (gapIndex, null), // address gap marker
            (0, 0x1001), // absent byte in a partial row
            (0, 0x1100), // address from another row
            (referenceOnlyIndex, 0x1080) // no base byte at this address
        };

        foreach (var (rowIndex, address) in targets)
        {
            var result = workspace.ActivateRow(ComparePanelSide.Base, rowIndex, address);
            Assert.AreEqual(CompareRowActivationKind.Ignored, result.Kind);
            Assert.IsNull(result.Address);
            Assert.IsNull(result.StatusMessage);
        }

        Assert.AreSame(left, workspace.Comparison.FileA);
        Assert.AreSame(right, workspace.Comparison.FileB);
    }

    [TestMethod]
    public void EditingActivatedBaseRow_UsesSharedDocumentAndRefreshesEveryComparison()
    {
        var workspace = new EditorDocumentWorkspace();
        var baseImage = MemoryImage.FromSegments([new MemorySegment(0x1000, [0x10, 0x20])]);
        workspace.OpenImage(baseImage, "base.hex", null);
        var document = workspace.Document;
        var first = new CompareWorkspaceTabViewModel(baseImage,
            MemoryImage.FromSegments([new MemorySegment(0x1000, [0x10, 0x20])]), "base.hex", "ref-a.hex", baseDocument: document);
        var second = new CompareWorkspaceTabViewModel(baseImage,
            MemoryImage.FromSegments([new MemorySegment(0x1000, [0x10, 0x30])]), "base.hex", "ref-b.hex", baseDocument: document);
        workspace.RegisterComparisonView(first);
        workspace.RegisterComparisonView(second);

        var activation = first.ActivateRow(ComparePanelSide.Base, 0, 0x1001);
        Assert.AreEqual(CompareRowActivationKind.EditableRow, activation.Kind);
        var edit = workspace.BeginEditRow(first.BaseDocument!, 0x1000, activation.Address!.Value, first.Comparison.BytesPerRow);
        Assert.IsTrue(edit.SetHex(0x1001, "30"));
        Assert.IsTrue(edit.Commit());

        Assert.AreEqual((byte)0x30, ReadByte(document.CurrentImage, 0x1001));
        Assert.AreSame(document.CurrentImage, first.Comparison.FileA);
        Assert.AreSame(document.CurrentImage, second.Comparison.FileA);
        Assert.AreEqual(1, first.Comparison.Rows[0].States.Count(state => state == HexEditor.Compare.CompareByteState.Different));
        Assert.AreEqual(0, second.Comparison.Rows[0].States.Count(state => state == HexEditor.Compare.CompareByteState.Different));
        Assert.IsTrue(document.IsModified);
        Assert.IsTrue(document.Editing.Undo());
        Assert.AreEqual(0, first.Comparison.Rows[0].States.Count(state => state == HexEditor.Compare.CompareByteState.Different));
        Assert.AreEqual(1, second.Comparison.Rows[0].States.Count(state => state == HexEditor.Compare.CompareByteState.Different));
        Assert.IsTrue(document.Editing.Redo());
        Assert.AreEqual(1, first.Comparison.Rows[0].States.Count(state => state == HexEditor.Compare.CompareByteState.Different));
    }

    [TestMethod]
    public void RemovingComparedBaseByteLeavesOneSidedDifferenceWithoutEditableCell()
    {
        var baseImage = MemoryImage.FromSegments([new MemorySegment(0x1000, [0x10])]);
        var reference = MemoryImage.FromSegments([new MemorySegment(0x1000, [0x20])]);
        var owner = new MainViewModel();
        owner.LoadImage(baseImage, null, null);
        var compare = new CompareWorkspaceTabViewModel(baseImage, reference, "base.hex", "reference.hex",
            baseDocument: owner.Workspace.Document);

        owner.Workspace.Document.Editing.DeleteSegment(0);

        Assert.AreEqual(HexEditor.Compare.CompareByteState.OnlyB, compare.Comparison.Rows[0].States[0]);
        Assert.AreEqual(ByteHighlightState.OnlyInB, compare.Comparison.FileARows[0].Cells[0].Highlight);
        Assert.IsFalse(compare.Comparison.FileARows[0].Cells[0].IsEdited);
        Assert.AreEqual(CompareRowActivationKind.Ignored,
            compare.ActivateRow(ComparePanelSide.Base, 0, 0x1000).Kind);
        Assert.AreEqual(CompareRowActivationKind.ReferenceReadOnly,
            compare.ActivateRow(ComparePanelSide.Reference, 0, 0x1000).Kind);
    }

    private static byte ReadByte(MemoryImage image, uint address)
    {
        Assert.IsTrue(image.TryGetByte(address, out var value));
        return value;
    }

    [STATestMethod]
    public void HexByteHitTesting_RejectsSeparatorsAndOutsideRow()
    {
        var row = new HexViewRow("00001000", "10 20", "..", false, null)
        {
            Cells = [new HexByteCell("10", ".", 0x1000, ByteHighlightState.None), new HexByteCell("20", ".", 0x1001, ByteHighlightState.None)]
        };
        var control = new HexByteRowControl { Row = row };
        control.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        var byteWidth = (control.DesiredSize.Width - GridSpacingSettings.Current.HorizontalByteSpacing) / 2;
        var separator = byteWidth + GridSpacingSettings.Current.HorizontalByteSpacing / 2d;

        Assert.IsTrue(control.TryGetAddressAt(new Point(byteWidth * 0.1, 1), out var first));
        Assert.AreEqual(0x1000u, first);
        Assert.IsFalse(control.TryGetAddressAt(new Point(separator, 1), out _));
        Assert.IsTrue(control.TryGetAddressAt(new Point(byteWidth + GridSpacingSettings.Current.HorizontalByteSpacing + byteWidth * 0.1, 1), out var second));
        Assert.AreEqual(0x1001u, second);
        Assert.IsFalse(control.TryGetAddressAt(new Point(control.DesiredSize.Width, 1), out _));
        Assert.IsFalse(control.TryGetAddressAt(new Point(byteWidth * 0.1, control.DesiredSize.Height + 0.5), out _));
        Assert.IsFalse(control.TryGetAddressAt(new Point(0, control.DesiredSize.Height), out _));
    }
}
