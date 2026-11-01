using System.Windows;
using System.Windows.Controls;

namespace HexEditor.UI;

internal static class FormTextBoxAppearance
{
    public const double FontSize = 15;
    public static readonly Thickness Padding = new(8, 5, 8, 5);

    public static void Apply(TextBox box)
    {
        box.FontSize = FontSize;
        box.Margin = new Thickness(0);
        box.Padding = Padding;
        box.VerticalContentAlignment = VerticalAlignment.Center;
    }
}
