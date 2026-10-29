namespace HexEditor.Core;

/// <summary>Combines two sparse images at the union of their occupied addresses.</summary>
public static class MemoryImageSparseOverlay
{
    public static MemoryImage Combine(
        MemoryImage first,
        MemoryImage second,
        Func<uint, bool, byte, bool, byte, byte> chooseValue)
    {
        ArgumentNullException.ThrowIfNull(first);
        ArgumentNullException.ThrowIfNull(second);
        ArgumentNullException.ThrowIfNull(chooseValue);
        if (first.Segments.Count == 0) return second;
        if (second.Segments.Count == 0) return first;

        var output = new List<MemorySegment>();
        var firstIndex = 0;
        var secondIndex = 0;
        ulong cursor = Math.Min(first.Segments[0].StartAddress, second.Segments[0].StartAddress);
        var maximum = Math.Max(first.Segments[^1].EndAddress, second.Segments[^1].EndAddress);
        uint runStart = 0;
        uint previousAddress = 0;
        var run = new List<byte>();

        while (cursor <= maximum)
        {
            Advance(first.Segments, ref firstIndex, cursor);
            Advance(second.Segments, ref secondIndex, cursor);
            var hasFirst = TryValue(first.Segments, firstIndex, cursor, out var firstValue);
            var hasSecond = TryValue(second.Segments, secondIndex, cursor, out var secondValue);
            if (!hasFirst && !hasSecond)
            {
                Flush();
                cursor = Math.Min(NextStart(first.Segments, firstIndex), NextStart(second.Segments, secondIndex));
                if (cursor == ulong.MaxValue) break;
                continue;
            }

            var address = (uint)cursor;
            if (run.Count == 0) runStart = address;
            else if ((ulong)previousAddress + 1 != cursor)
            {
                Flush();
                runStart = address;
            }
            run.Add(chooseValue(address, hasFirst, firstValue, hasSecond, secondValue));
            previousAddress = address;
            if (cursor == maximum) break;
            cursor++;
        }

        Flush();
        return MemoryImage.FromSegments(output);

        void Flush()
        {
            if (run.Count == 0) return;
            output.Add(new MemorySegment(runStart, run.ToArray()));
            run.Clear();
        }
    }

    private static ulong NextStart(IReadOnlyList<MemorySegment> segments, int index) =>
        index < segments.Count ? segments[index].StartAddress : ulong.MaxValue;

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
