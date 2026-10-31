using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using HexEditor.UI;

namespace HexEditor.Tests;

[TestClass]
[DoNotParallelize]
public sealed class AddressColumnAcceptanceTests
{
    [TestMethod]
    public void AddressColumnUsesMeasuredEightDigitWidthAndGapTextKeepsItsTooltipInBothLayouts()
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            Window? window = null;
            try
            {
                var rows = new[]
                {
                    new HexViewRow("FFFFFFFF", "00", "·", false, null),
                    new HexViewRow("00000000", string.Empty, null, true, "Address gap from 00000008 through FFFFFFFF")
                };
                foreach (var windowed in new[] { false, true })
                {
                    var view = new HexViewControl { ItemsSource = rows, UseWindowedScrolling = windowed, Width = 600, Height = 180 };
                    window = new Window { Content = view, Width = 620, Height = 200, ShowInTaskbar = false };
                    window.Show();
                    window.UpdateLayout();
                    var expected = new FormattedText("FFFFFFFF", CultureInfo.InvariantCulture, FlowDirection.LeftToRight,
                        new Typeface(view.ItemsGrid.FontFamily, FontStyles.Normal, FontWeights.Normal, FontStretches.Normal),
                        view.ItemsGrid.FontSize, view.Foreground, VisualTreeHelper.GetDpi(view).PixelsPerDip).Width + 12;
                    Assert.AreEqual(expected, view.AddressColumnWidth, 0.5, "Address width is the active font's eight-digit width plus both six-unit insets.");
                    if (windowed)
                    {
                        var address = Descendants<HexRowAddressControl>(view).First(control => control.Row?.IsGapMarker == true);
                        Assert.AreEqual(rows[1].GapDescription, address.ToolTip);
                    }
                    else
                    {
                        Assert.AreEqual(expected, view.ItemsGrid.Columns[0].ActualWidth, 0.5);
                        var gap = Descendants<TextBlock>(view).First(block => block.Text == rows[1].GapDescription);
                        Assert.AreEqual(rows[1].GapDescription, gap.ToolTip);
                        Assert.AreEqual(6, gap.Margin.Left, 0.1,
                            "Gap descriptions must share the windowed address control's six-unit left inset.");
                    }
                    window.Close();
                    window = null;
                }
            }
            catch (Exception exception) { failure = exception; }
            finally { window?.Close(); }
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
