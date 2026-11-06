using HexEditor.Core;
using HexEditor.Formats;

namespace HexEditor.UI;

public enum FillPolicy { KeepExisting, OverwriteExisting }

/// <summary>Owns the active image's edit operations, sparse clipboard, and undo/redo history.</summary>
public sealed class ImageEditingSession
{
    private readonly Stack<MemoryImage> _undo = new();
    private readonly Stack<MemoryImage> _redo = new();
    private MemoryImage _currentImage = MemoryImage.Empty;
    private EditClipboard? _clipboard;

    public event Action<MemoryImage, MemoryImage>? ImageChanged;

    public MemoryImage CurrentImage => _currentImage;
    public bool CanUndo => _undo.Count > 0;
    public bool CanRedo => _redo.Count > 0;
    public bool HasClipboard => _clipboard is not null;

    public void Reset(MemoryImage image)
    {
        ArgumentNullException.ThrowIfNull(image);
        _currentImage = image;
        _undo.Clear();
        _redo.Clear();
        _clipboard = null;
    }

    public void EditByte(uint address, byte value, OverlapDecision? overlapDecision = null)
    {
        var preview = PreviewInsert(address, [value]);
        var next = preview.Apply(overlapDecision);
        if (next is null) throw new InvalidOperationException("An explicit overlap decision is required for an occupied address.");
        Commit(next);
    }

    public void EditOccupiedBytes(IReadOnlyDictionary<uint, byte> changes)
    {
        ArgumentNullException.ThrowIfNull(changes);
        if (changes.Count == 0) return;
        if (changes.Keys.Any(address => !_currentImage.TryGetByte(address, out _)))
            throw new InvalidOperationException("Edit Row can change occupied bytes only.");
        Commit(_currentImage.WithByteChanges(changes));
    }

