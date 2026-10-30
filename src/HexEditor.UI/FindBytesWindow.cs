using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace HexEditor.UI;

/// <summary>Single nonmodal input surface for searching an Editor byte sequence.</summary>
public sealed class FindBytesWindow : Window
{
    private readonly ComboBox _mode = new() { MinWidth = 90, Margin = new Thickness(0, 0, 8, 0) };
    private readonly TextBox _pattern = new() { MinWidth = 220 };
    private readonly TextBlock _status = new() { Margin = new Thickness(0, 8, 0, 0), TextWrapping = TextWrapping.Wrap };
    private readonly ProgressBar _progress = new() { Height = 8, Margin = new Thickness(0, 6, 0, 0), Visibility = Visibility.Collapsed };
    private string _hexDraft = string.Empty;
    private string _asciiDraft = string.Empty;
    private FindPatternMode _previousMode;
    private Button? _nextButton;
    private Button? _previousButton;

    public FindBytesWindow()
    {
        Title = "Find bytes";
        Width = 440;
        SizeToContent = SizeToContent.Height;
        MinHeight = 150;
        ResizeMode = ResizeMode.NoResize;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        ShowInTaskbar = false;

        _mode.Items.Add("HEX");
        _mode.Items.Add("ASCII");
        _mode.SelectedIndex = 0;
        _pattern.TextChanged += (_, _) => SaveDraft();
        _mode.SelectionChanged += Mode_SelectionChanged;

        var input = new Grid { Margin = new Thickness(12, 12, 12, 0) };
        input.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        input.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        input.Children.Add(_mode);
        Grid.SetColumn(_pattern, 1);
        input.Children.Add(_pattern);

        var next = new Button { Content = "Find Next", MinWidth = 82, Margin = new Thickness(4, 0, 0, 0), IsDefault = true };
        var previous = new Button { Content = "Find Previous", MinWidth = 96, Margin = new Thickness(4, 0, 0, 0) };
        var close = new Button { Content = "Close", MinWidth = 64, Margin = new Thickness(4, 0, 0, 0), IsCancel = true };
        var cancel = new Button { Content = "Cancel search", MinWidth = 96, Margin = new Thickness(4, 0, 0, 0), Visibility = Visibility.Collapsed };
        next.Click += (_, _) => FindRequested?.Invoke(this, new FindRequestedEventArgs(Mode, CurrentDraft, forward: true));
        previous.Click += (_, _) => FindRequested?.Invoke(this, new FindRequestedEventArgs(Mode, CurrentDraft, forward: false));
        close.Click += (_, _) => Close();
        cancel.Click += (_, _) => CancelRequested?.Invoke(this, EventArgs.Empty);
        CancelButton = cancel;
        _nextButton = next;
        _previousButton = previous;

        var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(12, 8, 12, 0) };
        buttons.Children.Add(next);
        buttons.Children.Add(previous);
        buttons.Children.Add(cancel);
        buttons.Children.Add(close);

        var content = new StackPanel();
        content.Children.Add(input);
        content.Children.Add(_status);
        content.Children.Add(_progress);
        content.Children.Add(buttons);
        Content = content;
        _status.Margin = new Thickness(12, 8, 12, 0);
        _progress.Margin = new Thickness(12, 6, 12, 0);
        _pattern.Focus();
        _pattern.SelectAll();
        PreviewKeyDown += Window_PreviewKeyDown;
    }

    public event EventHandler<FindRequestedEventArgs>? FindRequested;
    public event EventHandler? CancelRequested;

    public FindPatternMode Mode => _mode.SelectedIndex == 1 ? FindPatternMode.Ascii : FindPatternMode.Hex;
    public string CurrentDraft => _pattern.Text;
    public string HexDraft => _hexDraft;
    public string AsciiDraft => _asciiDraft;
    public TextBox PatternInput => _pattern;
    public ComboBox ModeSelector => _mode;
    public Button CancelButton { get; }

    public void SetDraft(FindPatternMode mode, string draft)
    {
        if (mode == FindPatternMode.Hex) _hexDraft = draft;
        else _asciiDraft = draft;
        _mode.SelectedIndex = mode == FindPatternMode.Hex ? 0 : 1;
        _pattern.Text = draft;
        _pattern.CaretIndex = _pattern.Text.Length;
    }

    public void SetStatus(string message) => _status.Text = message;

    public void SetProgress(ulong completed, ulong total)
    {
        _progress.Visibility = Visibility.Visible;
        _progress.IsIndeterminate = total == 0;
        if (total > 0)
        {
            _progress.Maximum = total;
            _progress.Value = Math.Min(completed, total);
        }
    }

    public void SetSearching(bool searching)
    {
        _progress.Visibility = searching ? Visibility.Visible : Visibility.Collapsed;
        _progress.IsIndeterminate = searching;
        CancelButton.Visibility = searching ? Visibility.Visible : Visibility.Collapsed;
        CancelButton.IsEnabled = searching;
        if (_nextButton is not null) _nextButton.IsEnabled = !searching;
        if (_previousButton is not null) _previousButton.IsEnabled = !searching;
        _pattern.IsEnabled = !searching;
        _mode.IsEnabled = !searching;
    }

    private void Mode_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_mode.SelectedIndex < 0) return;
        if (_previousMode == FindPatternMode.Hex) _hexDraft = _pattern.Text;
        else _asciiDraft = _pattern.Text;
        var newMode = Mode;
        if (newMode == _previousMode) return;
        _previousMode = newMode;
        _pattern.Text = newMode == FindPatternMode.Hex ? _hexDraft : _asciiDraft;
        _pattern.CaretIndex = _pattern.Text.Length;
    }

    private void SaveDraft()
    {
        if (_mode.SelectedIndex == 1) _asciiDraft = _pattern.Text;
        else _hexDraft = _pattern.Text;
    }

    private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.F && (Keyboard.Modifiers & ModifierKeys.Control) != 0)
        {
            _pattern.Focus();
            _pattern.SelectAll();
            e.Handled = true;
            return;
        }
        if (e.Key == Key.F3)
        {
            FindRequested?.Invoke(this, new FindRequestedEventArgs(Mode, CurrentDraft,
                forward: (Keyboard.Modifiers & ModifierKeys.Shift) == 0));
            e.Handled = true;
        }
    }
}

public sealed class FindRequestedEventArgs(FindPatternMode mode, string draft, bool forward) : EventArgs
{
    public FindPatternMode Mode { get; } = mode;
    public string Draft { get; } = draft;
    public bool Forward { get; } = forward;
}
