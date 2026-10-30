using HexEditor.Core;

namespace HexEditor.UI;

public enum FindPatternMode { Hex, Ascii }

public sealed record FindSearchResult(bool IsValidPattern, uint? MatchAddress, uint? MatchEndAddress, string? Message = null);

/// <summary>Parses and searches byte sequences in an Editor memory image.</summary>
public static class EditorFindSearch
{
    public static FindSearchResult FindCandidate(MemoryImage image, string draft, FindPatternMode mode,
        uint? cursorAddress, bool forward, bool inclusive, CancellationToken cancellationToken = default,
        IProgress<(ulong Completed, ulong Total)>? progress = null)
    {
        ArgumentNullException.ThrowIfNull(image);
        if (!TryParse(draft, mode, out var pattern, out var error)) return new FindSearchResult(false, null, null, error);
        ulong total = 0;
        foreach (var range in image.Ranges)
            if ((ulong)range.EndAddress - range.StartAddress + 1 >= (uint)pattern.Length)
                total += (ulong)range.EndAddress - range.StartAddress + 1 - (uint)pattern.Length + 1;

        ulong completed = 0;
        ulong? best = null;
        foreach (var range in image.Ranges)
        {
            if ((ulong)range.EndAddress - range.StartAddress + 1 < (uint)pattern.Length) continue;
            var last = (ulong)range.EndAddress + 1 - (uint)pattern.Length;
            for (var address = (ulong)range.StartAddress; address <= last; address++)
            {
                if ((completed & 0xFFF) == 0)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    progress?.Report((completed, total));
                }
                var meetsCursor = cursorAddress is null || (forward
                    ? (inclusive ? address >= cursorAddress.Value : address > cursorAddress.Value)
                    : (inclusive ? address <= cursorAddress.Value : address < cursorAddress.Value));
                if (meetsCursor && Matches(range, address, pattern) &&
                    (best is null || (forward ? address < best.Value : address > best.Value))) best = address;
                completed++;
            }
        }
        cancellationToken.ThrowIfCancellationRequested();
        progress?.Report((completed, total));
        return best is ulong found
            ? new FindSearchResult(true, (uint)found, (uint)(found + (uint)pattern.Length - 1))
            : new FindSearchResult(true, null, null, "No match.");
    }

    public static FindSearchResult FindNext(MemoryImage image, string draft, FindPatternMode mode,
        uint? selectionStart, uint? selectionEnd, CancellationToken cancellationToken = default,
        IProgress<(ulong Completed, ulong Total)>? progress = null) =>
        Find(image, draft, mode, selectionStart, selectionEnd, forward: true, cancellationToken, progress);

    public static FindSearchResult FindPrevious(MemoryImage image, string draft, FindPatternMode mode,
        uint? selectionStart, uint? selectionEnd, CancellationToken cancellationToken = default,
        IProgress<(ulong Completed, ulong Total)>? progress = null) =>
        Find(image, draft, mode, selectionStart, selectionEnd, forward: false, cancellationToken, progress);

    private static FindSearchResult Find(MemoryImage image, string draft, FindPatternMode mode,
        uint? selectionStart, uint? selectionEnd, bool forward, CancellationToken cancellationToken,
        IProgress<(ulong Completed, ulong Total)>? progress)
    {
        ArgumentNullException.ThrowIfNull(image);
        ArgumentNullException.ThrowIfNull(draft);
        if (!TryParse(draft, mode, out var pattern, out var error))
            return new FindSearchResult(false, null, null, error);
        if (image.Ranges.Count == 0)
            return new FindSearchResult(true, null, null, "No match.");

        var ranges = image.Ranges;
        var total = image.OccupiedByteCount * 2;
        ulong completed = 0;
        ulong? match = null;
        if (forward)
        {
            var first = selectionEnd is uint end ? (ulong)end + 1 : ranges[0].StartAddress;
            match = SearchForward(ranges, pattern, first, uint.MaxValue, cancellationToken, progress, ref completed, total);
            if (match is null && first > ranges[0].StartAddress)
                match = SearchForward(ranges, pattern, ranges[0].StartAddress, first - 1, cancellationToken, progress, ref completed, total);
        }
        else
        {
            var first = selectionStart is uint start
                ? start >= (uint)pattern.Length ? (ulong)start - (uint)pattern.Length : 0
                : (ulong)uint.MaxValue - (uint)pattern.Length + 1;
            var hasInitialRange = selectionStart is null || selectionStart.Value >= (uint)pattern.Length;
            if (hasInitialRange)
                match = SearchBackward(ranges, pattern, first, 0, cancellationToken, progress, ref completed, total);
            if (match is null && hasInitialRange)
                match = SearchBackward(ranges, pattern, uint.MaxValue - (uint)pattern.Length + 1UL, first + 1,
                    cancellationToken, progress, ref completed, total);
            else if (match is null && !hasInitialRange)
                match = SearchBackward(ranges, pattern, uint.MaxValue - (uint)pattern.Length + 1UL, 0,
                    cancellationToken, progress, ref completed, total);
        }

        progress?.Report((completed, total));
        if (match is not ulong address)
            return new FindSearchResult(true, null, null, "No match.");
        return new FindSearchResult(true, (uint)address, (uint)(address + (uint)pattern.Length - 1));
    }

    private static ulong? SearchForward(IReadOnlyList<MemoryImageRange> ranges, byte[] pattern, ulong from, ulong through,
        CancellationToken token, IProgress<(ulong Completed, ulong Total)>? progress, ref ulong completed, ulong total)
    {
        if (from > through) return null;
        foreach (var range in ranges)
        {
            var first = Math.Max((ulong)range.StartAddress, from);
            var lastByte = Math.Min((ulong)range.EndAddress, through);
            if (lastByte + 1 < (uint)pattern.Length) continue;
            var last = Math.Min(lastByte + 1 - (uint)pattern.Length, (ulong)range.EndAddress + 1 - (uint)pattern.Length);
            if (first > last) continue;
            for (var candidate = first; candidate <= last; candidate++)
            {
                if ((completed & 0xFFF) == 0) { token.ThrowIfCancellationRequested(); progress?.Report((completed, total)); }
                if (Matches(range, candidate, pattern)) return candidate;
                completed++;
            }
        }
        return null;
    }

    private static ulong? SearchBackward(IReadOnlyList<MemoryImageRange> ranges, byte[] pattern, ulong from, ulong through,
        CancellationToken token, IProgress<(ulong Completed, ulong Total)>? progress, ref ulong completed, ulong total)
    {
        if (from < through) return null;
        for (var rangeIndex = ranges.Count - 1; rangeIndex >= 0; rangeIndex--)
        {
            var range = ranges[rangeIndex];
            if ((ulong)range.StartAddress + (uint)pattern.Length > (ulong)range.EndAddress + 1) continue;
            var first = Math.Max((ulong)range.StartAddress, through);
            var last = Math.Min(from, (ulong)range.EndAddress + 1 - (uint)pattern.Length);
            if (first > last) continue;
            for (var candidate = last; ; candidate--)
            {
                if ((completed & 0xFFF) == 0) { token.ThrowIfCancellationRequested(); progress?.Report((completed, total)); }
                if (Matches(range, candidate, pattern)) return candidate;
                completed++;
                if (candidate == first) break;
            }
        }
        return null;
    }

    private static bool Matches(MemoryImageRange range, ulong start, byte[] pattern)
    {
        for (var offset = 0; offset < pattern.Length; offset++)
            if (!range.TryGetByte((uint)(start + (uint)offset), out var value) || value != pattern[offset]) return false;
        return true;
    }

    private static bool TryParse(string draft, FindPatternMode mode, out byte[] pattern, out string? error)
    {
        error = null;
        if (mode == FindPatternMode.Ascii)
        {
            if (draft.Length == 0) { pattern = []; error = "Enter an ASCII pattern."; return false; }
            if (draft.Any(character => character > 0x7F)) { pattern = []; error = "ASCII patterns can contain only 7-bit characters."; return false; }
            pattern = draft.Select(character => (byte)character).ToArray();
            return true;
        }

        var groups = draft.Trim(' ').Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (groups.Length == 0) { pattern = []; error = "Enter a HEX byte pattern."; return false; }
        if (groups.Any(group => (group.Length & 1) != 0))
        {
            pattern = [];
            error = "HEX patterns must contain complete byte pairs separated by spaces.";
            return false;
        }

        pattern = new byte[groups.Sum(group => group.Length / 2)];
        var outputIndex = 0;
        foreach (var group in groups)
        {
            for (var index = 0; index < group.Length / 2; index++)
            {
                if (!byte.TryParse(group.AsSpan(index * 2, 2), System.Globalization.NumberStyles.HexNumber,
                        System.Globalization.CultureInfo.InvariantCulture, out pattern[outputIndex++]))
                {
                    pattern = [];
                    error = "HEX patterns can contain only hexadecimal byte pairs and spaces.";
                    return false;
                }
            }
        }
        return true;
    }
}
