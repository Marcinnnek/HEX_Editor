using System.Windows;
using System.Windows.Controls;
using HexEditor.Core;
using HexEditor.Formats;
using HexEditor.UI;

namespace HexEditor.Tests;

[TestClass]
[DoNotParallelize]
public sealed class EditorMergeAcceptanceTests
{
    [TestMethod]
    public void Rendered_fixed_destination_merge_uses_editor_memory_and_is_undoable_while_compare_is_active()
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            MergeWindow? merge = null;
            try
            {
                var viewModel = new MainViewModel();
                viewModel.LoadImage(Image((0x10, 0x11)), "C:\\firmware\\editor.hex", MemoryImageExportFormat.IntelHex);
                var editorDocument = viewModel.Workspace.Document;
                editorDocument.Editing.EditByte(0x10, 0x12, OverlapDecision.OverwriteIncoming);
                var compare = new CompareWorkspaceTabViewModel(Image((0x10, 0x22)), Image((0x30, 0x33)),
                    "other.hex", "reference.hex", "other.hex", "reference.hex");
                viewModel.ActivateComparison(compare);

                var source = Image((0x10, 0x44), (0x20, 0x55));
                merge = new MergeWindow(editorDocument.CurrentImage, source, "editor.hex", "source.hex", fixedDestination: true);
                merge.Show();
                merge.UpdateLayout();
                Assert.IsTrue(merge.IsFixedDestination);
                Assert.HasCount(1, merge.DirectionOptions, "A fixed destination must expose no reverse direction.");
                Assert.AreSame(editorDocument.CurrentImage, merge.DestinationImage);
                Assert.AreEqual((byte)0x12, merge.Preview.Destination.TryGetByte(0x10, out var unsaved) ? unsaved : (byte)0);
                Assert.AreEqual((byte)0x55, merge.Preview.Resolve(MemoryImageMergeChoice.KeepSource)!.TryGetByte(0x20, out var added) ? added : (byte)0);

                editorDocument.Editing.ApplyMerge(merge.Preview, MemoryImageMergeChoice.KeepDestination);
                Assert.AreSame(editorDocument, viewModel.Workspace.Document);
                Assert.AreEqual((byte)0x12, editorDocument.CurrentImage.TryGetByte(0x10, out var kept) ? kept : (byte)0);
                Assert.IsTrue(editorDocument.Editing.Undo());
                Assert.AreEqual((byte)0x12, editorDocument.CurrentImage.TryGetByte(0x10, out var restored) ? restored : (byte)0);
                Assert.IsFalse(editorDocument.CurrentImage.TryGetByte(0x20, out _));
            }
            catch (Exception exception) { failure = exception; }
            finally { merge?.Close(); }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
        if (failure is not null) System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(failure).Throw();
    }

    private static MemoryImage Image(params (uint Address, byte Value)[] bytes) =>
        MemoryImage.FromSegments(bytes.Select(item => new MemorySegment(item.Address, [item.Value])));
}
