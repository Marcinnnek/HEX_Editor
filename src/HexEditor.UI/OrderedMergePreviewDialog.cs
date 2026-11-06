using System.Windows;
using System.Windows.Controls;
using HexEditor.Core;

namespace HexEditor.UI;

/// <summary>Shows the result of an ordered merge before its one-step commit.</summary>
public sealed class OrderedMergePreviewDialog : Window
{
    public OrderedMergePreviewDialog(OrderedMemoryMergePreview preview, IReadOnlyList<string> names)
    {
        ArgumentNullException.ThrowIfNull(preview);
        ArgumentNullException.ThrowIfNull(names);
        Title = "Preview ordered merge";
        Width = 1150;
        Height = 720;
        MinWidth = 700;
        MinHeight = 460;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        var root = new DockPanel { Margin = new Thickness(12) };
        var summary = new TextBlock
        {
            Text = $"Result: {preview.Result.OccupiedByteCount:N0} occupied byte(s). " +
                $"Order: {string.Join(" → ", names)}. " +
                $"Overlapping bytes per added file: {string.Join(", ", preview.Stages.Select(stage => stage.OverlappingByteCount.ToString("N0")))}.",
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 0, 0, 8)
        };
        DockPanel.SetDock(summary, Dock.Top);
        root.Children.Add(summary);
        var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right,
            Margin = new Thickness(0, 8, 0, 0) };
        var apply = new Button { Content = "Apply merge", IsDefault = true, MinWidth = 105, Margin = new Thickness(0, 0, 8, 0) };
        apply.Click += (_, _) => DialogResult = true;
        buttons.Children.Add(apply);
        buttons.Children.Add(new Button { Content = "Cancel", IsCancel = true, MinWidth = 80 });
        DockPanel.SetDock(buttons, Dock.Bottom);
        root.Children.Add(buttons);
        var compare = new CompareView(new CompareWorkspaceTabViewModel(preview.BaseImage, preview.Result,
            "Before merge", "Resolved merge"));
        root.Children.Add(compare);
        Content = root;
    }
}
