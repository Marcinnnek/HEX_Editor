using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace HexEditor.UI;

/// <summary>Collects an absolute hexadecimal address for navigation.</summary>
public sealed class GoToAddressDialog : Window
{
    private readonly TextBox _address = new()
    {
        MinWidth = 190,
        MaxLength = 10,
        FontSize = 15,
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
        _address.PreviewTextInput += Address_PreviewTextInput;
        DataObject.AddPastingHandler(_address, Address_Pasting);

        var panel = new StackPanel { Margin = new Thickness(18) };
        panel.Children.Add(new TextBlock { Text = "Address (hex)", Margin = new Thickness(0, 0, 0, 5) });
        panel.Children.Add(_address);
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

    private void Address_PreviewTextInput(object sender, TextCompositionEventArgs e)
    {
        var proposed = _address.Text.Remove(_address.SelectionStart, _address.SelectionLength)
            .Insert(_address.SelectionStart, e.Text);
        e.Handled = !HexAddressParser.IsValidPartialInput(proposed);
    }

    private void Address_Pasting(object sender, DataObjectPastingEventArgs e)
    {
        if (!e.SourceDataObject.GetDataPresent(DataFormats.UnicodeText, true) ||
            e.SourceDataObject.GetData(DataFormats.UnicodeText, true) is not string pasted)
        {
            e.CancelCommand();
            return;
        }

        var proposed = _address.Text.Remove(_address.SelectionStart, _address.SelectionLength)
            .Insert(_address.SelectionStart, pasted);
        if (!HexAddressParser.IsValidPartialInput(proposed)) e.CancelCommand();
    }

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
