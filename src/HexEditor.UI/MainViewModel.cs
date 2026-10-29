using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using HexEditor.Core;
using HexEditor.Formats;

namespace HexEditor.UI;

public sealed class MainViewModel : INotifyPropertyChanged
{
    private readonly MemoryImageFileService _fileService = new();
    private readonly Stack<MemoryImage> _undo = new();
    private readonly Stack<MemoryImage> _redo = new();
    private MemoryImage _currentImage = MemoryImage.Empty;
    private MemoryImage _savedImage = MemoryImage.Empty;
    private HexImageViewModel _imageView = new(MemoryImage.Empty);
    private string _statusMessage = "Open an Intel HEX or Motorola S-Record file to inspect its occupied memory segments.";
    private string _windowTitle = "HEX Editor";
    private string? _sourcePath;
    private MemoryImageExportFormat? _sourceFormat;
    private object? _sourceFormatMetadata;
    private EditClipboard? _clipboard;

    public event PropertyChangedEventHandler? PropertyChanged;
    public event Action<MemoryImage, MemoryImage>? ImageChanged;

    public ObservableCollection<MemorySegmentRow> Segments { get; } = new();
    public int[] RowWidths { get; } = [8, 16, 32];

    public MemoryImage CurrentImage => _currentImage;
    public bool IsModified => !ReferenceEquals(_currentImage, _savedImage);
    public bool CanUndo => _undo.Count > 0;
    public bool CanRedo => _redo.Count > 0;
    public string? SourcePath => _sourcePath;
    public MemoryImageExportFormat? SourceFormat => _sourceFormat;
    public object? SourceFormatMetadata => _sourceFormatMetadata;

    public HexImageViewModel ImageView
    {
        get => _imageView;
        private set => SetProperty(ref _imageView, value);
    }

    public string StatusMessage { get => _statusMessage; private set => SetProperty(ref _statusMessage, value); }
    public string WindowTitle { get => _windowTitle; private set => SetProperty(ref _windowTitle, value); }

    public MemoryImageImportPreview PrepareOpen(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        using var stream = File.OpenRead(path);
        return _fileService.PrepareImport(stream, _currentImage, path);
    }

    public void ApplyImport(MemoryImageImportPreview preview, OverlapDecision decision)
    {
        ArgumentNullException.ThrowIfNull(preview);
        var hadExistingImage = _currentImage.Segments.Count > 0;
        var previousImage = _currentImage;
        var imported = preview.Apply(decision);
        if (imported is null) return;

        _undo.Clear();
        _redo.Clear();
        _currentImage = imported.Image;
        // An import applied over an already-open image creates merged contents that are not represented
        // by the newly associated source file, so keep them marked unsaved until the user saves explicitly.
        _savedImage = hadExistingImage ? MemoryImage.Empty : imported.Image;
        _sourcePath = imported.SourcePath;
        _sourceFormat = imported.InputFormat == "Intel HEX" ? MemoryImageExportFormat.IntelHex : MemoryImageExportFormat.SRecord;
        _sourceFormatMetadata = imported.FormatMetadata;
        RefreshImage();
        RefreshTitle();
        StatusMessage = DescribeImage(imported.InputFormat) + (IsModified ? " | merged changes not saved" : string.Empty);
        Raise(nameof(CurrentImage), nameof(IsModified), nameof(CanUndo), nameof(CanRedo), nameof(SourcePath), nameof(SourceFormat), nameof(SourceFormatMetadata));
        NotifyImageChanged(previousImage);
    }

    public void LoadImage(MemoryImage image, string? sourcePath, MemoryImageExportFormat? format)
    {
        ArgumentNullException.ThrowIfNull(image);
        _undo.Clear();
        _redo.Clear();
        _currentImage = _savedImage = image;
        _sourcePath = sourcePath;
        _sourceFormat = format;
        _sourceFormatMetadata = null;
        RefreshImage();
        RefreshTitle();
        StatusMessage = DescribeImage(format?.ToString());
        Raise(nameof(CurrentImage), nameof(IsModified), nameof(CanUndo), nameof(CanRedo), nameof(SourcePath), nameof(SourceFormat), nameof(SourceFormatMetadata));
    }

