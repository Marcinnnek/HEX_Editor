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
