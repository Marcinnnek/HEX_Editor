using System.Windows;
using System.Windows.Controls;
using HexEditor.Formats;

namespace HexEditor.UI;

public enum ExportScope
{
    EntireImage,
    SelectedSegments,
    AddressRange
}

public sealed record ExportOptions(ExportScope Scope, string Path, MemoryImageExportFormat Format,
    int MaxDataBytesPerRecord, uint RangeStart, uint RangeEnd, IReadOnlyList<int> SegmentIndexes);

/// <summary>Collects all choices for one copy export before the file workflow starts.</summary>
public sealed class ExportOptionsDialog : Window
{
    private readonly ComboBox _scope = new() { ItemsSource = new[] { "Entire image", "Selected memory segments", "Address range" }, SelectedIndex = 0 };
    private readonly ComboBox _format = new()
    {
        ItemsSource = new[] { MemoryImageExportFormat.IntelHex, MemoryImageExportFormat.SRecord }
            .Select(format => new FormatChoice(format, format.GetDisplayName())).ToArray(),
        SelectedIndex = 0
    };
    private readonly ComboBox _recordSize = new() { ItemsSource = new[] { 8, 16, 32 }, SelectedItem = 16 };
    private readonly TextBox _path = new() { MinWidth = 300 };
    private readonly TextBox _start = new() { Text = "00000000" };
    private readonly TextBox _end = new() { Text = "00000000" };
    private readonly ListBox _segments = new() { SelectionMode = SelectionMode.Extended, MinHeight = 90 };

