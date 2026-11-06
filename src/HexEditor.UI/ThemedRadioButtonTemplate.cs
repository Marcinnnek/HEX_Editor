using System.Windows;
using System.Windows.Controls;

namespace HexEditor.UI;

internal static class ThemedRadioButtonTemplate
{
    public static ControlTemplate Create()
    {
        var root = new FrameworkElementFactory(typeof(StackPanel));
        root.SetValue(StackPanel.OrientationProperty, Orientation.Horizontal);
        root.SetValue(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center);

        var indicator = new FrameworkElementFactory(typeof(Grid));
        indicator.SetValue(FrameworkElement.WidthProperty, 18d);
        indicator.SetValue(FrameworkElement.HeightProperty, 18d);
        indicator.SetValue(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center);
        var outer = new FrameworkElementFactory(typeof(System.Windows.Shapes.Ellipse));
        outer.SetResourceReference(System.Windows.Shapes.Shape.StrokeProperty, "HexBorderBrush");
        outer.SetResourceReference(System.Windows.Shapes.Shape.FillProperty, "HexSurfaceBrush");
        outer.SetValue(System.Windows.Shapes.Shape.StrokeThicknessProperty, 1.5d);
        indicator.AppendChild(outer);
        var dot = new FrameworkElementFactory(typeof(System.Windows.Shapes.Ellipse), "PART_Indicator");
        dot.SetValue(FrameworkElement.WidthProperty, 8d);
        dot.SetValue(FrameworkElement.HeightProperty, 8d);
        dot.SetValue(UIElement.VisibilityProperty, Visibility.Collapsed);
        dot.SetResourceReference(System.Windows.Shapes.Shape.FillProperty, "HexFocusBrush");
        indicator.AppendChild(dot);
        root.AppendChild(indicator);

        var content = new FrameworkElementFactory(typeof(ContentPresenter));
        content.SetValue(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center);
        content.SetValue(FrameworkElement.MarginProperty, new Thickness(2, 0, 0, 0));
        root.AppendChild(content);

        var template = new ControlTemplate(typeof(RadioButton)) { VisualTree = root };
        var selected = new Trigger { Property = RadioButton.IsCheckedProperty, Value = true };
        selected.Setters.Add(new Setter(UIElement.VisibilityProperty, Visibility.Visible, "PART_Indicator"));
        template.Triggers.Add(selected);
        return template;
    }
}
