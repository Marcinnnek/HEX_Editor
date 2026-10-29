using HexEditor.Core;
using System.Collections.ObjectModel;

namespace HexEditor.UI;

public sealed class CompareWorkspaceTabViewModel
{
    public CompareWorkspaceTabViewModel(MemoryImage fileA, MemoryImage fileB, string nameA, string nameB)
    {
        ArgumentNullException.ThrowIfNull(fileA);
        ArgumentNullException.ThrowIfNull(fileB);
        NameA = nameA;
        NameB = nameB;
        _fileA = fileA;
        _fileB = fileB;
        Comparison = new CompareViewModel(fileA, fileB);
        BlocksA = new ObservableCollection<CompareMemoryBlockRow>(CreateRows(fileA));
        BlocksB = new ObservableCollection<CompareMemoryBlockRow>(CreateRows(fileB));
    }

    public string NameA { get; }
    public string NameB { get; }
    public CompareViewModel Comparison { get; }
    public ObservableCollection<CompareMemoryBlockRow> BlocksA { get; }
    public ObservableCollection<CompareMemoryBlockRow> BlocksB { get; }

    private MemoryImage _fileA;
    private MemoryImage _fileB;

    public void RefreshImage(MemoryImage previousImage, MemoryImage updatedImage)
    {
        var updateA = ReferenceEquals(_fileA, previousImage);
        var updateB = ReferenceEquals(_fileB, previousImage);
        if (!updateA && !updateB) return;
        if (updateA) _fileA = updatedImage;
        if (updateB) _fileB = updatedImage;
        ReplaceRows(BlocksA, _fileA, updateA);
        ReplaceRows(BlocksB, _fileB, updateB);
        Comparison.Refresh(_fileA, _fileB);
    }

    private static void ReplaceRows(ObservableCollection<CompareMemoryBlockRow> rows, MemoryImage image, bool shouldReplace)
    {
        if (!shouldReplace) return;
        rows.Clear();
        foreach (var row in CreateRows(image)) rows.Add(row);
    }

    private static IReadOnlyList<CompareMemoryBlockRow> CreateRows(MemoryImage image) => image.Segments
        .Select(segment => new CompareMemoryBlockRow(segment.StartAddress, segment.EndAddress, segment.ByteCount))
        .ToArray();
}

public sealed record CompareMemoryBlockRow(uint StartAddress, uint EndAddress, int ByteCount)
{
    public string StartText => StartAddress.ToString("X8");
    public string EndText => EndAddress.ToString("X8");
}
