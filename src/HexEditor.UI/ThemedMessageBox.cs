using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace HexEditor.UI;

/// <summary>Keeps application prompts visually consistent without changing their decisions.</summary>
internal sealed class ThemedMessageBox : Window
{
    private MessageBoxResult _result = MessageBoxResult.Cancel;

    private ThemedMessageBox(Window? owner, string message, string title, MessageBoxButton buttons)
    {
        Title = title;
        Owner = owner;
        WindowStartupLocation = owner is null ? WindowStartupLocation.CenterScreen : WindowStartupLocation.CenterOwner;
        SizeToContent = SizeToContent.WidthAndHeight;
        MinWidth = 360;
        MaxWidth = 620;
        ShowInTaskbar = false;
        WindowStyle = WindowStyle.SingleBorderWindow;
        ResizeMode = ResizeMode.NoResize;

        var layout = new DockPanel { Margin = new Thickness(24) };
        var actions = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right,
            Margin = new Thickness(0, 20, 0, 0) };
        DockPanel.SetDock(actions, Dock.Bottom);
        layout.Children.Add(actions);
        layout.Children.Add(new TextBlock { Text = message, TextWrapping = TextWrapping.Wrap, MaxWidth = 560 });

        switch (buttons)
        {
            case MessageBoxButton.YesNoCancel:
                AddButton(actions, "Yes", MessageBoxResult.Yes, isDefault: true);
                AddButton(actions, "No", MessageBoxResult.No);
                AddButton(actions, "Cancel", MessageBoxResult.Cancel, isCancel: true);
                break;
            case MessageBoxButton.YesNo:
                AddButton(actions, "Yes", MessageBoxResult.Yes, isDefault: true);
                AddButton(actions, "No", MessageBoxResult.No, isCancel: true);
                _result = MessageBoxResult.No;
                break;
            default:
                AddButton(actions, "OK", MessageBoxResult.OK, isDefault: true, isCancel: true);
                _result = MessageBoxResult.OK;
                break;
        }

        Content = layout;
        PreviewKeyDown += (_, e) =>
        {
            if (e.Key == Key.Escape)
            {
                Close();
                e.Handled = true;
            }
        };
    }

    public static MessageBoxResult Show(Window? owner, string message, string title,
        MessageBoxButton buttons, MessageBoxImage image)
    {
        var dialog = new ThemedMessageBox(owner, message, title, buttons);
        dialog.ShowDialog();
        return dialog._result;
    }

    private void AddButton(StackPanel actions, string label, MessageBoxResult result,
        bool isDefault = false, bool isCancel = false)
    {
        var button = new Wpf.Ui.Controls.Button
        {
            Content = label,
            MinWidth = 76,
            Margin = new Thickness(8, 0, 0, 0),
            IsDefault = isDefault,
            IsCancel = isCancel
        };
        button.Click += (_, _) => { _result = result; Close(); };
        actions.Children.Add(button);
    }
}
