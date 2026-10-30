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
        Commit(MemoryImageSparseOverlay.Combine(_currentImage, MemoryImageEdits.FromBytes(changes),
            (_, hasExisting, existing, hasIncoming, incoming) => hasIncoming ? incoming : existing));
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

    public void ApplyMerge(MemoryImageMergePreview preview, MemoryImageMergeChoice? defaultChoice,
        IEnumerable<MemoryImageMergeRangeOverride>? rangeOverrides = null)
    {
        ArgumentNullException.ThrowIfNull(preview);
        if (!ReferenceEquals(preview.Destination, _currentImage))
            throw new InvalidOperationException("The destination image changed after the merge preview was created. Preview the merge again before applying it.");
        var merged = preview.Resolve(defaultChoice, rangeOverrides);
        if (merged is not null) Commit(merged);
    }

    public long CopyRange(uint startAddress, uint endAddress)
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
        return copied.Sum(segment => (long)segment.ByteCount);
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

    public void FillRange(uint startAddress, uint endAddress, IReadOnlyList<byte> pattern,
        FillPolicy policy = FillPolicy.KeepExisting)
    {
        ValidateRange(startAddress, endAddress);
        ArgumentNullException.ThrowIfNull(pattern);
        if (pattern.Count == 0) throw new ArgumentException("The fill pattern must contain at least one byte.", nameof(pattern));
        if (policy is not FillPolicy.KeepExisting and not FillPolicy.OverwriteExisting)
            throw new ArgumentOutOfRangeException(nameof(policy));
        var length = (ulong)endAddress - startAddress + 1;
        if (length > int.MaxValue) throw new ArgumentOutOfRangeException(nameof(endAddress), "The selected range is too large to represent as a memory segment.");
        var filled = new byte[(int)length];
        for (var offset = 0; offset < filled.Length; offset++) filled[offset] = pattern[offset % pattern.Count];
        if (policy == FillPolicy.KeepExisting)
        {
            var existingAndGaps = MemoryImage.FromSegments([new MemorySegment(startAddress, filled)]);
            Commit(MemoryImageSparseOverlay.Combine(_currentImage, existingAndGaps,
                (_, hasExisting, existing, _, incoming) => hasExisting ? existing : incoming));
            return;
        }
        Commit(MemoryImageEdits.Overlay(_currentImage, MemoryImage.FromSegments([new MemorySegment(startAddress, filled)]), OverlapDecision.OverwriteIncoming));
    }

    public MemoryRelocationPreview PrepareRelocation(int segmentIndex, uint destinationBaseAddress) =>
        _currentImage.PrepareRelocation(segmentIndex, destinationBaseAddress);

    public bool ApplyRelocation(MemoryRelocationPreview preview, RelocationOverlapDecision decision)
    {
        ArgumentNullException.ThrowIfNull(preview);
        var relocated = preview.Apply(decision);
        if (relocated is null) return false;
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
        return MemoryImageSparseOverlay.Combine(existing, incoming,
            (_, hasExisting, existingValue, hasIncoming, incomingValue) =>
                hasIncoming && (!hasExisting || decision == OverlapDecision.OverwriteIncoming) ? incomingValue : existingValue);
    }
}
