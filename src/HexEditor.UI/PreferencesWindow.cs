using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace HexEditor.UI;

public sealed class PreferencesWindow : Window
{
    private readonly PreferencesSession _choices;
    private readonly Dictionary<ComparisonColorKind, Border> _swatches = [];
    private AppearanceMode _palette = AppearanceMode.Light;

    public PreferencesWindow(PreferencesSession choices)
    {
        _choices = choices;
        Title = "Preferences";
        Width = 470;
        SizeToContent = SizeToContent.Height;
        ResizeMode = ResizeMode.NoResize;
        WindowStyle = WindowStyle.SingleBorderWindow;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        ShowInTaskbar = false;

        var panel = new StackPanel { Margin = new Thickness(20) };
        panel.Children.Add(new TextBlock { Text = "Appearance", FontSize = 18, FontWeight = FontWeights.SemiBold,
            Margin = new Thickness(0, 0, 0, 10) });
        var modes = new StackPanel { Margin = new Thickness(0, 0, 0, 16) };
        var followWindows = AddMode(modes, "Follow Windows", choices.Appearance == AppearanceMode.Automatic);
        var light = AddMode(modes, "Light", choices.Appearance == AppearanceMode.Light);
        AddMode(modes, "Dark", choices.Appearance == AppearanceMode.Dark);
        panel.Children.Add(modes);

        panel.Children.Add(new TextBlock { Text = "HEX grid spacing", FontSize = 18,
            FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 0, 0, 12) });
        var horizontal = new Slider { Minimum = 0, Maximum = 12, TickFrequency = 1,
            IsSnapToTickEnabled = true, Value = choices.HorizontalByteSpacing };
        var vertical = new Slider { Minimum = 0, Maximum = 8, TickFrequency = 1,
            IsSnapToTickEnabled = true, Value = choices.VerticalRowSpacing };
        horizontal.SetResourceReference(Control.ForegroundProperty, "HexTextBrush");
        vertical.SetResourceReference(Control.ForegroundProperty, "HexTextBrush");
        AddSetting(panel, "Horizontal space between bytes", horizontal);
        AddSetting(panel, "Vertical space between rows", vertical);

        panel.Children.Add(new TextBlock { Text = "Highlight colors", FontSize = 18,
            FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 8, 0, 8) });
        var paletteSelector = new ComboBox { Name = "ComparisonPaletteSelector", Width = 150,
            HorizontalAlignment = HorizontalAlignment.Left, SelectedIndex = 0, Margin = new Thickness(0, 0, 0, 8) };
        paletteSelector.Items.Add("Light palette");
        paletteSelector.Items.Add("Dark palette");
        paletteSelector.SelectionChanged += (_, _) =>
        {
            _palette = paletteSelector.SelectedIndex == 1 ? AppearanceMode.Dark : AppearanceMode.Light;
            RefreshSwatches();
        };
        panel.Children.Add(paletteSelector);
        AddColorRow(panel, "Unequal bytes", ComparisonColorKind.Different);
        AddColorRow(panel, "Bytes only in A", ComparisonColorKind.OnlyInA);
        AddColorRow(panel, "Bytes only in B", ComparisonColorKind.OnlyInB);
        AddColorRow(panel, "Edited bytes", ComparisonColorKind.EditedByte);
        var reset = new Wpf.Ui.Controls.Button { Content = "Reset palette", MinWidth = 100,
            HorizontalAlignment = HorizontalAlignment.Left, Margin = new Thickness(0, 4, 0, 0) };
        reset.Click += (_, _) => { choices.ResetPalette(_palette); RefreshSwatches(); };
        panel.Children.Add(reset);

        var actions = new StackPanel { Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 12, 0, 0) };
        var cancel = new Wpf.Ui.Controls.Button { Content = "Cancel", IsCancel = true,
            MinWidth = 80, Margin = new Thickness(0, 0, 8, 0) };
        cancel.Click += (_, _) => DialogResult = false;
        var save = new Wpf.Ui.Controls.Button { Content = "Save", IsDefault = true, MinWidth = 80 };
        save.Click += (_, _) =>
        {
            choices.Appearance = followWindows.IsChecked == true ? AppearanceMode.Automatic
                : light.IsChecked == true ? AppearanceMode.Light : AppearanceMode.Dark;
            choices.HorizontalByteSpacing = (int)horizontal.Value;
            choices.VerticalRowSpacing = (int)vertical.Value;
            DialogResult = true;
        };
        actions.Children.Add(cancel);
        actions.Children.Add(save);
        panel.Children.Add(actions);
        Content = panel;
        RefreshSwatches();
    }

    private void AddColorRow(Panel panel, string label, ComparisonColorKind kind)
    {
        var row = new DockPanel { Margin = new Thickness(0, 3, 0, 3), LastChildFill = false };
        var choose = new Wpf.Ui.Controls.Button { Content = "Choose…", MinWidth = 80, Tag = kind };
        choose.Click += (_, _) => ChooseColor(kind);
        DockPanel.SetDock(choose, Dock.Right);
        row.Children.Add(choose);
        var swatch = new Border { Width = 42, Height = 22, BorderThickness = new Thickness(1),
            Margin = new Thickness(0, 0, 8, 0), Tag = kind };
        swatch.SetResourceReference(Border.BorderBrushProperty, "HexBorderBrush");
        DockPanel.SetDock(swatch, Dock.Right);
        row.Children.Add(swatch);
        _swatches.Add(kind, swatch);
        row.Children.Add(new TextBlock { Text = label, VerticalAlignment = VerticalAlignment.Center });
        panel.Children.Add(row);
    }

    private void ChooseColor(ComparisonColorKind kind)
    {
        var picker = new ComparisonColorPickerWindow(_choices.GetColor(_palette, kind)) { Owner = this };
        if (picker.ShowDialog() != true) return;
        _choices.SetColor(_palette, kind, picker.SelectedColor);
        RefreshSwatches();
    }

    private void RefreshSwatches()
    {
        foreach (var (kind, swatch) in _swatches)
            swatch.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString(_choices.GetColor(_palette, kind)));
    }

    private static RadioButton AddMode(Panel panel, string label, bool selected)
    {
        var radio = new RadioButton { Content = label, GroupName = "Appearance", IsChecked = selected,
            Margin = new Thickness(0, 0, 0, 7), Template = CreateModeTemplate() };
        radio.SetResourceReference(ForegroundProperty, "HexTextBrush");
        panel.Children.Add(radio);
        return radio;
    }

    private static ControlTemplate CreateModeTemplate()
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

    private static void AddSetting(Panel panel, string label, Slider slider)
    {
        var row = new DockPanel { Margin = new Thickness(0, 4, 0, 8) };
        var value = new TextBlock { Width = 32, TextAlignment = TextAlignment.Right, Text = slider.Value.ToString("0") };
        slider.ValueChanged += (_, _) => value.Text = slider.Value.ToString("0");
        DockPanel.SetDock(value, Dock.Right);
        row.Children.Add(value);
        row.Children.Add(slider);
        panel.Children.Add(new TextBlock { Text = label, Margin = new Thickness(0, 0, 0, 4) });
        panel.Children.Add(row);
    }
}