    public void CloseImage() => LoadImage(MemoryImage.Empty, null, null);

    public void EditByte(uint address, byte value, OverlapDecision? overlapDecision = null)
    {
        var preview = PreviewInsert(address, [value]);
        var next = preview.Apply(overlapDecision);
        if (next is null) throw new InvalidOperationException("An explicit overlap decision is required for an occupied address.");
        Commit(next);
    }

    public void InsertBytes(uint address, IReadOnlyList<byte> bytes, OverlapDecision? overlapDecision = null)
    {
        var preview = PreviewInsert(address, bytes);
        if (preview.OverlapCount > 0 && overlapDecision is null)
            throw new InvalidOperationException("An explicit overlap decision is required for occupied addresses.");
        var next = preview.Apply(overlapDecision);
        if (next is not null) Commit(next);
    }

    public MemoryImageMergePreview PreviewMerge(MemoryImage source) => new(_currentImage, source);

    public void ApplyMerge(
        MemoryImageMergePreview preview,
        MemoryImageMergeChoice? defaultChoice,
        IEnumerable<MemoryImageMergeRangeOverride>? rangeOverrides = null)
    {
        ArgumentNullException.ThrowIfNull(preview);
        if (!ReferenceEquals(preview.Destination, _currentImage))
            throw new InvalidOperationException("The destination image changed after the merge preview was created. Preview the merge again before applying it.");
        var merged = preview.Resolve(defaultChoice, rangeOverrides);
        if (merged is not null) Commit(merged);
    }

    public InsertPreview PreviewInsert(uint address, IReadOnlyList<byte> bytes)
    {
        ArgumentNullException.ThrowIfNull(bytes);
        if (bytes.Count == 0) throw new ArgumentException("At least one byte is required.", nameof(bytes));
        if ((ulong)address + (ulong)bytes.Count - 1 > uint.MaxValue)
            throw new ArgumentOutOfRangeException(nameof(bytes), "The inserted bytes extend beyond the 32-bit address range.");
        var incoming = new Dictionary<uint, byte>(bytes.Count);
        for (var i = 0; i < bytes.Count; i++) incoming[address + (uint)i] = bytes[i];
        var overlapCount = incoming.Keys.Count(address => _currentImage.TryGetByte(address, out _));
        return new InsertPreview(_currentImage, incoming, overlapCount);
    }

    public void CopyRange(uint startAddress, uint endAddress)
    {
        ValidateRange(startAddress, endAddress);
        var copied = _currentImage.Segments
            .Where(segment => segment.EndAddress >= startAddress && segment.StartAddress <= endAddress)
            .Select(segment =>
            {
                var from = Math.Max(segment.StartAddress, startAddress);
                var to = Math.Min(segment.EndAddress, endAddress);
                var offset = checked((int)(from - segment.StartAddress));
                var count = checked((int)((ulong)to - from + 1));
                return new MemorySegment(checked((uint)((ulong)from - startAddress)), segment.Data.Span.Slice(offset, count));
            })
            .ToArray();
        _clipboard = new EditClipboard(copied);
        StatusMessage = $"Copied {copied.Sum(segment => (long)segment.ByteCount):N0} occupied byte(s), preserving address gaps.";
    }

    public void CutRange(uint startAddress, uint endAddress)
    {
        CopyRange(startAddress, endAddress);
        DeleteRange(startAddress, endAddress);
    }

    public void PasteAt(uint address, OverlapDecision? overlapDecision = null)
    {
        if (_clipboard is null) throw new InvalidOperationException("Copy a byte range before pasting.");
        var overlaps = GetPasteOverlapCount(address);
        if (overlaps > 0 && overlapDecision is null)
            throw new InvalidOperationException($"Paste overlaps {overlaps:N0} occupied byte(s); choose whether to keep or overwrite them.");
        Commit(MemoryImageEdits.Overlay(_currentImage, BuildPasteImage(address), overlapDecision ?? OverlapDecision.OverwriteIncoming));
    }

