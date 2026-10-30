using System.Windows;
using System.Windows.Controls;
using HexEditor.Core;
using HexEditor.Formats;
using HexEditor.UI;

namespace HexEditor.Tests;

[TestClass]
public sealed class ActiveWindowTitleAcceptanceTests
{
    [TestMethod]
    public void RenderedWindowTitleTracksTheSelectedImageAndItsSavedState()
    {
        var failures = new List<Exception>();
        var thread = new Thread(() =>
        {
            var window = new MainWindow();
            try
            {
                window.Show();
                window.UpdateLayout();
                Assert.AreEqual("Untitled - HEX Editor", window.Title);

                window.ViewModel.LoadImage(Image(0x10), "C:\\images\\editor.hex", MemoryImageExportFormat.IntelHex);
                Assert.AreEqual("editor.hex - HEX Editor", window.Title);
                window.ViewModel.EditByte(0x1000, 0x11, OverlapDecision.OverwriteIncoming);
                Assert.AreEqual("editor.hex * - HEX Editor", window.Title);

                var savedAs = Path.Combine(Path.GetTempPath(), $"title-{Guid.NewGuid():N}.hex");
                var savedAsName = Path.GetFileName(savedAs);
                try
                {
                    window.ViewModel.SaveAs(savedAs, MemoryImageExportFormat.IntelHex, overwriteExisting: false);
                    Assert.AreEqual($"{savedAsName} - HEX Editor", window.Title);
                    window.ViewModel.EditByte(0x1000, 0x12, OverlapDecision.OverwriteIncoming);
                    Assert.AreEqual($"{savedAsName} * - HEX Editor", window.Title);
                    window.ViewModel.Save();
                    Assert.AreEqual($"{savedAsName} - HEX Editor", window.Title);
                    var baseDocument = window.ViewModel.Workspace.Document;
                    var first = new CompareWorkspaceTabViewModel(baseDocument.CurrentImage, Image(0x20),
                        savedAsName, "reference-a.hex", baseDocument.SourcePath, "reference-a.hex", baseDocument);
                    var firstTab = new TabItem { Header = "Compare A", Content = new CompareView(first) };
                    var second = new CompareWorkspaceTabViewModel(Image(0x30), Image(0x40),
                        "other-base.hex", "reference-b.hex", "other-base.hex", "reference-b.hex");
                    var secondTab = new TabItem { Header = "Compare B", Content = new CompareView(second) };
                    var tabs = (TabControl)window.FindName("WorkspaceTabs");
                    tabs.Items.Add(firstTab);
                    tabs.Items.Add(secondTab);

                    tabs.SelectedItem = firstTab;
                    window.UpdateLayout();
                    Assert.AreEqual($"Compare: {savedAsName} ↔ reference-a.hex - HEX Editor", window.Title);
                    first.BaseDocument!.Editing.EditByte(0x1000, 0x13, OverlapDecision.OverwriteIncoming);
                    Assert.AreEqual($"Compare: {savedAsName} * ↔ reference-a.hex - HEX Editor", window.Title);

                    var compareBaseAs = Path.Combine(Path.GetTempPath(), $"compare-title-{Guid.NewGuid():N}.hex");
                    try
                    {
                        window.ViewModel.SaveAs(compareBaseAs, MemoryImageExportFormat.IntelHex, overwriteExisting: false);
                        var compareBaseAsName = Path.GetFileName(compareBaseAs);
                        Assert.AreEqual($"Compare: {compareBaseAsName} ↔ reference-a.hex - HEX Editor", window.Title);

                        tabs.SelectedItem = secondTab;
                        window.UpdateLayout();
                        Assert.AreEqual("Compare: other-base.hex ↔ reference-b.hex - HEX Editor", window.Title);
                        tabs.SelectedItem = firstTab;
                        Assert.AreEqual($"Compare: {compareBaseAsName} ↔ reference-a.hex - HEX Editor", window.Title);
                        tabs.SelectedItem = tabs.Items[0];
                        Assert.AreEqual($"{compareBaseAsName} - HEX Editor", window.Title);
                        window.ViewModel.EditByte(0x1000, 0x14, OverlapDecision.OverwriteIncoming);
                        Assert.AreEqual($"{compareBaseAsName} * - HEX Editor", window.Title);
                        window.ViewModel.Save();
                        Assert.AreEqual($"{compareBaseAsName} - HEX Editor", window.Title);
                    }
                    finally { if (File.Exists(compareBaseAs)) File.Delete(compareBaseAs); }
                }
                finally { if (File.Exists(savedAs)) File.Delete(savedAs); }
            }
            catch (Exception exception) { failures.Add(exception); }
            finally
            {
                window.ViewModel.ActivateEditor();
                window.ViewModel.CloseImage();
                window.Close();
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
        if (failures.Count > 0) throw failures[0];
    }

    private static MemoryImage Image(byte value) =>
        MemoryImage.FromSegments([new MemorySegment(0x1000, [value])]);
}
