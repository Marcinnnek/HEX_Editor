using System.Collections.ObjectModel;
using System.Windows;
using HexEditor.Core;
using FluentWindow = Wpf.Ui.Controls.FluentWindow;
using MessageBox = HexEditor.UI.ThemedMessageBox;

namespace HexEditor.UI;

public partial class MergeWindow : FluentWindow
{
    private readonly MemoryImage _imageA;
    private readonly MemoryImage _imageB;
    private readonly string _nameA;
    private readonly string _nameB;

    public MergeWindow(MemoryImage imageA, MemoryImage imageB, string nameA, string nameB)
    {
        ArgumentNullException.ThrowIfNull(imageA);
        ArgumentNullException.ThrowIfNull(imageB);
        InitializeComponent();
        _imageA = imageA;
        _imageB = imageB;
        _nameA = nameA;
        _nameB = nameB;
        DataContext = this;

        DirectionOptions =
        [
            new MergeDirectionOption($"Merge {_nameB} into {_nameA}", false),
            new MergeDirectionOption($"Merge {_nameA} into {_nameB}", true)
        ];
        ChoiceOptions =
        [
            new MergeChoiceOption("Keep destination values", MemoryImageMergeChoice.KeepDestination),
            new MergeChoiceOption("Keep source values", MemoryImageMergeChoice.KeepSource)
        ];
        DirectionBox.ItemsSource = DirectionOptions;
        DirectionBox.SelectedIndex = 0;
        DefaultChoiceBox.ItemsSource = ChoiceOptions;
        DefaultChoiceBox.SelectedIndex = 0;
        OverrideChoiceBox.ItemsSource = ChoiceOptions;
        OverrideChoiceBox.SelectedIndex = 1;
        RebuildPreview();
    }

    public ObservableCollection<MergeOverlapRow> OverlapRows { get; } = [];
    public ObservableCollection<MergeOverrideRow> OverrideRows { get; } = [];
    public MergeDirectionOption[] DirectionOptions { get; }
    public MergeChoiceOption[] ChoiceOptions { get; }
    public MemoryImageMergePreview Preview { get; private set; } = null!;

    public MemoryImage DestinationImage => Preview.Destination;
    public string DestinationName => ((MergeDirectionOption)DirectionBox.SelectedItem).Reverse ? _nameB : _nameA;
    public string SourceName => ((MergeDirectionOption)DirectionBox.SelectedItem).Reverse ? _nameA : _nameB;
    public MemoryImageMergeChoice DefaultChoice => ((MergeChoiceOption)DefaultChoiceBox.SelectedItem).Choice;
    public IReadOnlyList<MemoryImageMergeRangeOverride> RangeOverrides => OverrideRows.Select(row => row.Value).ToArray();

    private void Direction_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        if (DirectionBox.SelectedItem is not null && OverlapRows is not null) RebuildPreview();
    }

    private void RebuildPreview()
    {
        ApplyButton.IsEnabled = false;
        var reverse = ((MergeDirectionOption)DirectionBox.SelectedItem).Reverse;
        var destination = reverse ? _imageB : _imageA;
        var source = reverse ? _imageA : _imageB;
        Preview = new MemoryImageMergePreview(destination, source);
        OverlapRows.Clear();
        foreach (var overlap in Preview.Overlaps)
            OverlapRows.Add(new MergeOverlapRow(overlap));
        OverrideRows.Clear();
        DirectionSummary.Text = $"Destination: {DestinationName}    Source: {SourceName}";
        OverlapSummary.Text = Preview.TotalOverlapByteCount == 0
            ? "No occupied addresses overlap. All bytes will be combined by absolute address."
            : $"{Preview.TotalOverlapByteCount:N0} overlapping byte(s) across {Preview.Overlaps.Count:N0} range(s): " +
              $"{Preview.IdenticalOverlapByteCount:N0} identical and {Preview.DifferentOverlapByteCount:N0} different.";
    }

    private void Policy_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        if (ApplyButton is not null) ApplyButton.IsEnabled = false;
    }

    private void AddOverride_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var start = ParseAddress(OverrideStartBox.Text);
            var end = ParseAddress(OverrideEndBox.Text);
            if (end < start) throw new ArgumentException("The end address must not precede the start address.");
            var choice = ((MergeChoiceOption)OverrideChoiceBox.SelectedItem).Choice;
            var candidate = new MemoryImageMergeRangeOverride(start, end, choice);
            // Resolve validates that explicit override ranges do not conflict with existing overrides.
            Preview.Resolve(DefaultChoice, RangeOverrides.Append(candidate));
            OverrideRows.Add(new MergeOverrideRow(candidate));
            ApplyButton.IsEnabled = false;
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException)
        {
            MessageBox.Show(this, exception.Message, "Invalid merge override", MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }

    private void RemoveOverride_Click(object sender, RoutedEventArgs e)
    {
        if (OverrideList.SelectedItem is MergeOverrideRow row)
        {
            OverrideRows.Remove(row);
            ApplyButton.IsEnabled = false;
        }
    }

    private void PreviewResult_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var merged = Preview.Resolve(DefaultChoice, RangeOverrides);
            if (merged is null) return;
            ComparisonPreviewHost.Content = new CompareView(new CompareWorkspaceTabViewModel(
                DestinationImage, merged, $"{DestinationName} before", "Resolved merge"));
            MergePanel.Visibility = Visibility.Collapsed;
            ComparisonPreviewPanel.Visibility = Visibility.Visible;
            ApplyButton.IsEnabled = true;
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException)
        {
            MessageBox.Show(this, exception.Message, "Unable to preview merge", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void ReturnToMerge_Click(object sender, RoutedEventArgs e)
    {
        ComparisonPreviewHost.Content = null;
        ComparisonPreviewPanel.Visibility = Visibility.Collapsed;
        MergePanel.Visibility = Visibility.Visible;
    }

    private void Apply_Click(object sender, RoutedEventArgs e)
    {
        if (!ApplyButton.IsEnabled)
        {
            MessageBox.Show(this, "Preview the resolved result before applying the merge.", "Merge preview required", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        try
        {
            _ = Preview.Resolve(DefaultChoice, RangeOverrides);
            DialogResult = true;
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException)
        {
            MessageBox.Show(this, exception.Message, "Unable to apply merge", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void Cancel_Click(object sender, RoutedEventArgs e) => DialogResult = false;

    private static uint ParseAddress(string text)
        => HexAddressParser.Parse(text);
}

public sealed record MergeDirectionOption(string Label, bool Reverse)
{
    public override string ToString() => Label;
}

public sealed record MergeChoiceOption(string Label, MemoryImageMergeChoice Choice)
{
    public override string ToString() => Label;
}

public sealed record MergeOverlapRow(string StartAddress, string EndAddress, long TotalByteCount, long IdenticalByteCount, long DifferentByteCount)
{
    public MergeOverlapRow(MemoryImageOverlapRange overlap)
        : this(overlap.StartAddress.ToString("X8"), overlap.EndAddress.ToString("X8"), overlap.TotalByteCount, overlap.IdenticalByteCount, overlap.DifferentByteCount)
    {
    }
}

public sealed record MergeOverrideRow(MemoryImageMergeRangeOverride Value)
{
    public string Description => $"{Value.StartAddress:X8}–{Value.EndAddress:X8}: {Value.Choice switch { MemoryImageMergeChoice.KeepSource => "keep source", _ => "keep destination" }}";
}