    public int GetPasteOverlapCount(uint address)
    {
        if (_clipboard is null) throw new InvalidOperationException("Copy a byte range before pasting.");
        var count = 0;
        foreach (var segment in _clipboard.Segments)
        {
            var targetStart = (ulong)address + segment.StartAddress;
            var targetEnd = targetStart + (ulong)segment.ByteCount - 1;
            if (targetEnd > uint.MaxValue) throw new ArgumentOutOfRangeException(nameof(address), "The pasted bytes extend beyond the 32-bit address range.");
            for (var i = 0; i < segment.ByteCount; i++)
                if (_currentImage.TryGetByte((uint)(targetStart + (uint)i), out _)) count++;
        }
        return count;
    }

    public void DeleteRange(uint startAddress, uint endAddress)
    {
        ValidateRange(startAddress, endAddress);
        var kept = new List<MemorySegment>();
        foreach (var segment in _currentImage.Segments)
        {
            if (segment.EndAddress < startAddress || segment.StartAddress > endAddress) { kept.Add(segment); continue; }
            if (segment.StartAddress < startAddress)
            {
                var leftLength = checked((int)(startAddress - segment.StartAddress));
                kept.Add(new MemorySegment(segment.StartAddress, segment.Data.Span[..leftLength]));
            }
            if (segment.EndAddress > endAddress)
            {
                var rightOffset = checked((int)((ulong)endAddress + 1 - segment.StartAddress));
                kept.Add(new MemorySegment(checked(endAddress + 1), segment.Data.Span[rightOffset..]));
            }
        }
        Commit(MemoryImage.FromSegments(kept));
    }

    public void FillRange(uint startAddress, uint endAddress, IReadOnlyList<byte> pattern)
    {
        ValidateRange(startAddress, endAddress);
        ArgumentNullException.ThrowIfNull(pattern);
        if (pattern.Count == 0) throw new ArgumentException("The fill pattern must contain at least one byte.", nameof(pattern));
        var length = (ulong)endAddress - startAddress + 1;
        if (length > int.MaxValue) throw new ArgumentOutOfRangeException(nameof(endAddress), "The selected range is too large to represent as a memory segment.");
        var filled = new byte[(int)length];
        for (var offset = 0; offset < filled.Length; offset++) filled[offset] = pattern[offset % pattern.Count];
        Commit(MemoryImageEdits.Overlay(_currentImage, MemoryImage.FromSegments([new MemorySegment(startAddress, filled)]), OverlapDecision.OverwriteIncoming));
    }

    public bool Undo()
    {
        if (!_undo.TryPop(out var previous)) return false;
        var changedImage = _currentImage;
        _redo.Push(_currentImage);
        _currentImage = previous;
        RefreshImage();
        RefreshModifiedTitle();
        Raise(nameof(CurrentImage), nameof(IsModified), nameof(CanUndo), nameof(CanRedo));
        NotifyImageChanged(changedImage);
        return true;
    }

    public bool Redo()
    {
        if (!_redo.TryPop(out var next)) return false;
        var changedImage = _currentImage;
        _undo.Push(_currentImage);
        _currentImage = next;
        RefreshImage();
        RefreshModifiedTitle();
        Raise(nameof(CurrentImage), nameof(IsModified), nameof(CanUndo), nameof(CanRedo));
        NotifyImageChanged(changedImage);
        return true;
    }

    public void Save()
    {
        if (string.IsNullOrWhiteSpace(_sourcePath) || _sourceFormat is null)
            throw new InvalidOperationException("Choose Save As and select a file format first.");
        SaveAs(_sourcePath, _sourceFormat.Value, overwriteExisting: true);
    }

