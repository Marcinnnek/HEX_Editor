using HexEditor.Core;
using HexEditor.UI;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace HexEditor.Tests;

[TestClass]
public class EditRowSessionTests
{
    [TestMethod]
    public void EditRowStagesPrintableAsciiAndHexAsOneUndoableChange()
    {
        var workspace = new EditorDocumentWorkspace();
        workspace.OpenImage(MemoryImage.FromSegments([new MemorySegment(0x1000, [0x41, 0x00, 0x7E])]), null, null);
        var edit = workspace.BeginEditRow(0x1000, 0x1002, 8);
        Assert.AreEqual("A", edit.Cells[0].Ascii);
        Assert.AreEqual(".", edit.Cells[1].Ascii);
        Assert.IsFalse(edit.SetAscii(0x1001, "x"));
        Assert.IsTrue(edit.SetHex(0x1001, "00"));
        Assert.IsTrue(edit.SetAscii(0x1000, "B"));
        Assert.IsTrue(edit.SetHex(0x1002, "20"));
        Assert.IsTrue(edit.Commit());
        Assert.IsTrue(workspace.Document.IsModified);
        Assert.IsTrue(workspace.Document.CurrentImage.TryGetByte(0x1000, out var first));
        Assert.AreEqual((byte)'B', first);
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
}
