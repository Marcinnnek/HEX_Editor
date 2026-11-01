using System.Windows;
using System.Windows.Controls;

namespace HexEditor.UI;

internal static class AddressFieldRow
{
    public const double LabelWidth = 180;
    public const double LongLabelWidth = 285;

    public static Grid Create(string label, TextBox input, Thickness margin = default, double labelWidth = LabelWidth)
    {
        var row = new Grid { Margin = margin };
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(labelWidth) });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        row.Children.Add(new TextBlock { Text = label, VerticalAlignment = VerticalAlignment.Center });
        Grid.SetColumn(input, 1);
        row.Children.Add(input);
        return row;
    }
}
