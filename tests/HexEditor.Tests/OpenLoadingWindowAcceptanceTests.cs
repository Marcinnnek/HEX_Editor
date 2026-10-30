using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Threading;
using HexEditor.Core;
using HexEditor.Formats;
using HexEditor.UI;

namespace HexEditor.Tests;

[TestClass]
[DoNotParallelize]
public sealed class OpenLoadingWindowAcceptanceTests
{
    [TestMethod]
    public void Loading_keeps_the_old_editor_browsable_and_cancelable_until_the_read_stops()
    {
        var path = Path.Combine(Path.GetTempPath(), $"hex-editor-open-window-{Guid.NewGuid():N}.hex");
        using (var writer = new StreamWriter(path))
        {
            for (var index = 0; index < 1_000; index++) writer.WriteLine(":01002000BB24");
            writer.WriteLine(":00000001FF");
        }

        Exception? failure = null;
        var thread = new Thread(() =>
        {
            var window = new MainWindow();
            using var enteredProgress = new ManualResetEventSlim();
            using var continueLoad = new ManualResetEventSlim();
            try
            {
                SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext(window.Dispatcher));
                window.Show();
                window.ViewModel.LoadImage(MemoryImage.FromSegments([new MemorySegment(0x10, [0xAA])]),
                    "old.hex", MemoryImageExportFormat.IntelHex);
                var progressObserver = new BlockingProgress<MemoryImageFileProgress>(enteredProgress, continueLoad);
                var load = window.LoadOpenReplacementAsync(path, progressObserver);
                Assert.IsTrue(enteredProgress.Wait(TimeSpan.FromSeconds(10)), "Open should report progress while reading the selected file.");
                window.UpdateLayout();

                var operationPanel = (Border)window.FindName("FileOperationPanel");
                var progress = (ProgressBar)window.FindName("FileOperationProgress");
                var tabs = (TabControl)window.FindName("WorkspaceTabs");
                var editor = (HexViewControl)window.FindName("HexRowsView");
                Assert.AreEqual(Visibility.Visible, operationPanel.Visibility);
                Assert.IsTrue(progress.IsIndeterminate);
                Assert.IsTrue(window.ViewModel.IsOpenReplacementLoading);
                Assert.IsFalse(window.ViewModel.CanEdit);
                Assert.IsFalse(window.ViewModel.ActiveCanSave);
                Assert.IsFalse(window.ViewModel.CanStartOpenCompareOrMerge);
                Assert.IsFalse(((UIElement)window.FindName("OpenMenuItem")).IsEnabled);
                Assert.IsFalse(((UIElement)window.FindName("SaveMenuItem")).IsEnabled);
                Assert.IsFalse(((UIElement)window.FindName("EditMenuItem")).IsEnabled);
                Assert.IsFalse(((UIElement)window.FindName("CompareMenuItem")).IsEnabled);
                Assert.IsFalse(((UIElement)window.FindName("MergeMenuItem")).IsEnabled);
                Assert.IsTrue(tabs.IsEnabled, "The user can still switch workspace tabs while Open is loading.");
                Assert.IsTrue(editor.IsEnabled, "The old Editor stays available for browsing.");
                Assert.IsTrue(window.ViewModel.CurrentImage.TryGetByte(0x10, out var oldByte));
                Assert.AreEqual((byte)0xAA, oldByte);

                var cancel = Descendants<Button>(operationPanel).Single(button => Equals(button.Content, "Cancel"));
                cancel.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
                continueLoad.Set();
                var frame = new DispatcherFrame();
                _ = load.ContinueWith(_ => window.Dispatcher.BeginInvoke(new Action(() => frame.Continue = false)),
                    CancellationToken.None, TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
                Dispatcher.PushFrame(frame);
                Assert.ThrowsExactly<OperationCanceledException>(() => load.GetAwaiter().GetResult());

                Assert.AreEqual(Visibility.Collapsed, operationPanel.Visibility);
                Assert.IsFalse(window.ViewModel.IsOpenReplacementLoading);
                Assert.IsTrue(window.ViewModel.CanEdit);
                Assert.IsTrue(window.ViewModel.CanStartOpenCompareOrMerge);
                Assert.IsTrue(((UIElement)window.FindName("OpenMenuItem")).IsEnabled);
                Assert.IsTrue(((UIElement)window.FindName("SaveMenuItem")).IsEnabled);
                Assert.IsTrue(window.ViewModel.CurrentImage.TryGetByte(0x10, out oldByte));
                Assert.AreEqual((byte)0xAA, oldByte);
            }
            catch (Exception exception) { failure = exception; }
            finally
            {
                continueLoad.Set();
                SynchronizationContext.SetSynchronizationContext(null);
                window.Close();
            }
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

    private sealed class BlockingProgress<T>(ManualResetEventSlim entered, ManualResetEventSlim release) : IProgress<T>
    {
        private int _firstReport = 1;
        public void Report(T value)
        {
            if (Interlocked.Exchange(ref _firstReport, 0) == 1)
            {
                entered.Set();
                release.Wait();
            }
        }
    }
}