    public void SaveAs(string path, MemoryImageExportFormat format, bool overwriteExisting)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        _fileService.SaveAtomically(_currentImage, path, format, 16, overwriteExisting);
        _sourcePath = Path.GetFullPath(path);
        _sourceFormat = format;
        _sourceFormatMetadata = null;
        _savedImage = _currentImage;
        RefreshTitle();
        StatusMessage = $"Saved {_currentImage.Segments.Sum(segment => (long)segment.ByteCount):N0} bytes as {format}.";
        Raise(nameof(SourcePath), nameof(SourceFormat), nameof(SourceFormatMetadata), nameof(IsModified));
    }

    public bool TryNavigateToAddress(uint address, out int rowIndex) => ImageView.TryNavigateToAddress(address, out rowIndex);
    public bool TryNavigateToSegment(int segmentIndex, out int rowIndex) => ImageView.TryNavigateToSegment(segmentIndex, out rowIndex);

    private void Commit(MemoryImage next)
    {
        if (ImagesEqual(_currentImage, next)) return;
        var previousImage = _currentImage;
        _undo.Push(_currentImage);
        _redo.Clear();
        _currentImage = next;
        RefreshImage();
        RefreshModifiedTitle();
        StatusMessage = "Image modified. Save to write changes to disk.";
        Raise(nameof(CurrentImage), nameof(IsModified), nameof(CanUndo), nameof(CanRedo));
        NotifyImageChanged(previousImage);
    }

    private void NotifyImageChanged(MemoryImage previousImage)
    {
        if (!ReferenceEquals(previousImage, _currentImage)) ImageChanged?.Invoke(previousImage, _currentImage);
    }

    private void RefreshImage()
    {
        ImageView = new HexImageViewModel(_currentImage);
        Segments.Clear();
        foreach (var segment in _currentImage.Segments)
            Segments.Add(new MemorySegmentRow(segment.StartAddress.ToString("X8"), segment.EndAddress.ToString("X8"), segment.ByteCount));
    }

    private void RefreshTitle() => WindowTitle = $"{(string.IsNullOrWhiteSpace(_sourcePath) ? "Untitled" : Path.GetFileName(_sourcePath))}{(IsModified ? " *" : "")} - HEX Editor";
    private void RefreshModifiedTitle() => RefreshTitle();

    private string DescribeImage(string? format)
    {
        var totalBytes = _currentImage.Segments.Sum(segment => (long)segment.ByteCount);
        return $"{_currentImage.Segments.Count:N0} segments, {totalBytes:N0} bytes" + (format is null ? string.Empty : $" | {format}");
    }

    private MemoryImage BuildPasteImage(uint address)
    {
        if (_clipboard is null) throw new InvalidOperationException("Copy a byte range before pasting.");
        var segments = new List<MemorySegment>(_clipboard.Segments.Length);
        foreach (var segment in _clipboard.Segments)
        {
            var target = (ulong)address + segment.StartAddress;
            if (target + (ulong)segment.ByteCount - 1 > uint.MaxValue)
                throw new ArgumentOutOfRangeException(nameof(address), "The pasted bytes extend beyond the 32-bit address range.");
            segments.Add(new MemorySegment((uint)target, segment.Data.Span));
        }
        return MemoryImage.FromSegments(segments);
    }

    private static MemoryImage BuildImage(IEnumerable<KeyValuePair<uint, byte>> values)
    {
        var ordered = values.OrderBy(pair => pair.Key).ToArray();
        if (ordered.Length == 0) return MemoryImage.Empty;
        var segments = new List<MemorySegment>();
        var start = ordered[0].Key;
        var prior = start;
        var data = new List<byte> { ordered[0].Value };
        for (var i = 1; i < ordered.Length; i++)
        {
            var pair = ordered[i];
            if ((ulong)prior + 1 == pair.Key) data.Add(pair.Value);
            else
            {
                segments.Add(new MemorySegment(start, data.ToArray()));
                start = pair.Key;
                data = [pair.Value];
            }
            prior = pair.Key;
        }
        segments.Add(new MemorySegment(start, data.ToArray()));
        return MemoryImage.FromSegments(segments);
    }

    private static bool ImagesEqual(MemoryImage left, MemoryImage right)
    {
        if (left.Segments.Count != right.Segments.Count) return false;
        for (var i = 0; i < left.Segments.Count; i++)
            if (left.Segments[i].StartAddress != right.Segments[i].StartAddress || !left.Segments[i].Data.Span.SequenceEqual(right.Segments[i].Data.Span)) return false;
        return true;
    }

    private static void ValidateRange(uint startAddress, uint endAddress)
    {
        if (endAddress < startAddress) throw new ArgumentOutOfRangeException(nameof(endAddress), "The range end must not precede its start.");
    }

    private void Raise(params string[] names)
    {
        foreach (var name in names) PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    private void SetProperty<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return;
        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    private sealed record EditClipboard(MemorySegment[] Segments);
}

