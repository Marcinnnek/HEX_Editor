using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using HexEditor.Core;
using HexEditor.UI;
using Wpf.Ui.Appearance;

namespace HexEditor.Tests;

[TestClass]
[DoNotParallelize]
public sealed class CompareColumnHeaderAcceptanceTests
{
    [TestMethod]
    public void CompareColumnHeadersUseTheApplicationSurfaceInsteadOfAWhiteBand()
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            var app = new App();
            app.InitializeComponent();
            Window? window = null;
            try
            {
                var image = MemoryImage.FromSegments([new MemorySegment(0x1000, [0x10, 0x20])]);
                var compare = new CompareView(new CompareWorkspaceTabViewModel(image, image, "base.hex", "reference.hex"));
                window = new Window { Content = compare, Width = 1100, Height = 600, ShowInTaskbar = false };
                ApplicationAppearance.ApplyToWindow(window, AppearanceMode.Dark);
                window.Show();
                window.UpdateLayout();

                var expected = ((SolidColorBrush)window.FindResource("HexWindowBrush")).Color;
                var expectedText = ((SolidColorBrush)window.FindResource("HexTextBrush")).Color;
                foreach (var panelName in new[] { "FileAView", "FileBView" })
                {
                    var panel = (HexViewControl)compare.FindName(panelName);
                    Assert.IsInstanceOfType<SolidColorBrush>(panel.ItemsGrid.Background);
                    Assert.AreEqual(expected, ((SolidColorBrush)panel.ItemsGrid.Background).Color,
                        $"{panelName} empty header area should use the Editor surface.");
                    var headers = Descendants<DataGridColumnHeader>(panel.ItemsGrid)
                        .Where(header => header.Content?.ToString() is "Address" or "HEX Data" or "ASCII")
                        .ToArray();
                    Assert.HasCount(3, headers, $"{panelName} should show Address, HEX Data, and ASCII headers.");
                    foreach (var header in Descendants<DataGridColumnHeader>(panel.ItemsGrid))
                    {
                        Assert.IsInstanceOfType<SolidColorBrush>(header.Background,
                            $"{panelName} header '{header.Content}' should have an explicit themed surface.");
                        Assert.AreEqual(expected, ((SolidColorBrush)header.Background).Color,
                            $"{panelName} header '{header.Content}' should use the same surface as the Editor.");
                    }
                    foreach (var header in headers)
                    {
                        Assert.IsInstanceOfType<SolidColorBrush>(header.Foreground);
                        Assert.AreEqual(expectedText, ((SolidColorBrush)header.Foreground).Color,
                            $"{panelName} header '{header.Content}' should use readable application text.");
                    }
                }
            }
            catch (Exception exception) { failure = exception; }
            finally { window?.Close(); app.Shutdown(); }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
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
