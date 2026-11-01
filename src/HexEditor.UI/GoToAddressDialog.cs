using System.Windows;
using System.Windows.Controls;

namespace HexEditor.UI;

/// <summary>Collects an absolute hexadecimal address for navigation.</summary>
public sealed class GoToAddressDialog : Window
{
    private readonly TextBox _address = new()
    {
        Padding = new Thickness(8, 5, 8, 5),
        VerticalContentAlignment = VerticalAlignment.Center
    };
    private readonly TextBlock _error = new() { TextWrapping = TextWrapping.Wrap };

    public GoToAddressDialog()
    {
        Title = "Go to address";
        Width = 360;
        SizeToContent = SizeToContent.Height;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        WindowStyle = WindowStyle.SingleBorderWindow;
        ResizeMode = ResizeMode.NoResize;
        ShowInTaskbar = false;

        _error.SetResourceReference(TextBlock.ForegroundProperty, "HexErrorBrush");
        HexAddressInput.Configure(_address);

        var panel = new StackPanel { Margin = new Thickness(18) };
        panel.Children.Add(AddressFieldRow.Create("Address (hex)", _address));
        panel.Children.Add(_error);

        var buttons = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Margin = new Thickness(0, 14, 0, 0)
        };
        var go = new Button { Content = "Go", IsDefault = true, MinWidth = 76, Margin = new Thickness(0, 0, 8, 0) };
        go.Click += (_, _) => Accept();
        var cancel = new Button { Content = "Cancel", IsCancel = true, MinWidth = 76 };
        buttons.Children.Add(go);
        buttons.Children.Add(cancel);
        panel.Children.Add(buttons);
        Content = panel;

        Loaded += (_, _) => { _address.Focus(); _address.SelectAll(); };
    }

    public uint? Address { get; private set; }

    private void Accept()
    {
        if (!HexAddressParser.TryParse(_address.Text, out var address))
        {
            _error.Text = "Enter a 32-bit hexadecimal address.";
            _address.Focus();
            _address.SelectAll();
            return;
        }

        Address = address;
        DialogResult = true;
    }
}
