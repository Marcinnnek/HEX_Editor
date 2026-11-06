using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace HexEditor.UI;

/// <summary>Collects whole-file merge inputs in the order they will be overlaid.</summary>
public sealed class OrderedMergeFilesDialog : Window
{
    private readonly ListBox _files = new() { AllowDrop = true, MinHeight = 220 };
    private readonly bool _hasEditorImage;
    private Point _dragStart;
    private MergeInputRow? _dragged;

    public OrderedMergeFilesDialog(string? editorName)
    {
        _hasEditorImage = editorName is not null;
        Title = "Merge images in order";
        Width = 620;
        Height = 440;
        MinWidth = 480;
        MinHeight = 340;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        if (editorName is not null)
            _files.Items.Add(new MergeInputRow(editorName, null));
        _files.PreviewMouseLeftButtonDown += Files_MouseDown;
        _files.PreviewMouseMove += Files_MouseMove;
        _files.DragOver += Files_DragOver;
        _files.Drop += Files_Drop;

        var root = new DockPanel { Margin = new Thickness(16) };
        var heading = new TextBlock
        {
            Text = "Drag files from top to bottom. Later files replace bytes at overlapping addresses.",
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 0, 0, 10)
        };
        DockPanel.SetDock(heading, Dock.Top);
        root.Children.Add(heading);
        var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right,
            Margin = new Thickness(0, 10, 0, 0) };
        var add = new Button { Content = "Add files...", MinWidth = 90, Margin = new Thickness(0, 0, 8, 0) };
        add.Click += Add_Click;
        var remove = new Button { Content = "Remove", MinWidth = 80, Margin = new Thickness(0, 0, 8, 0) };
        remove.Click += (_, _) => { if (_files.SelectedItem is MergeInputRow { Path: not null } selected) _files.Items.Remove(selected); };
        var preview = new Button { Content = "Preview merge", IsDefault = true, MinWidth = 110, Margin = new Thickness(0, 0, 8, 0) };
        preview.Click += (_, _) =>
        {
            if (Paths.Count < (_hasEditorImage ? 1 : 2))
            {
                ThemedMessageBox.Show(this, _hasEditorImage ? "Add at least one file." : "Add at least two files.",
                    "Merge inputs required", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            DialogResult = true;
        };
        buttons.Children.Add(add);
        buttons.Children.Add(remove);
        buttons.Children.Add(preview);
        buttons.Children.Add(new Button { Content = "Cancel", IsCancel = true, MinWidth = 80 });
        DockPanel.SetDock(buttons, Dock.Bottom);
        root.Children.Add(buttons);
        root.Children.Add(_files);
        Content = root;
    }

    public IReadOnlyList<string> Paths => _files.Items.Cast<MergeInputRow>()
        .Where(item => item.Path is not null).Select(item => item.Path!).ToArray();

    private void Add_Click(object sender, RoutedEventArgs e)
    {
        var picker = FirmwareImageDialog.CreateOpen("Choose files to merge");
        picker.Multiselect = true;
        if (picker.ShowDialog(this) != true) return;
        foreach (var path in picker.FileNames)
            _files.Items.Add(new MergeInputRow(System.IO.Path.GetFileName(path), path));
    }

    private void Files_MouseDown(object sender, MouseButtonEventArgs e)
    {
        _dragStart = e.GetPosition(_files);
        _dragged = FindRow(e.OriginalSource as DependencyObject);
    }

    private void Files_MouseMove(object sender, MouseEventArgs e)
    {
        if (e.LeftButton != MouseButtonState.Pressed || _dragged is not { Path: not null } item) return;
        var point = e.GetPosition(_files);
        if (Math.Abs(point.X - _dragStart.X) < SystemParameters.MinimumHorizontalDragDistance &&
            Math.Abs(point.Y - _dragStart.Y) < SystemParameters.MinimumVerticalDragDistance) return;
        _dragged = null;
        DragDrop.DoDragDrop(_files, item, DragDropEffects.Move);
    }

    private void Files_DragOver(object sender, DragEventArgs e)
    {
        e.Effects = e.Data.GetDataPresent(typeof(MergeInputRow)) ? DragDropEffects.Move : DragDropEffects.None;
        e.Handled = true;
    }

    private void Files_Drop(object sender, DragEventArgs e)
    {
        if (e.Data.GetData(typeof(MergeInputRow)) is not MergeInputRow moving || moving.Path is null) return;
        var source = _files.Items.IndexOf(moving);
        var targetRow = FindRow(e.OriginalSource as DependencyObject);
        var target = targetRow is null ? _files.Items.Count : _files.Items.IndexOf(targetRow);
        if (targetRow is not null && _files.ItemContainerGenerator.ContainerFromItem(targetRow) is ListBoxItem container &&
            e.GetPosition(container).Y >= container.ActualHeight / 2)
            target++;
        if (source < 0 || target < (_hasEditorImage ? 1 : 0)) return;
        if (source < target) target--;
        _files.Items.RemoveAt(source);
        _files.Items.Insert(target, moving);
        _files.SelectedItem = moving;
        e.Handled = true;
    }

    private MergeInputRow? FindRow(DependencyObject? source)
    {
        for (var current = source; current is not null; current = current is Visual
            ? VisualTreeHelper.GetParent(current) : LogicalTreeHelper.GetParent(current))
            if (current is ListBoxItem container && _files.ItemContainerGenerator.ItemFromContainer(container) is MergeInputRow row)
                return row;
        return null;
    }

    private sealed record MergeInputRow(string Name, string? Path)
    {
        public override string ToString() => Path is null ? $"Editor: {Name} (fixed first)" : Name;
    }
}
