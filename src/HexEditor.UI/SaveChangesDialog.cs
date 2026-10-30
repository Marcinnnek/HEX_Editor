using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace HexEditor.UI;

/// <summary>Asks whether one named image should be saved before it is closed.</summary>
public sealed class SaveChangesDialog : Window
{
    public SaveChangesDialog(Window? owner, string imageName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(imageName);
        Owner = owner;
        Title = "Unsaved changes";
        WindowStartupLocation = owner is null ? WindowStartupLocation.CenterScreen : WindowStartupLocation.CenterOwner;
        SizeToContent = SizeToContent.WidthAndHeight;
        MinWidth = 430;
        MaxWidth = 620;
        ShowInTaskbar = false;
        WindowStyle = WindowStyle.SingleBorderWindow;
        ResizeMode = ResizeMode.NoResize;
        Choice = MessageBoxResult.Cancel;

        var layout = new DockPanel { Margin = new Thickness(24) };
        var actions = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Margin = new Thickness(0, 24, 0, 0)
        };
        DockPanel.SetDock(actions, Dock.Bottom);
        layout.Children.Add(actions);

        var messageRow = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        var warning = new Border
        {
            Width = 36,
            Height = 36,
            CornerRadius = new CornerRadius(18),
            BorderThickness = new Thickness(1),
            Margin = new Thickness(0, 0, 16, 0),
            Child = new TextBlock
            {
                Text = "!",
                FontSize = 22,
                FontWeight = FontWeights.SemiBold,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                TextAlignment = TextAlignment.Center
            }
        };
        warning.SetResourceReference(BackgroundProperty, "HexRaisedBrush");
        warning.SetResourceReference(BorderBrushProperty, "HexBorderBrush");
        ((TextBlock)warning.Child).SetResourceReference(ForegroundProperty, "HexTextBrush");
        messageRow.Children.Add(warning);
        messageRow.Children.Add(new TextBlock
        {
            Text = $"Save changes to {imageName} before closing?",
            TextWrapping = TextWrapping.Wrap,
            MaxWidth = 490,
            VerticalAlignment = VerticalAlignment.Center
        });
        layout.Children.Add(messageRow);

        AddButton(actions, "Save", MessageBoxResult.Yes, isDefault: true);
        AddButton(actions, "Don't save", MessageBoxResult.No);
        AddButton(actions, "Cancel", MessageBoxResult.Cancel, isCancel: true);
        Content = layout;

        PreviewKeyDown += (_, e) =>
        {
            if (e.Key != Key.Escape) return;
            Choice = MessageBoxResult.Cancel;
            Close();
            e.Handled = true;
        };
    }

    public MessageBoxResult Choice { get; private set; }

    public static MessageBoxResult Show(Window? owner, string imageName)
    {
        var dialog = new SaveChangesDialog(owner, imageName);
        dialog.ShowDialog();
        return dialog.Choice;
    }

    private void AddButton(Panel actions, string label, MessageBoxResult result, bool isDefault = false, bool isCancel = false)
    {
        var button = new Wpf.Ui.Controls.Button
        {
            Content = label,
            MinWidth = 100,
            Padding = new Thickness(10, 5, 10, 5),
            Margin = new Thickness(8, 0, 0, 0),
            IsDefault = isDefault,
            IsCancel = isCancel
        };
        button.Click += (_, _) => { Choice = result; Close(); };
        actions.Children.Add(button);
    }
}
