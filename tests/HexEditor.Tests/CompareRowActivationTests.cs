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
        Assert.AreEqual(0, first.Comparison.Rows[0].States.Count(state => state == HexEditor.Compare.CompareByteState.Different));
        Assert.AreEqual(1, second.Comparison.Rows[0].States.Count(state => state == HexEditor.Compare.CompareByteState.Different));
        Assert.IsTrue(document.IsModified);
        Assert.IsTrue(document.Editing.Undo());
        Assert.AreEqual(1, first.Comparison.Rows[0].States.Count(state => state == HexEditor.Compare.CompareByteState.Different));
        Assert.AreEqual(0, second.Comparison.Rows[0].States.Count(state => state == HexEditor.Compare.CompareByteState.Different));
        Assert.IsTrue(document.Editing.Redo());
        Assert.AreEqual(0, first.Comparison.Rows[0].States.Count(state => state == HexEditor.Compare.CompareByteState.Different));
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
            Cells = [new HexByteCell("10", ".", 0x1000, ""), new HexByteCell("20", ".", 0x1001, "")]
        };
        var control = new HexByteRowControl { Row = row };
        control.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        var cellWidth = control.DesiredSize.Width / 2;

        Assert.IsTrue(control.TryGetAddressAt(new Point(cellWidth * 0.1, 1), out var first));
        Assert.AreEqual(0x1000u, first);
        Assert.IsFalse(control.TryGetAddressAt(new Point(cellWidth * 0.9, 1), out _));
        Assert.IsTrue(control.TryGetAddressAt(new Point(cellWidth * 1.1, 1), out var second));
        Assert.AreEqual(0x1001u, second);
        Assert.IsFalse(control.TryGetAddressAt(new Point(cellWidth * 2, 1), out _));
        Assert.IsFalse(control.TryGetAddressAt(new Point(0, control.DesiredSize.Height), out _));
    }
}