public sealed record InsertPreview(MemoryImage ExistingImage, IReadOnlyDictionary<uint, byte> IncomingBytes, int OverlapCount)
{
    public MemoryImage? Apply(OverlapDecision? decision)
    {
        if (OverlapCount > 0 && decision is null) return null;
        if (decision == OverlapDecision.Cancel) return null;
        return MemoryImageEdits.Overlay(ExistingImage, MemoryImageEdits.FromBytes(IncomingBytes), decision ?? OverlapDecision.KeepExisting);
    }
}

internal static class MemoryImageEdits
{
    public static MemoryImage FromBytes(IReadOnlyDictionary<uint, byte> bytes)
    {
        if (bytes.Count == 0) return MemoryImage.Empty;
        var ordered = bytes.OrderBy(pair => pair.Key).ToArray();
        var segments = new List<MemorySegment>();
        var start = ordered[0].Key;
        var prior = start;
        var data = new List<byte> { ordered[0].Value };
        for (var i = 1; i < ordered.Length; i++)
        {
            var pair = ordered[i];
            if ((ulong)prior + 1 == pair.Key) data.Add(pair.Value);
            else { segments.Add(new MemorySegment(start, data.ToArray())); start = pair.Key; data = [pair.Value]; }
            prior = pair.Key;
        }
        segments.Add(new MemorySegment(start, data.ToArray()));
        return MemoryImage.FromSegments(segments);
    }

    public static MemoryImage Overlay(MemoryImage existing, MemoryImage incoming, OverlapDecision decision)
    {
        if (decision == OverlapDecision.Cancel) return existing;
        if (existing.Segments.Count == 0) return incoming;
        if (incoming.Segments.Count == 0) return existing;
        var result = new List<MemorySegment>();
        var left = 0;
        var right = 0;
        ulong cursor = Math.Min(existing.Segments[0].StartAddress, incoming.Segments[0].StartAddress);
        var maximum = Math.Max(existing.Segments[^1].EndAddress, incoming.Segments[^1].EndAddress);
        ulong runStart = 0;
        ulong previousAddress = 0;
        var run = new List<byte>();
        while (cursor <= maximum)
        {
            Advance(existing.Segments, ref left, cursor);
            Advance(incoming.Segments, ref right, cursor);
            var hasLeft = TryValue(existing.Segments, left, cursor, out var leftValue);
            var hasRight = TryValue(incoming.Segments, right, cursor, out var rightValue);
            if (!hasLeft && !hasRight)
            {
                Flush();
                cursor = Math.Min(NextStart(existing.Segments, left), NextStart(incoming.Segments, right));
                if (cursor == ulong.MaxValue) break;
                continue;
            }
            if (run.Count == 0) runStart = cursor;
            else if (previousAddress + 1 != cursor) { Flush(); runStart = cursor; }
            run.Add(hasRight && (!hasLeft || decision == OverlapDecision.OverwriteIncoming) ? rightValue : leftValue);
            previousAddress = cursor;
            if (cursor == maximum) break;
            cursor++;
        }
        Flush();
        return MemoryImage.FromSegments(result);

        void Flush()
        {
            if (run.Count == 0) return;
            result.Add(new MemorySegment((uint)runStart, run.ToArray()));
            run.Clear();
        }
    }

    private static ulong NextStart(IReadOnlyList<MemorySegment> segments, int index) => index < segments.Count ? segments[index].StartAddress : ulong.MaxValue;
    private static void Advance(IReadOnlyList<MemorySegment> segments, ref int index, ulong address)
    {
        while (index < segments.Count && segments[index].EndAddress < address) index++;
    }
    private static bool TryValue(IReadOnlyList<MemorySegment> segments, int index, ulong address, out byte value)
    {
        if (index < segments.Count && address >= segments[index].StartAddress && address <= segments[index].EndAddress)
        {
            value = segments[index].Data.Span[(int)(address - segments[index].StartAddress)];
            return true;
        }
        value = default;
        return false;
    }
}

public sealed record MemorySegmentRow(string StartAddress, string EndAddress, int ByteCount);
