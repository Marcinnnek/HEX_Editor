using HexEditor.Core;
using System.Collections.ObjectModel;
using System.IO;

namespace HexEditor.UI;

public sealed class CompareWorkspaceTabViewModel
{
    public CompareWorkspaceTabViewModel(MemoryImage fileA, MemoryImage fileB, string nameA, string nameB,
        string? pathA = null, string? pathB = null)
    {
        ArgumentNullException.ThrowIfNull(fileA);
        ArgumentNullException.ThrowIfNull(fileB);
        NameA = nameA;
        NameB = nameB;
        _fileA = fileA;
        _fileB = fileB;
        _pathA = NormalizePath(pathA);
        _pathB = NormalizePath(pathB);
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
    private readonly string? _pathA;
    private readonly string? _pathB;

    public void RefreshDocument(string? documentPath, MemoryImage updatedImage)
    {
        ArgumentNullException.ThrowIfNull(updatedImage);
        var path = NormalizePath(documentPath);
        if (path is null) return;

        var updateA = PathsMatch(_pathA, path);
        var updateB = PathsMatch(_pathB, path);
        RefreshImages(updatedImage, updateA, updateB);
    }

    public void RefreshImage(MemoryImage previousImage, MemoryImage updatedImage)
    {
        var updateA = ReferenceEquals(_fileA, previousImage);
        var updateB = ReferenceEquals(_fileB, previousImage);
        RefreshImages(updatedImage, updateA, updateB);
    }

    private void RefreshImages(MemoryImage updatedImage, bool updateA, bool updateB)
    {
        if (!updateA && !updateB) return;
        if (updateA) _fileA = updatedImage;
        if (updateB) _fileB = updatedImage;
        ReplaceRows(BlocksA, _fileA, updateA);
        ReplaceRows(BlocksB, _fileB, updateB);
        Comparison.Refresh(_fileA, _fileB);
    }

    private static string? NormalizePath(string? path) => string.IsNullOrWhiteSpace(path) ? null : Path.GetFullPath(path);

    private static bool PathsMatch(string? left, string right) =>
        left is not null && string.Equals(left, right, StringComparison.OrdinalIgnoreCase);

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
