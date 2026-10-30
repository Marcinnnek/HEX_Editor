using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace HexEditor.UI;

/// <summary>Modal editor for proposed values in one occupied Editor row.</summary>
public sealed class EditRowWindow : Window
{
    private readonly EditRowSession _session;
    private readonly TextBox[] _hex;
    private readonly TextBox[] _ascii;
    private readonly HashSet<TextBox> _enteredInputs = [];
    private bool _syncing;

    public EditRowWindow(EditRowSession session)
    {
        _session = session ?? throw new ArgumentNullException(nameof(session));
        Title = $"Edit Row — {_session.ImageName}";
        // A 37-DIP byte column keeps two HEX digits and one ASCII character readable.
        // The work-area cap leaves scrolling for narrow screens and high display scaling.
        Width = Math.Min(120 + session.Cells.Count * 37 + 36, Math.Max(1, SystemParameters.WorkArea.Width - 24));
        MaxWidth = SystemParameters.WorkArea.Width;
        MaxHeight = SystemParameters.WorkArea.Height;
        SizeToContent = SizeToContent.Height;
        ResizeMode = ResizeMode.NoResize;
        WindowStartupLocation = WindowStartupLocation.CenterOwner; ShowInTaskbar = false;
        _hex = new TextBox[session.Cells.Count]; _ascii = new TextBox[session.Cells.Count];
        var root = new DockPanel { Margin = new Thickness(12) };
        var heading = new TextBlock
        {
            Text = $"{session.ImageName}     Row starts at 0x{session.RowStartAddress:X8}",
            FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 0, 0, 10),
            TextWrapping = TextWrapping.Wrap
        };
        DockPanel.SetDock(heading, Dock.Top); root.Children.Add(heading);
        var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 20, 10, 0) };
        var ok = new Button { Content = "OK", IsDefault = true, MinWidth = 80, Margin = new Thickness(10, 0, 0, 0), TabIndex = session.Cells.Count * 2 };
        var cancel = new Button { Content = "Cancel", IsCancel = true, MinWidth = 80, Margin = new Thickness(10, 0, 0, 0), TabIndex = session.Cells.Count * 2 + 1 };
        ok.Click += (_, _) => { if (_session.Commit()) { DialogResult = true; Close(); } else if (_session.IsValid) { DialogResult = true; Close(); } };
        cancel.Click += (_, _) => { _session.Cancel(); DialogResult = false; Close(); };
        buttons.Children.Add(ok); buttons.Children.Add(cancel); DockPanel.SetDock(buttons, Dock.Bottom); root.Children.Add(buttons);
        var scroll = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Auto };
        var grid = new Grid(); grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(112) });
        for (var i = 0; i < session.Cells.Count; i++) grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(37) });
        AddLabelRow(grid, 0, "Offset", cell => cell.OffsetText);
        AddLabelRow(grid, 1, "Original HEX", cell => cell.Hex);
        AddLabelRow(grid, 2, "Original ASCII", cell => cell.Ascii);
        AddRowHeader(grid, 3, "Proposed HEX");
        AddRowHeader(grid, 4, "Proposed ASCII");
        for (var i = 0; i < session.Cells.Count; i++)
        {
            var cell = session.Cells[i];
            _hex[i] = CreateInput(cell.Hex.Trim(), cell.IsOccupied, 2, i);
            _ascii[i] = CreateInput(cell.Ascii, cell.IsOccupied, 1, session.Cells.Count + i);
            var index = i;
            _hex[i].TextChanged += (_, _) => { if (_syncing) return; if (_session.SetHex(cell.Address!.Value, _hex[index].Text)) SyncAscii(index); };
            _ascii[i].TextChanged += (_, _) => { if (_syncing) return; if (_session.SetAscii(cell.Address!.Value, _ascii[index].Text)) SyncHex(index); };
            AddCell(grid, 3, i + 1, _hex[i]); AddCell(grid, 4, i + 1, _ascii[i]);
            if (cell.Address == session.SelectedAddress)
            {
                _hex[i].SetResourceReference(TextBox.BorderBrushProperty, "HexFocusBrush");
                _ascii[i].SetResourceReference(TextBox.BorderBrushProperty, "HexFocusBrush");
            }
        }
        scroll.Content = grid; root.Children.Add(scroll); Content = root;
        Loaded += (_, _) =>
        {
            var selectedIndex = session.Cells.ToList().FindIndex(cell => cell.Address == session.SelectedAddress);
            if (selectedIndex >= 0) _hex[selectedIndex].Focus();
        };
    }

    private void SyncAscii(int index)
    {
        var text = _hex[index].Text;
        if (text.Length is >= 1 and <= 2 && byte.TryParse(text, System.Globalization.NumberStyles.HexNumber, null, out var value))
        { _syncing = true; _ascii[index].Text = value is >= 0x20 and <= 0x7E ? ((char)value).ToString() : "."; _syncing = false; }
    }
    private void SyncHex(int index) { _syncing = true; _hex[index].Text = ((byte)_ascii[index].Text[0]).ToString("X2"); _syncing = false; }

    private TextBox CreateInput(string text, bool enabled, int maxLength, int tabIndex)
    {
        var input = new TextBox
        {
            Text = text, IsEnabled = enabled, IsTabStop = enabled, MaxLength = maxLength,
            Width = 32, Margin = new Thickness(2), TabIndex = tabIndex,
            HorizontalContentAlignment = HorizontalAlignment.Center,
            VerticalContentAlignment = VerticalAlignment.Center,
            FontFamily = new FontFamily("Consolas")
        };
        input.GotKeyboardFocus += (_, _) => SelectOnFirstEntry(input);
        input.PreviewMouseLeftButtonDown += (_, args) =>
        {
            if (input.IsKeyboardFocused) return;
            input.Focus();
            SelectOnFirstEntry(input);
            args.Handled = true;
        };
        return input;
    }

    private void SelectOnFirstEntry(TextBox input)
    {
        if (_enteredInputs.Add(input)) input.SelectAll();
    }
    private static void AddCell(Grid grid, int row, int col, UIElement element) { while (grid.RowDefinitions.Count <= row) grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); Grid.SetRow(element, row); Grid.SetColumn(element, col); grid.Children.Add(element); }
    private void AddLabelRow(Grid grid, int row, string header, Func<EditRowCell, string> value)
    {
        AddRowHeader(grid, row, header);
        for (var i = 0; i < _session.Cells.Count; i++) AddCell(grid, row, i + 1, new TextBlock
        {
            Text = value(_session.Cells[i]), Margin = new Thickness(2, 3, 2, 3),
            FontFamily = new FontFamily("Consolas"),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            TextAlignment = TextAlignment.Center
        });
    }
    private static void AddRowHeader(Grid grid, int row, string header) =>
        AddCell(grid, row, 0, new TextBlock
        {
            Text = header, Margin = new Thickness(4, 3, 4, 3), FontWeight = FontWeights.SemiBold,
            HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Center
        });
}