    public void InsertBytes(uint address, IReadOnlyList<byte> bytes, OverlapDecision? overlapDecision = null)
    {
        var preview = PreviewInsert(address, bytes);
        if (preview.OverlapCount > 0 && overlapDecision is null)
            throw new InvalidOperationException("An explicit overlap decision is required for occupied addresses.");
        var next = preview.Apply(overlapDecision);
        if (next is not null) Commit(next);
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

    public MemoryImageMergePreview PreviewMerge(MemoryImage source) => new(_currentImage, source);

    public OrderedMemoryMergePreview PreviewOrderedMerge(IEnumerable<MemoryImage> sources) =>
        new(_currentImage, sources, MemoryImageFileService.DefaultMaximumOccupiedByteCount);

    public void ApplyOrderedMerge(OrderedMemoryMergePreview preview)
    {
        ArgumentNullException.ThrowIfNull(preview);
        if (!ReferenceEquals(preview.BaseImage, _currentImage))
            throw new InvalidOperationException("The image changed after the merge preview was created. Preview again before applying.");
        EnsureWithinUserImageLimit(preview.Result.OccupiedByteCount);
        Commit(preview.Result);
    }

    public MemoryBankRemapPreview PreviewBankRemap(BankRemapConfiguration configuration) =>
        new(_currentImage, configuration);

    public bool ApplyBankRemap(MemoryBankRemapPreview preview, MemoryImageMergeChoice? defaultChoice,
        IEnumerable<MemoryImageMergeRangeOverride>? rangeOverrides = null)
    {
        ArgumentNullException.ThrowIfNull(preview);
        if (!ReferenceEquals(preview.Source, _currentImage))
            throw new InvalidOperationException("The image changed after the remap preview was created. Preview again before applying.");
        var result = preview.Resolve(defaultChoice, rangeOverrides);
        if (result is null) return false;
        EnsureWithinUserImageLimit(result.OccupiedByteCount);
        Commit(result);
        return true;
    }

    public void ApplyMerge(MemoryImageMergePreview preview, MemoryImageMergeChoice? defaultChoice,
        IEnumerable<MemoryImageMergeRangeOverride>? rangeOverrides = null)
    {
        ArgumentNullException.ThrowIfNull(preview);
        if (!ReferenceEquals(preview.Destination, _currentImage))
            throw new InvalidOperationException("The destination image changed after the merge preview was created. Preview the merge again before applying it.");
        if (defaultChoice is not null)
            EnsureWithinUserImageLimit(GetMergeOccupiedByteCount(preview.Destination, preview.Source));
        var merged = preview.Resolve(defaultChoice, rangeOverrides);
        if (merged is not null) Commit(merged);
    }

    public long CopyRange(uint startAddress, uint endAddress)
    {
        ValidateRange(startAddress, endAddress);
        var copied = _currentImage.Ranges
            .Where(range => range.EndAddress >= startAddress && range.StartAddress <= endAddress)
            .Select(range =>
            {
                var from = Math.Max(range.StartAddress, startAddress);
                var to = Math.Min(range.EndAddress, endAddress);
                return range.Slice(from, to).Rebase(checked((uint)((ulong)from - startAddress)));
            }).ToArray();
        _clipboard = new EditClipboard(MemoryImage.FromRanges(copied));
        return checked((long)_clipboard.Image.OccupiedByteCount);
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
        if (overlapDecision == OverlapDecision.Cancel) return;
        EnsureWithinUserImageLimit(checked(_currentImage.OccupiedByteCount + _clipboard.Image.OccupiedByteCount - (ulong)overlaps));
        Commit(MemoryImageEdits.Overlay(_currentImage, BuildPasteImage(address), overlapDecision ?? OverlapDecision.OverwriteIncoming));
    }

    public long GetSystemPasteOverlapCount(uint address, int byteCount)
    {
        if (byteCount <= 0 || (ulong)address + (uint)byteCount - 1 > uint.MaxValue)
            throw new ArgumentOutOfRangeException(nameof(byteCount), "Pasted bytes extend beyond the 32-bit address space.");
        var end = (ulong)address + (uint)byteCount - 1;
        long count = 0;
        foreach (var range in _currentImage.Ranges)
        {
            var first = Math.Max((ulong)range.StartAddress, address);
            var last = Math.Min((ulong)range.EndAddress, end);
            if (first <= last) count += checked((long)(last - first + 1));
        }
        return count;
    }

    public void PasteSystemBytes(uint address, IReadOnlyList<byte> bytes, OverlapDecision decision)
    {
        ArgumentNullException.ThrowIfNull(bytes);
        var overlaps = GetSystemPasteOverlapCount(address, bytes.Count);
        if (decision == OverlapDecision.Cancel) return;
        if (decision is not (OverlapDecision.KeepExisting or OverlapDecision.OverwriteIncoming))
            throw new ArgumentOutOfRangeException(nameof(decision));
        EnsureWithinUserImageLimit(checked(_currentImage.OccupiedByteCount + (ulong)bytes.Count - (ulong)overlaps));
        var input = bytes is byte[] array ? array : bytes.ToArray();
        var incoming = MemoryImage.FromSegments([new MemorySegment(address, input)]);
        Commit(_currentImage.WithRangesOverlay(incoming.Ranges, decision == OverlapDecision.OverwriteIncoming));
    }

    public long GetPasteOverlapCount(uint address)
    {
        if (_clipboard is null) throw new InvalidOperationException("Copy a byte range before pasting.");
        long count = 0;
        foreach (var range in _clipboard.Image.Ranges)
        {
            var targetStart = (ulong)address + range.StartAddress;
            var targetEnd = targetStart + range.ByteCount - 1;
            if (targetEnd > uint.MaxValue) throw new ArgumentOutOfRangeException(nameof(address), "The pasted bytes extend beyond the 32-bit address range.");
            foreach (var existing in _currentImage.Ranges)
            {
                var overlapStart = Math.Max(targetStart, existing.StartAddress);
                var overlapEnd = Math.Min(targetEnd, existing.EndAddress);
                if (overlapStart <= overlapEnd) count = checked(count + (long)(overlapEnd - overlapStart + 1));
            }
        }
        return count;
    }

    public void DeleteRange(uint startAddress, uint endAddress)
    {
        ValidateRange(startAddress, endAddress);
        Commit(_currentImage.WithoutRange(startAddress, endAddress));
    }

    public void ZeroOccupiedRange(uint startAddress, uint endAddress)
    {
        ValidateRange(startAddress, endAddress);
        Commit(_currentImage.ZeroOccupiedRange(startAddress, endAddress));
    }

    public void DeleteSegment(int segmentIndex)
    {
        var blocks = _currentImage.Blocks;
        if ((uint)segmentIndex >= (uint)blocks.Count)
            throw new ArgumentOutOfRangeException(nameof(segmentIndex));
        var range = blocks[segmentIndex];
        Commit(_currentImage.WithoutRange(range.StartAddress, range.EndAddress));
    }

    public void FillRange(uint startAddress, uint endAddress, IReadOnlyList<byte> pattern,
        FillPolicy policy = FillPolicy.KeepExisting)
    {
        ValidateFill(startAddress, endAddress, pattern, policy);
        EnsureWithinUserImageLimit(GetFilledOccupiedByteCount(startAddress, endAddress, policy));
        Commit(_currentImage.WithPatternRange(startAddress, endAddress, pattern.ToArray(), policy == FillPolicy.OverwriteExisting));
    }

    /// <summary>Builds a logical repeated range off-thread, then commits it as one undoable edit.</summary>
    public async Task FillRangeAsync(uint startAddress, uint endAddress, IReadOnlyList<byte> pattern,
        FillPolicy policy, CancellationToken cancellationToken, IProgress<MemoryImageFileProgress>? progress)
    {
        ValidateFill(startAddress, endAddress, pattern, policy);
        var priorImage = _currentImage;
        EnsureWithinUserImageLimit(GetFilledOccupiedByteCount(priorImage, startAddress, endAddress, policy));
        var fillPattern = pattern.ToArray();
        var addressCount = (long)((ulong)endAddress - startAddress + 1);
        cancellationToken.ThrowIfCancellationRequested();
        progress?.Report(new MemoryImageFileProgress("Preparing fill", 0, addressCount));
        cancellationToken.ThrowIfCancellationRequested();

        var nextImage = await Task.Run(() => priorImage.WithPatternRange(startAddress, endAddress,
            fillPattern, policy == FillPolicy.OverwriteExisting), cancellationToken);

        cancellationToken.ThrowIfCancellationRequested();
        if (!ReferenceEquals(_currentImage, priorImage))
            throw new InvalidOperationException("The image changed while Fill Range was being prepared. No fill was applied.");
        progress?.Report(new MemoryImageFileProgress("Committing fill", addressCount, addressCount));
        cancellationToken.ThrowIfCancellationRequested();
        Commit(nextImage);
    }

    private static void ValidateFill(uint startAddress, uint endAddress, IReadOnlyList<byte> pattern, FillPolicy policy)
    {
        ValidateRange(startAddress, endAddress);
        ArgumentNullException.ThrowIfNull(pattern);
        if (pattern.Count == 0) throw new ArgumentException("The fill pattern must contain at least one byte.", nameof(pattern));
        if (policy is not FillPolicy.KeepExisting and not FillPolicy.OverwriteExisting)
            throw new ArgumentOutOfRangeException(nameof(policy));
    }

    internal static void EnsureWithinUserImageLimit(ulong occupiedByteCount)
    {
        var limit = MemoryImageFileService.DefaultMaximumOccupiedByteCount;
        if (occupiedByteCount > limit)
            throw new InvalidOperationException($"The edit would exceed the limit of {limit.ToString("N0", System.Globalization.CultureInfo.InvariantCulture)} occupied bytes per image.");
    }

    private ulong GetFilledOccupiedByteCount(uint startAddress, uint endAddress, FillPolicy policy) =>
        GetFilledOccupiedByteCount(_currentImage, startAddress, endAddress, policy);

    private static ulong GetFilledOccupiedByteCount(MemoryImage image, uint startAddress, uint endAddress, FillPolicy policy)
    {
        ulong overlappingBytes = 0;
        foreach (var range in image.Ranges)
        {
            var overlapStart = Math.Max((ulong)startAddress, range.StartAddress);
            var overlapEnd = Math.Min((ulong)endAddress, range.EndAddress);
            if (overlapStart <= overlapEnd) overlappingBytes = checked(overlappingBytes + overlapEnd - overlapStart + 1);
        }

        var requestedBytes = (ulong)endAddress - startAddress + 1;
        return policy == FillPolicy.OverwriteExisting
            ? checked(image.OccupiedByteCount - overlappingBytes + requestedBytes)
            : checked(image.OccupiedByteCount + requestedBytes - overlappingBytes);
    }

    public MemoryRelocationPreview PrepareRelocation(int segmentIndex, uint destinationBaseAddress) =>
        _currentImage.PrepareRelocation(segmentIndex, destinationBaseAddress);

    public bool ApplyRelocation(MemoryRelocationPreview preview, RelocationOverlapDecision decision)
    {
        ArgumentNullException.ThrowIfNull(preview);
        var relocated = preview.Apply(decision);
        if (relocated is null) return false;
        EnsureWithinUserImageLimit(relocated.OccupiedByteCount);
        Commit(relocated);
        return true;
    }

    public bool Undo()
    {
        if (!_undo.TryPop(out var previous)) return false;
        var updated = _currentImage;
        _redo.Push(_currentImage);
        _currentImage = previous;
        ImageChanged?.Invoke(updated, previous);
        return true;
    }

    public bool Redo()
    {
        if (!_redo.TryPop(out var next)) return false;
        var previous = _currentImage;
        _undo.Push(_currentImage);
        _currentImage = next;
        ImageChanged?.Invoke(previous, next);
        return true;
    }

    private void Commit(MemoryImage next)
    {
        if (ImagesEqual(_currentImage, next)) return;
        var previous = _currentImage;
        _undo.Push(previous);
        _redo.Clear();
        _currentImage = next;
        ImageChanged?.Invoke(previous, next);
    }

    private MemoryImage BuildPasteImage(uint address)
    {
        if (_clipboard is null) throw new InvalidOperationException("Copy a byte range before pasting.");
        var ranges = new List<MemoryImageRange>(_clipboard.Image.Ranges.Count);
        foreach (var range in _clipboard.Image.Ranges)
        {
            var target = (ulong)address + range.StartAddress;
            var end = target + range.ByteCount - 1;
            if (end > uint.MaxValue)
                throw new ArgumentOutOfRangeException(nameof(address), "The pasted bytes extend beyond the 32-bit address range.");
            ranges.Add(range.Rebase((uint)target));
        }
        return MemoryImage.FromRanges(ranges);
    }

    private static bool ImagesEqual(MemoryImage left, MemoryImage right)
    {
        if (left.Ranges.Count != right.Ranges.Count) return false;
        for (var i = 0; i < left.Ranges.Count; i++)
            if (!left.Ranges[i].ContentEquals(right.Ranges[i])) return false;
        return true;
    }

    private static ulong GetMergeOccupiedByteCount(MemoryImage destination, MemoryImage source)
    {
        ulong overlap = 0;
        var destinationIndex = 0;
        var sourceIndex = 0;
        while (destinationIndex < destination.Ranges.Count && sourceIndex < source.Ranges.Count)
        {
            var destinationRange = destination.Ranges[destinationIndex];
            var sourceRange = source.Ranges[sourceIndex];
            var start = Math.Max((ulong)destinationRange.StartAddress, sourceRange.StartAddress);
            var end = Math.Min((ulong)destinationRange.EndAddress, sourceRange.EndAddress);
            if (start <= end) overlap = checked(overlap + end - start + 1);
            if (destinationRange.EndAddress <= sourceRange.EndAddress) destinationIndex++;
            if (sourceRange.EndAddress <= destinationRange.EndAddress) sourceIndex++;
        }
        return checked(destination.OccupiedByteCount + source.OccupiedByteCount - overlap);
    }

    private static void ValidateRange(uint startAddress, uint endAddress)
    {
        if (endAddress < startAddress) throw new ArgumentOutOfRangeException(nameof(endAddress), "The range end must not precede its start.");
    }

    private sealed record EditClipboard(MemoryImage Image);
}

public sealed record InsertPreview(MemoryImage ExistingImage, IReadOnlyDictionary<uint, byte> IncomingBytes, int OverlapCount)
{
    public MemoryImage? Apply(OverlapDecision? decision)
    {
        if (OverlapCount > 0 && decision is null) return null;
        if (decision == OverlapDecision.Cancel) return null;
        var applied = decision == OverlapDecision.OverwriteIncoming
            ? IncomingBytes
            : IncomingBytes.Where(pair => !ExistingImage.TryGetByte(pair.Key, out _))
                .ToDictionary(pair => pair.Key, pair => pair.Value);
        var newBytes = decision == OverlapDecision.OverwriteIncoming
            ? applied.Count - OverlapCount
            : applied.Count;
        ImageEditingSession.EnsureWithinUserImageLimit(checked(ExistingImage.OccupiedByteCount + (ulong)newBytes));
        return ExistingImage.WithByteChanges(applied);
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
        return existing.WithRangesOverlay(incoming.Ranges, decision == OverlapDecision.OverwriteIncoming);
    }
}
