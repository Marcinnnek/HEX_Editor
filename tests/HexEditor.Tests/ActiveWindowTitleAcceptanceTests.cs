using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using HexEditor.Core;
using HexEditor.Formats;
using HexEditor.UI;

namespace HexEditor.Tests;

[TestClass]
public sealed class ActiveWindowTitleAcceptanceTests
{
    [TestMethod]
    public void NativeCaptionTracksTheSelectedImageAndItsSavedState()
    {
        var failures = new List<Exception>();
        var thread = new Thread(() =>
        {
            var window = new MainWindow();
            try
            {
                window.Show();
                window.UpdateLayout();
                AssertNativeCaption(window, "HexEditor");
                var handle = new WindowInteropHelper(window).Handle;
                if (DwmGetWindowAttribute(handle, 36, out var captionTextColor, sizeof(int)) >= 0)
                    Assert.AreEqual(0x00000000, captionTextColor, "Light title bars need visible dark caption text.");

                window.ViewModel.LoadImage(Image(0x10), "C:\\images\\editor.hex", MemoryImageExportFormat.IntelHex);
                AssertNativeCaption(window, "editor.hex - HexEditor");
                window.ViewModel.EditByte(0x1000, 0x11, OverlapDecision.OverwriteIncoming);
                AssertNativeCaption(window, "editor.hex * - HexEditor");

                var savedAs = Path.Combine(Path.GetTempPath(), $"title-{Guid.NewGuid():N}.hex");
                var savedAsName = Path.GetFileName(savedAs);
                try
                {
                    window.ViewModel.SaveAs(savedAs, MemoryImageExportFormat.IntelHex, overwriteExisting: false);
                    AssertNativeCaption(window, $"{savedAsName} - HexEditor");
                    window.ViewModel.EditByte(0x1000, 0x12, OverlapDecision.OverwriteIncoming);
                    AssertNativeCaption(window, $"{savedAsName} * - HexEditor");
                    window.ViewModel.Save();
                    AssertNativeCaption(window, $"{savedAsName} - HexEditor");
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
                    AssertNativeCaption(window, $"Compare: {savedAsName} ↔ reference-a.hex - HexEditor");
                    first.BaseDocument!.Editing.EditByte(0x1000, 0x13, OverlapDecision.OverwriteIncoming);
                    AssertNativeCaption(window, $"Compare: {savedAsName} * ↔ reference-a.hex - HexEditor");

                    var compareBaseAs = Path.Combine(Path.GetTempPath(), $"compare-title-{Guid.NewGuid():N}.hex");
                    try
                    {
                        window.ViewModel.SaveAs(compareBaseAs, MemoryImageExportFormat.IntelHex, overwriteExisting: false);
                        var compareBaseAsName = Path.GetFileName(compareBaseAs);
                        AssertNativeCaption(window, $"Compare: {compareBaseAsName} ↔ reference-a.hex - HexEditor");

                        tabs.SelectedItem = secondTab;
                        window.UpdateLayout();
                        AssertNativeCaption(window, "Compare: other-base.hex ↔ reference-b.hex - HexEditor");
                        tabs.SelectedItem = firstTab;
                        AssertNativeCaption(window, $"Compare: {compareBaseAsName} ↔ reference-a.hex - HexEditor");
                        tabs.SelectedItem = tabs.Items[0];
                        AssertNativeCaption(window, $"{compareBaseAsName} - HexEditor");
                        window.ViewModel.EditByte(0x1000, 0x14, OverlapDecision.OverwriteIncoming);
                        AssertNativeCaption(window, $"{compareBaseAsName} * - HexEditor");
                        window.ViewModel.Save();
                        AssertNativeCaption(window, $"{compareBaseAsName} - HexEditor");
                        window.ViewModel.CloseImage();
                        AssertNativeCaption(window, "HexEditor");
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

    private static void AssertNativeCaption(Window window, string expected)
    {
        Assert.IsTrue(window.IsVisible, "The main window must be visible to verify its native caption.");
        var caption = new StringBuilder(512);
        GetWindowText(new WindowInteropHelper(window).Handle, caption, caption.Capacity);
        Assert.AreEqual(expected, caption.ToString());
    }

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern int GetWindowText(IntPtr hWnd, StringBuilder text, int count);

    [DllImport("dwmapi.dll")]
    private static extern int DwmGetWindowAttribute(IntPtr hWnd, int attribute, out int value, int size);
}
