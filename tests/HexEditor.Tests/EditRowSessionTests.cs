using HexEditor.Core;
using HexEditor.UI;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace HexEditor.Tests;

[TestClass]
public class EditRowSessionTests
{
    [TestMethod]
    [DataRow(8)]
    [DataRow(16)]
    [DataRow(32)]
    [DataRow(1)]
    [DataRow(3)]
    [DataRow(24)]
    [DataRow(80)]
    [DataRow(128)]
    public void EditRowUsesSelectedRowWidthAndCancelPreservesSparseImage(int bytesPerRow)
    {
        var workspace = new EditorDocumentWorkspace();
        var rowStartAddress = (uint)(0x1000 / bytesPerRow * bytesPerRow);
        var lastAddress = rowStartAddress + (uint)bytesPerRow - 1;
        var segments = bytesPerRow == 1
            ? new[] { new MemorySegment(rowStartAddress, new byte[] { 0x5A }) }
            : new[] { new MemorySegment(rowStartAddress, new byte[] { 0x41 }), new MemorySegment(lastAddress, new byte[] { 0x5A }) };
        workspace.OpenImage(MemoryImage.FromSegments(segments), null, null);

        var edit = workspace.BeginEditRow(rowStartAddress, lastAddress, bytesPerRow);

        Assert.HasCount(bytesPerRow, edit.Cells);
        Assert.AreEqual("00", edit.Cells[0].OffsetText);
        Assert.AreEqual((bytesPerRow - 1).ToString("X2"), edit.Cells[^1].OffsetText);
        Assert.AreEqual(lastAddress, edit.SelectedAddress);
        Assert.AreEqual("Z", edit.Cells[^1].Ascii);
        if (bytesPerRow > 1)
        {
            Assert.IsFalse(edit.Cells[1].IsOccupied);
            Assert.IsFalse(edit.SetHex(rowStartAddress + 1, "20"));
        }
        Assert.IsTrue(edit.SetHex(lastAddress, "21"));
        edit.Cancel();
        Assert.IsFalse(workspace.Document.IsModified);
        if (bytesPerRow > 1) Assert.IsFalse(workspace.Document.CurrentImage.TryGetByte(rowStartAddress + 1, out _));
        Assert.IsTrue(workspace.Document.CurrentImage.TryGetByte(lastAddress, out var unchanged));
        Assert.AreEqual((byte)'Z', unchanged);
    }

    [TestMethod]
    public void EditRowStagesPrintableAsciiAndHexAsOneUndoableChange()
    {
        var workspace = new EditorDocumentWorkspace();
        workspace.OpenImage(MemoryImage.FromSegments([new MemorySegment(0x1000, [0x41, 0x00, 0x7E])]), null, null);
        var edit = workspace.BeginEditRow(0x1000, 0x1002, 8);
        Assert.AreEqual("A", edit.Cells[0].Ascii);
        Assert.AreEqual(".", edit.Cells[1].Ascii);
        Assert.IsTrue(edit.SetAscii(0x1001, "x"));
        Assert.IsTrue(edit.SetAscii(0x1000, "B"));
        Assert.IsTrue(edit.SetHex(0x1002, "20"));
        Assert.IsTrue(edit.Commit());
        Assert.IsTrue(workspace.Document.IsModified);
        Assert.IsTrue(workspace.Document.CurrentImage.TryGetByte(0x1000, out var first));
        Assert.AreEqual((byte)'B', first);
        Assert.IsTrue(workspace.Document.CurrentImage.TryGetByte(0x1001, out var second));
        Assert.AreEqual((byte)'x', second);
        Assert.IsTrue(workspace.Undo());
        Assert.AreEqual((byte)'A', workspace.Document.CurrentImage.Segments[0].Data.Span[0]);
        Assert.IsTrue(workspace.Redo());
        Assert.AreEqual((byte)0x20, workspace.Document.CurrentImage.Segments[0].Data.Span[2]);
    }

    [TestMethod]
    public void CancelAndInvalidHexLeaveImageAndDirtyStateUnchangedAndGapsStayAbsent()
    {
        var workspace = new EditorDocumentWorkspace();
        workspace.OpenImage(MemoryImage.FromSegments([new MemorySegment(0x20, [0x41]), new MemorySegment(0x22, [0x43])]), null, null);
        var edit = workspace.BeginEditRow(0x20, 0x20, 8);
        Assert.IsFalse(edit.Cells[1].IsOccupied);
        Assert.IsFalse(edit.SetHex(0x20, "GG"));
        Assert.IsFalse(edit.Commit());
        edit.Cancel();
        Assert.IsFalse(workspace.Document.IsModified);
        Assert.IsFalse(workspace.Document.CurrentImage.TryGetByte(0x21, out _));
        Assert.AreEqual((byte)'A', workspace.Document.CurrentImage.Segments[0].Data.Span[0]);
    }

    [TestMethod]
    public void EditRowAcceptsOneHexDigitAsLowNibbleAndShowsZeroBasedOffsets()
    {
        var workspace = new EditorDocumentWorkspace();
        workspace.OpenImage(MemoryImage.FromSegments([new MemorySegment(0x1000, [0x41, 0x42, 0x43])]), null, null);
        var edit = workspace.BeginEditRow(0x1000, 0x1002, 8);

        Assert.AreEqual("00", edit.Cells[0].OffsetText);
        Assert.AreEqual("01", edit.Cells[1].OffsetText);
        Assert.IsTrue(edit.SetHex(0x1001, "A"));
        Assert.IsTrue(edit.Commit());
        Assert.IsTrue(workspace.Document.CurrentImage.TryGetByte(0x1001, out var changed));
        Assert.AreEqual((byte)0x0A, changed);
    }
}
