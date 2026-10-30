using HexEditor.Core;
using HexEditor.Formats;
using HexEditor.UI;

namespace HexEditor.Tests;

[TestClass]
public sealed class EditorDocumentWorkspaceTests
{
    [TestMethod]
    public void Export_image_writes_a_copy_without_changing_document_association_or_dirty_state()
    {
        var path = Path.Combine(Path.GetTempPath(), "working.hex");
        var image = MemoryImage.FromSegments([new MemorySegment(0x1000, [0x10, 0x20])]);
        var viewModel = new MainViewModel();
        viewModel.LoadImage(image, path, MemoryImageExportFormat.IntelHex);
        viewModel.EditByte(0x1000, 0x11);

        using var output = new MemoryStream();
        viewModel.ExportImage(MemoryImageExportFormat.SRecord, 8, output);

        Assert.AreEqual(path, viewModel.SourcePath);
        Assert.IsTrue(viewModel.IsModified);
        Assert.IsTrue(viewModel.CurrentImage.TryGetByte(0x1000, out var exportedValue));
        Assert.AreEqual((byte)0x11, exportedValue);
        Assert.IsTrue(output.Length > 0);
    }

    [TestMethod]
    public void EditUndoRedoAndSaveAsKeepTheSameDocument()
    {
        var workspace = new EditorDocumentWorkspace();
        workspace.OpenImage(MemoryImage.FromSegments([new MemorySegment(0x1000, [0x10])]),
            null, MemoryImageExportFormat.IntelHex);
        var document = workspace.Document!;
        var originalImage = document.CurrentImage;
        var path = Path.Combine(Path.GetTempPath(), $"hex-editor-document-{Guid.NewGuid():N}.hex");
        try
        {
            workspace.EditByte(0x1000, 0x20, OverlapDecision.OverwriteIncoming);
            Assert.AreSame(document, workspace.Document);
            Assert.AreNotSame(originalImage, document.CurrentImage);
            Assert.IsTrue(document.IsModified);

            Assert.IsTrue(workspace.Undo());
            Assert.AreSame(document, workspace.Document);
            Assert.AreSame(originalImage, document.CurrentImage);
            Assert.IsFalse(document.IsModified);
            Assert.IsTrue(workspace.Redo());
            Assert.IsTrue(document.IsModified);

            workspace.SaveAs(path, MemoryImageExportFormat.IntelHex, overwriteExisting: false);
            Assert.AreSame(document, workspace.Document);
            Assert.AreEqual(Path.GetFullPath(path), document.SourcePath);
            Assert.IsFalse(document.IsModified);
            Assert.IsTrue(document.CanUndo);
        }
        finally { if (File.Exists(path)) File.Delete(path); }
    }

    [TestMethod]
    public void OpenFailureKeepsTheCurrentDocumentAndItsUnsavedBytes()
    {
        var workspace = new EditorDocumentWorkspace();
        workspace.OpenImage(MemoryImage.FromSegments([new MemorySegment(0x1000, [0x10])]),
            null, MemoryImageExportFormat.IntelHex);
        workspace.EditByte(0x1000, 0x20, OverlapDecision.OverwriteIncoming);
        var document = workspace.Document;
        var missingPath = Path.Combine(Path.GetTempPath(), $"hex-editor-missing-{Guid.NewGuid():N}.hex");

        Assert.Throws<FileNotFoundException>(() => workspace.Open(missingPath, OverlapDecision.OverwriteIncoming));

        Assert.AreSame(document, workspace.Document);
        Assert.IsTrue(document.IsModified);
        Assert.IsTrue(document.CurrentImage.TryGetByte(0x1000, out var value));
        Assert.AreEqual((byte)0x20, value);
    }

