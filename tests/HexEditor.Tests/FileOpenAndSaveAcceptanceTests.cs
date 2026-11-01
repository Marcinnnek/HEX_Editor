using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using HexEditor.Core;
using HexEditor.Formats;
using HexEditor.UI;

namespace HexEditor.Tests;

[TestClass]
[DoNotParallelize]
public sealed class FileOpenAndSaveAcceptanceTests
{
    [TestMethod]
    public void FilePathSuppliedToTheWindowOpensThroughTheNormalWorkflow()
    {
        var path = Path.Combine(Path.GetTempPath(), $"firmware próba {Guid.NewGuid():N}.hex");
        new MemoryImageFileService().SaveAtomically(
            MemoryImage.FromSegments([new MemorySegment(0x1234, [0x41, 0x00])]),
            path, MemoryImageExportFormat.IntelHex, 16, overwriteExisting: false);
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            MainWindow? window = null;
            try
            {
                window = new MainWindow();
                SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext(window.Dispatcher));
                window.Show();
                WaitFor(window.OpenPathsAsync([path]), window.Dispatcher);

                Assert.AreEqual(Path.GetFullPath(path), window.ViewModel.Workspace.Document.SourcePath);
                Assert.IsTrue(window.ViewModel.CurrentImage.TryGetByte(0x1234, out var value));
                Assert.AreEqual((byte)0x41, value);
            }
            catch (Exception exception) { failure = exception; }
            finally { window?.Close(); SynchronizationContext.SetSynchronizationContext(null); }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
        File.Delete(path);
        if (failure is not null) System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(failure).Throw();
    }

    [DataRow(MemoryImageExportFormat.IntelHex, "hex")]
    [DataRow(MemoryImageExportFormat.SRecord, "s19")]
    [TestMethod]
    public void TenMegabyteSaveAsFinishesWithAResponsiveWindowAndValidOutput(
        MemoryImageExportFormat format, string extension)
    {
        var path = Path.Combine(Path.GetTempPath(), $"hex-editor-save-{Guid.NewGuid():N}.{extension}");
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            MainWindow? window = null;
            try
            {
                window = new MainWindow();
                SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext(window.Dispatcher));
                var image = MemoryImage.FromPatternRange(0, 3_599_999, [0x41, 0x00, 0x80, 0x5A]);
                window.ViewModel.LoadImage(image, null, null);
                window.Show();
                var dispatcherTicks = 0;
                var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(50) };
                timer.Tick += (_, _) => dispatcherTicks++;
                timer.Start();
                var elapsed = Stopwatch.StartNew();
                var saved = WaitFor(window.SaveDocumentAsAsync(window.ViewModel.Workspace.Document,
                    path, format, overwriteExisting: false), window.Dispatcher);
                elapsed.Stop();
                timer.Stop();

                Assert.IsTrue(saved);
                Assert.IsGreaterThanOrEqualTo(9_000_000L, new FileInfo(path).Length);
                Assert.IsGreaterThan(0, dispatcherTicks, "The UI dispatcher should process input while saving.");
                Assert.IsLessThan(TimeSpan.FromSeconds(30), elapsed.Elapsed,
                    "The save should finish without a progress-update backlog freezing the window.");
                Assert.AreEqual(Visibility.Collapsed, ((Border)window.FindName("FileOperationPanel")).Visibility);
                Assert.AreEqual(Path.GetFullPath(path), window.ViewModel.Workspace.Document.SourcePath);
                Assert.IsFalse(window.ViewModel.Workspace.Document.IsModified);

                window.ViewModel.Workspace.Document.Editing.EditByte(0, 0x42, OverlapDecision.OverwriteIncoming);
                Assert.IsTrue(window.ViewModel.Workspace.Document.IsModified);
                Assert.IsTrue(WaitFor(window.SaveDocumentAsync(window.ViewModel.Workspace.Document), window.Dispatcher),
                    "Ordinary Save should also finish and restore the window.");
                Assert.IsFalse(window.ViewModel.Workspace.Document.IsModified);
                Assert.AreEqual(Visibility.Collapsed, ((Border)window.FindName("FileOperationPanel")).Visibility);

                using var input = File.OpenRead(path);
                var imported = new MemoryImageFileService().PrepareImport(input, MemoryImage.Empty, path)
                    .Apply(OverlapDecision.OverwriteIncoming)!.Image;
                Assert.AreEqual(image.OccupiedByteCount, imported.OccupiedByteCount);
                foreach (var address in new uint[] { 0, 1, 2, 3, 1_000_000, 3_599_999 })
                {
                    Assert.IsTrue(imported.TryGetByte(address, out var actual));
                    Assert.IsTrue(image.TryGetByte(address, out var expected));
                    Assert.AreEqual(address == 0 ? (byte)0x42 : expected, actual);
                }
            }
            catch (Exception exception) { failure = exception; }
            finally { window?.Close(); SynchronizationContext.SetSynchronizationContext(null); }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
        if (File.Exists(path)) File.Delete(path);
        if (failure is not null) System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(failure).Throw();
    }

    [DataRow(true)]
    [DataRow(false)]
    [TestMethod]
    public void ClosePromptHonorsSaveAndDontSaveWithoutFreezing(bool saveChanges)
    {
        var path = Path.Combine(Path.GetTempPath(), $"hex-editor-close-save-{Guid.NewGuid():N}.hex");
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            MainWindow? window = null;
            try
            {
                window = new MainWindow();
                SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext(window.Dispatcher));
                window.ViewModel.LoadImage(MemoryImage.FromSegments([new MemorySegment(0x10, [0x41])]),
                    path, MemoryImageExportFormat.IntelHex);
                window.ViewModel.Workspace.Document.Editing.EditByte(0x10, 0x42, OverlapDecision.OverwriteIncoming);
                window.Show();

                var frame = new DispatcherFrame();
                var closed = false;
                window.Closed += (_, _) => { closed = true; frame.Continue = false; };
                var promptTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(20) };
                promptTimer.Tick += (_, _) =>
                {
                    var prompt = window.OwnedWindows.OfType<SaveChangesDialog>().FirstOrDefault();
                    if (prompt is null) return;
                    promptTimer.Stop();
                    var choice = Descendants<Button>(prompt).Single(button =>
                        Equals(button.Content, saveChanges ? "Save" : "Don't save"));
                    choice.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                };
                var timeout = new DispatcherTimer { Interval = TimeSpan.FromSeconds(10) };
                timeout.Tick += (_, _) => { timeout.Stop(); frame.Continue = false; };
                promptTimer.Start();
                timeout.Start();
                window.Close();
                if (!closed) Dispatcher.PushFrame(frame);
                promptTimer.Stop();
                timeout.Stop();
                Assert.IsTrue(closed, "The close should resume after the asynchronous save finishes.");
                if (!saveChanges)
                {
                    Assert.IsFalse(File.Exists(path), "Don't save must not write the document.");
                    return;
                }
                using var input = File.OpenRead(path);
                var imported = new MemoryImageFileService().PrepareImport(input, MemoryImage.Empty, path)
                    .Apply(OverlapDecision.OverwriteIncoming)!.Image;
                Assert.IsTrue(imported.TryGetByte(0x10, out var value));
                Assert.AreEqual((byte)0x42, value);
            }
            catch (Exception exception) { failure = exception; }
            finally { SynchronizationContext.SetSynchronizationContext(null); }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
        if (File.Exists(path)) File.Delete(path);
        if (failure is not null) System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(failure).Throw();
    }

    private static IEnumerable<T> Descendants<T>(DependencyObject parent) where T : DependencyObject
    {
        for (var index = 0; index < System.Windows.Media.VisualTreeHelper.GetChildrenCount(parent); index++)
        {
            var child = System.Windows.Media.VisualTreeHelper.GetChild(parent, index);
            if (child is T match) yield return match;
            foreach (var descendant in Descendants<T>(child)) yield return descendant;
        }
    }

    private static T WaitFor<T>(Task<T> task, Dispatcher dispatcher)
    {
        if (!task.IsCompleted)
        {
            var frame = new DispatcherFrame();
            _ = task.ContinueWith(_ => dispatcher.BeginInvoke(new Action(() => frame.Continue = false)),
                CancellationToken.None, TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
            Dispatcher.PushFrame(frame);
        }
        return task.GetAwaiter().GetResult();
    }

    private static void WaitFor(Task task, Dispatcher dispatcher) => WaitFor(
        task.ContinueWith(completed => { completed.GetAwaiter().GetResult(); return true; },
            CancellationToken.None, TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default), dispatcher);
}
