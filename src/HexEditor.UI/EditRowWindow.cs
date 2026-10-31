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
        Width = Math.Min(120 + Math.Min(session.Cells.Count, 32) * 37 + 36, Math.Max(1, SystemParameters.WorkArea.Width - 24));
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
        var ok = new Button { Content = "OK", IsDefault = true, MinWidth = 80, Margin = new Thickness(10, 0, 0, 0) };
        var cancel = new Button { Content = "Cancel", IsCancel = true, MinWidth = 80, Margin = new Thickness(10, 0, 0, 0) };
        ok.Click += (_, _) => { if (_session.Commit()) { DialogResult = true; Close(); } else if (_session.IsValid) { DialogResult = true; Close(); } };
        cancel.Click += (_, _) => { _session.Cancel(); DialogResult = false; Close(); };
        buttons.Children.Add(ok); buttons.Children.Add(cancel); DockPanel.SetDock(buttons, Dock.Bottom); root.Children.Add(buttons);
        var groups = new StackPanel();
        var editableTabIndex = 0;
        for (var groupStart = 0; groupStart < session.Cells.Count; groupStart += 32)
        {
            var count = Math.Min(32, session.Cells.Count - groupStart);
            var grid = new Grid { Margin = new Thickness(0, groupStart == 0 ? 0 : 8, 0, 0) };
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(112) });
            for (var column = 0; column < count; column++) grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(37) });
            AddLabelRow(grid, 0, "Offset", cell => cell.OffsetText, groupStart, count);
            AddLabelRow(grid, 1, "Original HEX", cell => cell.Hex, groupStart, count);
            AddLabelRow(grid, 2, "Original ASCII", cell => cell.Ascii, groupStart, count);
            AddRowHeader(grid, 3, "Proposed HEX");
            AddRowHeader(grid, 4, "Proposed ASCII");
            for (var column = 0; column < count; column++)
            {
                var index = groupStart + column;
                var cell = session.Cells[index];
                _hex[index] = CreateInput(cell.Hex.Trim(), cell.IsOccupied, 2, 0);
                _ascii[index] = CreateInput(cell.Ascii, cell.IsOccupied, 1, 0);
                var capturedIndex = index;
                _hex[index].TextChanged += (_, _) => { if (_syncing) return; if (_session.SetHex(cell.Address!.Value, _hex[capturedIndex].Text)) SyncAscii(capturedIndex); };
                _ascii[index].TextChanged += (_, _) => { if (_syncing) return; if (_session.SetAscii(cell.Address!.Value, _ascii[capturedIndex].Text)) SyncHex(capturedIndex); };
                AddCell(grid, 3, column + 1, _hex[index]); AddCell(grid, 4, column + 1, _ascii[index]);
                if (cell.Address == session.SelectedAddress)
                {
                    _hex[index].SetResourceReference(TextBox.BorderBrushProperty, "HexFocusBrush");
                    _ascii[index].SetResourceReference(TextBox.BorderBrushProperty, "HexFocusBrush");
                }
            }

            for (var column = 0; column < count; column++)
            {
                var index = groupStart + column;
                if (!_session.Cells[index].IsOccupied) continue;
                _hex[index].TabIndex = editableTabIndex++;
            }
            for (var column = 0; column < count; column++)
            {
                var index = groupStart + column;
                if (!_session.Cells[index].IsOccupied) continue;
                _ascii[index].TabIndex = editableTabIndex++;
            }

            var horizontalScroll = new ScrollViewer
            {
                HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
                VerticalScrollBarVisibility = ScrollBarVisibility.Disabled,
                Content = grid
            };
            groups.Children.Add(horizontalScroll);
        }
        ok.TabIndex = editableTabIndex;
        cancel.TabIndex = editableTabIndex + 1;
        var scroll = new ScrollViewer
        {
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            Content = groups
        };
        root.Children.Add(scroll); Content = root;
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
    private void AddLabelRow(Grid grid, int row, string header, Func<EditRowCell, string> value, int groupStart, int count)
    {
        AddRowHeader(grid, row, header);
        for (var i = 0; i < count; i++) AddCell(grid, row, i + 1, new TextBlock
        {
            Text = value(_session.Cells[groupStart + i]), Margin = new Thickness(2, 3, 2, 3),
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