    [TestMethod]
    public void FailedOrCanceledSaveAsLeavesAssociationAndDirtyStateUntouched()
    {
        var workspace = new EditorDocumentWorkspace();
        workspace.OpenImage(MemoryImage.FromSegments([new MemorySegment(0x20, [0xA5])]),
            null, MemoryImageExportFormat.IntelHex);
        workspace.EditByte(0x20, 0x5A, OverlapDecision.OverwriteIncoming);
        var document = workspace.Document;
        var path = Path.Combine(Path.GetTempPath(), $"hex-editor-document-{Guid.NewGuid():N}.hex");
        File.WriteAllText(path, "keep existing");
        try
        {
            Assert.Throws<IOException>(() => workspace.SaveAs(path, MemoryImageExportFormat.IntelHex, overwriteExisting: false));
            Assert.AreSame(document, workspace.Document);
            Assert.IsNull(document.SourcePath);
            Assert.IsTrue(document.IsModified);
            Assert.AreEqual("keep existing", File.ReadAllText(path));

            using var cancellation = new CancellationTokenSource();
            cancellation.Cancel();
            Assert.ThrowsExactlyAsync<TaskCanceledException>(async () =>
                await workspace.SaveAsAsync(path, MemoryImageExportFormat.IntelHex, overwriteExisting: true,
                    cancellation.Token, null)).GetAwaiter().GetResult();
            Assert.IsNull(document.SourcePath);
            Assert.IsTrue(document.IsModified);
            Assert.AreEqual("keep existing", File.ReadAllText(path));
        }
        finally { File.Delete(path); }
    }

    [TestMethod]
    public void OpenKeepsSparseAddressesAndFormatMetadataWithDocument()
    {
        const string records = ":020010001011CD\n:0100200020BF\n:0400000500001234B1\n:00000001FF\n";
        var path = Path.Combine(Path.GetTempPath(), $"hex-editor-document-{Guid.NewGuid():N}.hex");
        File.WriteAllText(path, records);
        try
        {
            var workspace = new EditorDocumentWorkspace();
            Assert.IsTrue(workspace.Open(path, OverlapDecision.OverwriteIncoming));
            var document = workspace.Document;

            Assert.AreEqual(Path.GetFullPath(path), document.SourcePath);
            Assert.AreEqual(MemoryImageExportFormat.IntelHex, document.SourceFormat);
            Assert.AreEqual(0x1234u, document.SourceFormatMetadata!.IntelHex!.StartLinearAddress);
            Assert.IsFalse(document.IsModified);
            Assert.IsTrue(document.CurrentImage.TryGetByte(0x10, out var first));
            Assert.AreEqual((byte)0x10, first);
            Assert.IsFalse(document.CurrentImage.TryGetByte(0x12, out _));
            Assert.IsTrue(document.CurrentImage.TryGetByte(0x20, out var last));
            Assert.AreEqual((byte)0x20, last);
        }
        finally { File.Delete(path); }
    }

    [TestMethod]
    public void CloseReleasesTheActiveDocumentWithoutChangingItsSnapshot()
    {
        var workspace = new EditorDocumentWorkspace();
        workspace.OpenImage(MemoryImage.FromSegments([new MemorySegment(0x10, [0xAA])]), null, null);
        workspace.EditByte(0x10, 0xBB, OverlapDecision.OverwriteIncoming);
        var priorDocument = workspace.Document;

        workspace.CloseImage();

        Assert.AreNotSame(priorDocument, workspace.Document);
        Assert.IsEmpty(workspace.Document.CurrentImage.Segments);
        Assert.IsFalse(workspace.Document.CanUndo);
        Assert.IsTrue(priorDocument.CurrentImage.TryGetByte(0x10, out var priorValue));
        Assert.AreEqual((byte)0xBB, priorValue);
        Assert.IsTrue(priorDocument.IsModified);
    }

    [TestMethod]
    public void DocumentPublishesImageAndSavedStateChangesForItsViews()
    {
        var workspace = new EditorDocumentWorkspace();
        workspace.OpenImage(MemoryImage.FromSegments([new MemorySegment(0x10, [0xAA])]), null,
            MemoryImageExportFormat.IntelHex);
        var document = workspace.Document;
        var changed = new List<string?>();
        document.PropertyChanged += (_, args) => changed.Add(args.PropertyName);
        var path = Path.Combine(Path.GetTempPath(), $"hex-editor-document-{Guid.NewGuid():N}.hex");
        try
        {
            workspace.EditByte(0x10, 0xBB, OverlapDecision.OverwriteIncoming);
            CollectionAssert.Contains(changed, nameof(EditorDocument.CurrentImage));
            CollectionAssert.Contains(changed, nameof(EditorDocument.IsModified));
            changed.Clear();

            workspace.SaveAs(path, MemoryImageExportFormat.IntelHex, overwriteExisting: false);
            CollectionAssert.Contains(changed, nameof(EditorDocument.SourcePath));
            CollectionAssert.Contains(changed, nameof(EditorDocument.IsModified));
        }
        finally { if (File.Exists(path)) File.Delete(path); }
    }
}
