using System.Diagnostics;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;
using HexEditor.Core;
using HexEditor.Formats;
using HexEditor.UI;

namespace HexEditor.Tests;

[TestClass]
[DoNotParallelize]
public sealed class OpenPerformanceAcceptanceTests
{
    private const int OccupiedByteCount = 5_000_000;

    [TestMethod]
    [TestCategory("ReleaseBenchmark")]
    public void Five_megabyte_intel_hex_and_s_record_open_to_an_interactive_editor_within_two_seconds()
    {
        var data = new byte[OccupiedByteCount];
        for (var index = 0; index < data.Length; index++)
            data[index] = (byte)(index * 37 + 11);
        var image = MemoryImage.FromSegments([new MemorySegment(0x0010_0000, data)]);
        var files = new MemoryImageFileService();
        foreach (var format in new[] { MemoryImageExportFormat.IntelHex, MemoryImageExportFormat.SRecord })
        {
            var extension = format == MemoryImageExportFormat.IntelHex ? ".hex" : ".srec";
            var path = Path.Combine(Path.GetTempPath(), $"hex-editor-open-5mb-{Guid.NewGuid():N}{extension}");
            try
            {
                using (var output = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None,
                           64 * 1024, FileOptions.SequentialScan))
                    files.Export(image, format, 32, output);

                var elapsed = OpenToInteractiveEditor(path);
                var drive = new DriveInfo(Path.GetPathRoot(path)!);
                TestContext.WriteLine($"{format}: {elapsed.TotalMilliseconds:N0} ms; file {new FileInfo(path).Length:N0} bytes; {drive.Name} {drive.DriveType} {drive.DriveFormat}; {Environment.OSVersion}; .NET {Environment.Version}; {Environment.ProcessorCount} processors; Release");
                Assert.IsLessThan(TimeSpan.FromSeconds(2), elapsed,
                    $"Opening the 5,000,000-byte {format} fixture took {elapsed.TotalMilliseconds:N0} ms.");
            }
            finally
            {
                if (File.Exists(path)) File.Delete(path);
            }
        }
    }

    public TestContext TestContext { get; set; } = null!;

    private static TimeSpan OpenToInteractiveEditor(string path)
    {
        TimeSpan elapsed = default;
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            var main = new MainWindow();
            try
            {
                SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext(main.Dispatcher));
                main.Show();
                main.UpdateLayout();
                var timer = Stopwatch.StartNew();
                var load = LoadAndRenderAsync();
                var frame = new DispatcherFrame();
                _ = load.ContinueWith(_ => main.Dispatcher.BeginInvoke(() => frame.Continue = false),
                    TaskScheduler.Default);
                Dispatcher.PushFrame(frame);
                load.GetAwaiter().GetResult();
                elapsed = timer.Elapsed;
            }
            catch (Exception exception) { failure = exception; }
            finally { main.Close(); }

            async Task LoadAndRenderAsync()
            {
                var preview = await main.LoadOpenReplacementAsync(path);
                main.ViewModel.ApplyImport(preview, OverlapDecision.OverwriteIncoming);
                main.UpdateLayout();
                var firstDataRow = Descendants<HexByteRowControl>(main)
                    .Select(control => control.Row)
                    .FirstOrDefault(row => row is { IsGapMarker: false, Cells.Count: > 0 });
                Assert.IsNotNull(firstDataRow, "Open completed without rendering an interactive Editor row.");
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
        if (failure is not null) System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(failure).Throw();
        return elapsed;
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
