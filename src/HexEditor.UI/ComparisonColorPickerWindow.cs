using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace HexEditor.UI;

/// <summary>A small native WPF picker that edits all ARGB channels.</summary>
internal sealed class ComparisonColorPickerWindow : Window
{
    private readonly Slider[] _channels = new Slider[4];
    private readonly Border _preview = new() { Width = 88, Height = 34, BorderThickness = new Thickness(1), Margin = new Thickness(0, 0, 0, 12) };

    public ComparisonColorPickerWindow(string initialColor)
    {
        Title = "Choose highlight color";
        Width = 340;
        SizeToContent = SizeToContent.Height;
        ResizeMode = ResizeMode.NoResize;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        ShowInTaskbar = false;
        var parsed = (Color)ColorConverter.ConvertFromString(initialColor);
        var values = new byte[] { parsed.A, parsed.R, parsed.G, parsed.B };
        var content = new StackPanel { Margin = new Thickness(18) };
        content.Children.Add(new TextBlock { Text = "Adjust the color channels", FontSize = 16, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 0, 0, 10) });
        _preview.BorderBrush = new SolidColorBrush(Color.FromRgb(0x80, 0x80, 0x80));
        content.Children.Add(_preview);
        var names = new[] { "Alpha", "Red", "Green", "Blue" };
        for (var index = 0; index < names.Length; index++)
        {
            var row = new DockPanel { Margin = new Thickness(0, 2, 0, 2) };
            var value = new TextBlock { Width = 32, TextAlignment = TextAlignment.Right, VerticalAlignment = VerticalAlignment.Center };
            DockPanel.SetDock(value, Dock.Right);
            row.Children.Add(value);
            var slider = new Slider { Minimum = 0, Maximum = 255, TickFrequency = 1, IsSnapToTickEnabled = true, Value = values[index], Tag = value };
            slider.ValueChanged += (_, _) => { value.Text = ((int)slider.Value).ToString("X2"); UpdatePreview(); };
            _channels[index] = slider;
            var label = new TextBlock { Text = names[index], Width = 54, VerticalAlignment = VerticalAlignment.Center };
            DockPanel.SetDock(label, Dock.Left);
            row.Children.Add(label);
            row.Children.Add(slider);
            value.Text = values[index].ToString("X2");
            content.Children.Add(row);
        }
        var actions = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 14, 0, 0) };
        var cancel = new Wpf.Ui.Controls.Button { Content = "Cancel", IsCancel = true, MinWidth = 78, Margin = new Thickness(0, 0, 8, 0) };
        cancel.Click += (_, _) => DialogResult = false;
        var save = new Wpf.Ui.Controls.Button { Content = "Use color", IsDefault = true, MinWidth = 78 };
        save.Click += (_, _) => DialogResult = true;
        actions.Children.Add(cancel);
        actions.Children.Add(save);
        content.Children.Add(actions);
        Content = content;
        UpdatePreview();
    }

    public string SelectedColor => $"#{(int)_channels[0].Value:X2}{(int)_channels[1].Value:X2}{(int)_channels[2].Value:X2}{(int)_channels[3].Value:X2}";

    private void UpdatePreview() => _preview.Background = new SolidColorBrush(Color.FromArgb(
        (byte)_channels[0].Value, (byte)_channels[1].Value, (byte)_channels[2].Value, (byte)_channels[3].Value));
}