    public ExportOptionsDialog(IReadOnlyList<MemorySegmentRow> segments, IEnumerable<int> selectedSegments,
        int previousRecordSize, ExportScope initialScope, uint? initialStart = null, uint? initialEnd = null)
    {
        Title = "Export memory data";
        Width = 600;
        SizeToContent = SizeToContent.Height;
        MinWidth = 560;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        ResizeMode = ResizeMode.NoResize;
        FormTextBoxAppearance.Apply(_path);
        HexAddressInput.Configure(_start);
        HexAddressInput.Configure(_end);
        _scope.SelectedIndex = (int)initialScope;
        if (initialStart is uint start && initialEnd is uint end)
        {
            _start.Text = Math.Min(start, end).ToString("X8");
            _end.Text = Math.Max(start, end).ToString("X8");
        }
        _recordSize.SelectedItem = previousRecordSize;
        foreach (var row in segments) _segments.Items.Add($"{row.StartAddress} – {row.EndAddress} ({row.ByteCount:N0} bytes)");
        foreach (var index in selectedSegments.Where(index => index >= 0 && index < segments.Count)) _segments.SelectedItems.Add(_segments.Items[index]);

        var root = new Grid { Margin = new Thickness(16) };
        for (var i = 0; i < 6; i++) root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(135) });
        root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        AddRow(root, 0, "Export scope", _scope);
        AddRow(root, 1, "Target path", BuildPathPicker());
        AddRow(root, 2, "Format", _format);
        AddRow(root, 3, "Max bytes per record", _recordSize);
        AddRow(root, 4, "Address range", BuildRange());
        AddRow(root, 5, "Selected segments", _segments);
        var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 14, 0, 0) };
        var export = new Button { Content = "Export", IsDefault = true, MinWidth = 82, Margin = new Thickness(0, 0, 8, 0), Padding = new Thickness(10, 5, 10, 5) };
        export.Click += (_, _) => Accept();
        var cancel = new Button { Content = "Cancel", IsCancel = true, MinWidth = 82, Padding = new Thickness(10, 5, 10, 5) };
        buttons.Children.Add(export);
        buttons.Children.Add(cancel);
        Grid.SetRow(buttons, 6);
        Grid.SetColumn(buttons, 1);
        root.Children.Add(buttons);
        Content = root;
        _scope.SelectionChanged += (_, _) => UpdateScopeControls();
        UpdateScopeControls();
    }

    public ExportOptions? Options { get; private set; }

    private FrameworkElement BuildPathPicker()
    {
        var panel = new DockPanel();
        var browse = new Button { Content = "Browse…", Margin = new Thickness(6, 0, 0, 0), Padding = new Thickness(8, 4, 8, 4) };
        browse.Click += (_, _) =>
        {
            var format = _format.SelectedItem is FormatChoice selected ? selected.Format : MemoryImageExportFormat.IntelHex;
            var intel = format == MemoryImageExportFormat.IntelHex;
            var picker = new Microsoft.Win32.SaveFileDialog
            {
                Title = "Choose export destination",
                Filter = intel ? "Intel HEX (*.hex)|*.hex|All files (*.*)|*.*" : "Motorola S-Record (*.srec)|*.srec|All files (*.*)|*.*",
                DefaultExt = intel ? ".hex" : ".srec",
                AddExtension = true,
                OverwritePrompt = false,
                FileName = _path.Text
            };
            if (picker.ShowDialog(this) == true) _path.Text = picker.FileName;
        };
        DockPanel.SetDock(browse, Dock.Right);
        panel.Children.Add(browse);
        panel.Children.Add(_path);
        return panel;
    }

    private FrameworkElement BuildRange()
    {
        var panel = new StackPanel();
        panel.Children.Add(AddressFieldRow.Create("Start", _start));
        panel.Children.Add(AddressFieldRow.Create("End", _end, new Thickness(0, 6, 0, 0)));
        return panel;
    }

    private static void AddRow(Grid grid, int row, string label, FrameworkElement control)
    {
        var title = new TextBlock { Text = label, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 5, 8, 5) };
        Grid.SetRow(title, row);
        Grid.SetColumn(title, 0);
        Grid.SetRow(control, row);
        Grid.SetColumn(control, 1);
        control.Margin = new Thickness(0, 4, 0, 4);
        grid.Children.Add(title);
        grid.Children.Add(control);
    }

    private void UpdateScopeControls()
    {
        _segments.IsEnabled = _scope.SelectedIndex == (int)ExportScope.SelectedSegments;
        _start.IsEnabled = _end.IsEnabled = _scope.SelectedIndex == (int)ExportScope.AddressRange;
    }

    private void Accept()
    {
        if (_format.SelectedItem is not FormatChoice selectedFormat || _recordSize.SelectedItem is not int recordSize)
        {
            MessageBox.Show(this, "Choose an output format and 8, 16, or 32 bytes per record.", "Export options required", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        var format = selectedFormat.Format;
        if (string.IsNullOrWhiteSpace(_path.Text))
        {
            MessageBox.Show(this, "Choose a target path.", "Export path required", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        var scope = (ExportScope)_scope.SelectedIndex;
        uint start = 0, end = 0;
        if (scope == ExportScope.AddressRange &&
            (!HexAddressParser.TryParse(_start.Text, out start) || !HexAddressParser.TryParse(_end.Text, out end) || start > end))
        {
            MessageBox.Show(this, "Enter a valid inclusive 32-bit address range.", "Invalid export range", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        var indexes = scope == ExportScope.SelectedSegments
            ? _segments.SelectedItems.Cast<object>().Select(_segments.Items.IndexOf).ToArray()
            : Array.Empty<int>();
        if (scope == ExportScope.SelectedSegments && indexes.Length == 0)
        {
            MessageBox.Show(this, "Select at least one occupied memory segment.", "No segments selected", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        if (System.IO.File.Exists(_path.Text) && MessageBox.Show(this,
                $"Replace the existing file at {System.IO.Path.GetFullPath(_path.Text)}?", "Confirm export overwrite",
                MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes)
            return;
        Options = new ExportOptions(scope, _path.Text, format, recordSize, start, end, indexes);
        DialogResult = true;
    }

    private sealed record FormatChoice(MemoryImageExportFormat Format, string DisplayName)
    {
        public override string ToString() => DisplayName;
    }
}
