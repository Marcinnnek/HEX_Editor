using System.Windows;
using System.Windows.Controls;
using HexEditor.Core;

namespace HexEditor.UI;

public sealed class BankRemapDialog : Window
{
    private readonly MemoryImage _image;
    private readonly TextBox _sourceStart = new();
    private readonly TextBox _sourceEnd = new();
    private readonly TextBox _bankSize = new() { Text = "10000" };
    private readonly TextBox _bankOffset = new() { Text = "10000" };
    private readonly TextBox _linearBase = new();
    private readonly TextBlock _summary = new() { TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 10, 0, 8) };
    private readonly ComboBox _defaultChoice = new() { ItemsSource = new[] { "Overwrite existing", "Keep existing" }, SelectedIndex = 0 };
    private readonly StackPanel _collisionRows = new();
    private readonly List<(MemoryImageOverlapRange Range, ComboBox Choice)> _overrides = new();
    private readonly Button _apply = new() { Content = "Apply remap", IsDefault = true, IsEnabled = false, MinWidth = 100 };
    private int _previewGeneration;

    public BankRemapDialog(MemoryImage image)
    {
        _image = image ?? throw new ArgumentNullException(nameof(image));
        if (image.Ranges.Count == 0) throw new ArgumentException("The image has no occupied bytes.", nameof(image));
        Title = "Remap bank addresses";
        Width = 580;
        Height = 560;
        MinWidth = 500;
        MinHeight = 420;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        _sourceStart.Text = image.Ranges[0].StartAddress.ToString("X8");
        _sourceEnd.Text = image.Ranges[^1].EndAddress.ToString("X8");
        foreach (var box in new[] { _sourceStart, _sourceEnd, _bankSize, _bankOffset, _linearBase })
        {
            HexAddressInput.Configure(box);
            box.TextChanged += (_, _) => InvalidatePreview();
        }

        var root = new DockPanel { Margin = new Thickness(16) };
        var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
        var previewButton = new Button { Content = "Preview", MinWidth = 90, Margin = new Thickness(0, 0, 8, 0) };
        previewButton.Click += Preview_Click;
        _apply.Click += Apply_Click;
        buttons.Children.Add(previewButton);
        buttons.Children.Add(_apply);
        buttons.Children.Add(new Button { Content = "Cancel", IsCancel = true, MinWidth = 80, Margin = new Thickness(8, 0, 0, 0) });
        DockPanel.SetDock(buttons, Dock.Bottom);
        root.Children.Add(buttons);

        var fields = new StackPanel();
        fields.Children.Add(AddressFieldRow.Create("Source start (hex)", _sourceStart, new Thickness(0, 0, 0, 5)));
        fields.Children.Add(AddressFieldRow.Create("Source end (hex)", _sourceEnd, new Thickness(0, 0, 0, 5)));
        fields.Children.Add(AddressFieldRow.Create("Single bank size (hex)", _bankSize, new Thickness(0, 0, 0, 5)));
        fields.Children.Add(AddressFieldRow.Create("Offset between banks (hex)", _bankOffset, new Thickness(0, 0, 0, 5)));
        fields.Children.Add(AddressFieldRow.Create("Linear base address (hex)", _linearBase, new Thickness(0, 0, 0, 8)));
        fields.Children.Add(_summary);
        fields.Children.Add(new TextBlock { Text = "Default collision choice", Margin = new Thickness(0, 5, 0, 4) });
        fields.Children.Add(_defaultChoice);
        fields.Children.Add(new TextBlock { Text = "Collision range exceptions", Margin = new Thickness(0, 10, 0, 4) });
        fields.Children.Add(_collisionRows);
        root.Children.Add(new ScrollViewer { Content = fields, VerticalScrollBarVisibility = ScrollBarVisibility.Auto });
        Content = root;
    }

    public MemoryBankRemapPreview? Preview { get; private set; }
    public MemoryImageMergeChoice DefaultChoice => _defaultChoice.SelectedIndex == 1
        ? MemoryImageMergeChoice.KeepDestination : MemoryImageMergeChoice.KeepSource;
    public IReadOnlyList<MemoryImageMergeRangeOverride> RangeOverrides => _overrides
        .Where(item => item.Choice.SelectedIndex > 0)
        .Select(item => new MemoryImageMergeRangeOverride(item.Range.StartAddress, item.Range.EndAddress,
            item.Choice.SelectedIndex == 1 ? MemoryImageMergeChoice.KeepSource : MemoryImageMergeChoice.KeepDestination))
        .ToArray();

    private void InvalidatePreview()
    {
        _previewGeneration++;
        Preview = null;
        _apply.IsEnabled = false;
        _summary.Text = "Preview the remapped addresses before applying.";
        _collisionRows.Children.Clear();
        _overrides.Clear();
    }

    private async void Preview_Click(object sender, RoutedEventArgs e)
    {
        if (!HexAddressParser.TryParse(_sourceStart.Text, out var start) ||
            !HexAddressParser.TryParse(_sourceEnd.Text, out var end) ||
            !HexAddressParser.TryParse(_bankSize.Text, out var size) ||
            !HexAddressParser.TryParse(_bankOffset.Text, out var offset) ||
            !HexAddressParser.TryParse(_linearBase.Text, out var destination))
        {
            _summary.Text = "Enter five valid hexadecimal values; the linear base address is required.";
            return;
        }
        var configuration = new BankRemapConfiguration(start, end, size, offset, destination);
        var generation = ++_previewGeneration;
        _summary.Text = "Building remap preview...";
        try
        {
            var preview = await Task.Run(() => new MemoryBankRemapPreview(_image, configuration));
            if (generation != _previewGeneration) return;
            Preview = preview;
            _summary.Text = $"{preview.Moved.OccupiedByteCount:N0} byte(s) will move. " +
                $"{preview.Collisions.TotalOverlapByteCount:N0} destination byte(s) collide " +
                $"across {preview.Collisions.Overlaps.Count:N0} range(s). Keeping existing values discards colliding moved bytes.";
            foreach (var range in preview.Collisions.Overlaps)
            {
                var row = new DockPanel { Margin = new Thickness(0, 0, 0, 4) };
                var choice = new ComboBox { Width = 160, ItemsSource = new[] { "Use default", "Overwrite existing", "Keep existing" }, SelectedIndex = 0 };
                DockPanel.SetDock(choice, Dock.Right);
                row.Children.Add(choice);
                row.Children.Add(new TextBlock
                {
                    Text = $"0x{range.StartAddress:X8}–0x{range.EndAddress:X8}: {range.TotalByteCount:N0} byte(s)",
                    VerticalAlignment = VerticalAlignment.Center
                });
                _collisionRows.Children.Add(row);
                _overrides.Add((range, choice));
            }
            _apply.IsEnabled = true;
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException or OverflowException)
        {
            if (generation != _previewGeneration) return;
            InvalidatePreview();
            _summary.Text = exception.Message;
        }
    }

    private void Apply_Click(object sender, RoutedEventArgs e)
    {
        if (Preview is null) return;
        DialogResult = true;
    }
}
