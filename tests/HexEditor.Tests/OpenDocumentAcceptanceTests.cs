using System.Windows;
using System.Windows.Media;
using HexEditor.Core;
using HexEditor.Formats;
using HexEditor.UI;

namespace HexEditor.Tests;

[TestClass]
[DoNotParallelize]
public sealed class OpenDocumentAcceptanceTests
{
    [TestMethod]
    public void Successful_open_replaces_the_bytes_shown_in_the_editor_grid()
    {
        var path = Path.Combine(Path.GetTempPath(), $"hex-editor-open-{Guid.NewGuid():N}.hex");
        File.WriteAllText(path, ":01002000BB24\n:00000001FF\n");
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            var main = new MainWindow();
            try
            {
                main.Show();
                main.ViewModel.LoadImage(MemoryImage.FromSegments([new MemorySegment(0x10, [0xAA])]),
                    "old.hex", MemoryImageExportFormat.IntelHex);
                main.UpdateLayout();

                var preview = main.ViewModel.PrepareOpenAsync(path, CancellationToken.None, null)
                    .GetAwaiter().GetResult();
                main.ViewModel.ApplyImport(preview, OverlapDecision.OverwriteIncoming);
                main.UpdateLayout();

                Assert.IsFalse(main.ViewModel.CurrentImage.TryGetByte(0x10, out _));
                Assert.AreEqual(Path.GetFullPath(path), main.ViewModel.SourcePath);
                var shownByte = Descendants<HexByteRowControl>(main)
                    .Select(control => control.Row)
                    .FirstOrDefault(row => row is { IsGapMarker: false, Cells.Count: > 0 });
                Assert.IsNotNull(shownByte, "The Editor should render a data row after Open completes.");
                Assert.AreEqual("BB", shownByte!.Cells[0].Hex);
            }
            catch (Exception exception) { failure = exception; }
            finally { main.Close(); }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
        File.Delete(path);
        if (failure is not null) System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(failure).Throw();
    }

    private static IEnumerable<T> Descendants<T>(DependencyObject parent) where T : DependencyObject
    {
        for (var index = 0; index < VisualTreeHelper.GetChildrenCount(parent); index++)
        {
            var child = VisualTreeHelper.GetChild(parent, index);
            if (child is T match) yield return match;
            foreach (var descendant in Descendants<T>(child)) yield return descendant;
        }
    }
}
