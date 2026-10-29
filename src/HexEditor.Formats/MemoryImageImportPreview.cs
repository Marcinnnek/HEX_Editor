using HexEditor.Core;

namespace HexEditor.Formats;

public sealed class MemoryImageImportPreview
{
    private readonly MemoryImage _existingImage;
    private readonly IReadOnlyList<MemorySegment> _records;

    private readonly record struct ByteCursor(MemorySegment Segment, int Order, int Offset)
    {
        public uint Address => Segment.StartAddress + (uint)Offset;
        public byte Value => Segment.Data.Span[Offset];
        public ByteCursor Advance() => new(Segment, Order, Offset + 1);
        public bool HasNext => Offset + 1 < Segment.ByteCount;
    }

    internal MemoryImageImportPreview(
        MemoryImage existingImage,
        IEnumerable<MemorySegment> records,
        string inputFormat,
        string? sourcePath,
        object? metadata)
    {
        _existingImage = existingImage;
        _records = Array.AsReadOnly(records.ToArray());
        InputFormat = inputFormat;
        SourcePath = sourcePath;
        FormatMetadata = metadata;
        OverlapSummary = BuildOverlapSummary(existingImage, _records);
    }

    public string InputFormat { get; }
    public string? SourcePath { get; }
    public object? FormatMetadata { get; }
    public OverlapSummary OverlapSummary { get; }

    public MemoryImageImportResult? Apply(OverlapDecision decision)
    {
        if (!Enum.IsDefined(decision))
        {
            throw new ArgumentOutOfRangeException(nameof(decision));
        }

        if (decision == OverlapDecision.Cancel)
        {
            return null;
        }

        var segments = new List<MemorySegment>();
        var run = new MemoryByteRun();
        var queue = CreateCursorQueue(_existingImage, _records);
        while (queue.TryPeek(out _, out var priority))
        {
            var address = priority.Address;
            var hasSelectedValue = false;
            byte selectedValue = default;
            while (queue.TryPeek(out _, out priority) && priority.Address == address)
            {
                var cursor = queue.Dequeue();
                if (cursor.Order == 0 || decision == OverlapDecision.OverwriteIncoming || !hasSelectedValue)
                {
                    selectedValue = cursor.Value;
                    hasSelectedValue = true;
                }

                AdvanceCursor(queue, cursor);
            }

            run.Add(address, selectedValue, segments);
        }
        run.Flush(segments);
        return new MemoryImageImportResult(
            MemoryImage.FromSegments(segments),
            InputFormat,
            SourcePath,
            FormatMetadata);
    }

    private static OverlapSummary BuildOverlapSummary(MemoryImage existing, IReadOnlyList<MemorySegment> records)
    {
        var ranges = new List<OverlapRange>();
        var run = new OverlapRun();
        var queue = CreateCursorQueue(existing, records);
        while (queue.TryPeek(out _, out var priority))
        {
            var address = priority.Address;
            var hasPreviousValue = false;
            byte previousValue = default;
            long identicalCount = 0;
            long differentCount = 0;
            while (queue.TryPeek(out _, out priority) && priority.Address == address)
            {
                var cursor = queue.Dequeue();
                if (cursor.Order == 0)
                {
                    hasPreviousValue = true;
                    previousValue = cursor.Value;
                }
                else
                {
                    if (hasPreviousValue)
                    {
                        if (previousValue == cursor.Value)
                        {
                            identicalCount++;
                        }
                        else
                        {
                            differentCount++;
                        }
                    }

                    hasPreviousValue = true;
                    previousValue = cursor.Value;
                }

                AdvanceCursor(queue, cursor);
            }

            run.Add(address, identicalCount, differentCount, ranges);
        }
        run.Flush(ranges);
        return ranges.Count == 0 ? OverlapSummary.Empty : new OverlapSummary(ranges);
    }

    private static PriorityQueue<ByteCursor, (uint Address, int Order)> CreateCursorQueue(
        MemoryImage existing,
        IReadOnlyList<MemorySegment> records)
    {
        var queue = new PriorityQueue<ByteCursor, (uint Address, int Order)>();
        foreach (var segment in existing.Segments)
        {
            EnqueueNext(queue, new ByteCursor(segment, 0, 0));
        }

        for (var index = 0; index < records.Count; index++)
        {
            EnqueueNext(queue, new ByteCursor(records[index], index + 1, 0));
        }

        return queue;
    }

    private static void EnqueueNext(PriorityQueue<ByteCursor, (uint Address, int Order)> queue, ByteCursor cursor)
    {
        queue.Enqueue(cursor, (cursor.Address, cursor.Order));
    }

    private static void AdvanceCursor(PriorityQueue<ByteCursor, (uint Address, int Order)> queue, ByteCursor cursor)
    {
        if (cursor.HasNext)
        {
            EnqueueNext(queue, cursor.Advance());
        }
    }

    private sealed class MemoryByteRun
    {
        private uint? _start;
        private uint _previous;
        private readonly List<byte> _data = [];

        public void Add(uint address, byte value, ICollection<MemorySegment> output)
        {
            if (_start is not null && (ulong)_previous + 1 != address)
            {
                Flush(output);
            }

            _start ??= address;
            _previous = address;
            _data.Add(value);
        }

        public void Flush(ICollection<MemorySegment> output)
        {
            if (_start is null)
            {
                return;
            }

            output.Add(new MemorySegment(_start.Value, _data.ToArray()));
            _start = null;
            _data.Clear();
        }
    }

    private sealed class OverlapRun
    {
        private uint? _start;
        private uint _previous;
        private long _identicalCount;
        private long _differentCount;

        public void Add(uint address, long identicalCount, long differentCount, ICollection<OverlapRange> output)
        {
            if (identicalCount == 0 && differentCount == 0)
            {
                return;
            }

            if (_start is not null && (ulong)_previous + 1 != address)
            {
                Flush(output);
            }

            if (_start is null)
            {
                _start = address;
            }

            _previous = address;
            _identicalCount += identicalCount;
            _differentCount += differentCount;
        }

        public void Flush(ICollection<OverlapRange> output)
        {
            if (_start is null)
            {
                return;
            }

            output.Add(new OverlapRange(_start.Value, _previous, _identicalCount, _differentCount));
            _start = null;
            _identicalCount = 0;
            _differentCount = 0;
        }
    }
}
