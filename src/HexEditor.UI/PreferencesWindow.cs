using System.Windows;
using System.Windows.Controls;

namespace HexEditor.UI;

public sealed class PreferencesWindow : Window
{
    public PreferencesWindow(PreferencesSession choices)
    {
        Title = "Preferences";
        Width = 420;
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
        AddSetting(panel, "Horizontal space between bytes", horizontal);
        AddSetting(panel, "Vertical space between rows", vertical);

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
    }

    private static RadioButton AddMode(Panel panel, string label, bool selected)
    {
        var radio = new RadioButton { Content = label, GroupName = "Appearance", IsChecked = selected,
            Margin = new Thickness(0, 0, 0, 7) };
        radio.SetResourceReference(ForegroundProperty, "HexTextBrush");
        panel.Children.Add(radio);
        return radio;
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
