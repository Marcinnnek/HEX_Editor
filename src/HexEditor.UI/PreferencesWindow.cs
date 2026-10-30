using System.Windows;
using System.Windows.Controls;
using FluentWindow = Wpf.Ui.Controls.FluentWindow;

namespace HexEditor.UI;

public sealed class PreferencesWindow : FluentWindow
{
    public PreferencesWindow() : this(GridSpacingSettings.Current) { }

    public PreferencesWindow(GridSpacingSettings settings)
    {
        Title = "Preferences";
        Width = 420;
        SizeToContent = SizeToContent.Height;
        ResizeMode = ResizeMode.NoResize;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        var panel = new StackPanel { Margin = new Thickness(20) };
        panel.Children.Add(new TextBlock { Text = "HEX grid spacing", FontSize = 18, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 0, 0, 12) });
        var horizontal = new Slider { Minimum = 0, Maximum = 12, TickFrequency = 1, IsSnapToTickEnabled = true, Value = settings.HorizontalByteSpacing };
        var vertical = new Slider { Minimum = 0, Maximum = 8, TickFrequency = 1, IsSnapToTickEnabled = true, Value = settings.VerticalRowSpacing };
        AddSetting(panel, "Horizontal space between bytes", horizontal);
        AddSetting(panel, "Vertical space between rows", vertical);
        var save = new Wpf.Ui.Controls.Button { Content = "Save", IsDefault = true, MinWidth = 80, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 12, 0, 0) };
        save.Click += (_, _) => { settings.SetSpacing((int)horizontal.Value, (int)vertical.Value); DialogResult = true; };
        panel.Children.Add(save);
        Content = panel;
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
