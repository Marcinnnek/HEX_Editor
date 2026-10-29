using System.ComponentModel;
using HexEditor.Core;
using HexEditor.Tools;

namespace HexEditor.UI;

/// <summary>Owns the displayed checksum result and tracks whether edited input made it stale.</summary>
public sealed class ChecksumViewModel : INotifyPropertyChanged
{
    private ChecksumCalculationResult? _result;

    public event PropertyChangedEventHandler? PropertyChanged;

    public ChecksumCalculationResult? Result
    {
        get => _result;
        private set
        {
            if (ReferenceEquals(_result, value)) return;
            _result = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Result)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Summary)));
        }
    }

    public string Summary => Result is null
        ? "No checksum calculated."
        : $"{Result.AlgorithmName}: {Result.FormattedValue}" + (Result.IsStale ? " (STALE - recalculate before writing)" : string.Empty);

    public ChecksumCalculationResult Calculate(MemoryImage image, ChecksumRequest request)
    {
        ArgumentNullException.ThrowIfNull(image);
        ArgumentNullException.ThrowIfNull(request);
        Result = ChecksumCalculator.Calculate(image, request);
        return Result;
    }

    public void Clear() => Result = null;

    public ChecksumCalculationResult GetWritableResult()
    {
        var result = Result ?? throw new InvalidOperationException("Calculate a checksum before writing a result.");
        if (result.IsStale)
            throw new InvalidOperationException("The displayed checksum is stale. Recalculate it before writing.");
        return result;
    }

    public void TrackImageChange(MemoryImage before, MemoryImage after)
    {
        if (Result is { IsStale: false } result &&
            RangeContentsDiffer(before, after, result.StartAddress, result.EndAddress))
        {
            Result = result with { IsStale = true };
        }
    }

    private static bool RangeContentsDiffer(MemoryImage before, MemoryImage after, uint startAddress, uint endAddress)
    {
        var left = before.Segments.Where(segment => segment.EndAddress >= startAddress && segment.StartAddress <= endAddress)
            .Select(segment => Clip(segment, startAddress, endAddress)).ToArray();
        var right = after.Segments.Where(segment => segment.EndAddress >= startAddress && segment.StartAddress <= endAddress)
            .Select(segment => Clip(segment, startAddress, endAddress)).ToArray();
        if (left.Length != right.Length) return true;
        for (var index = 0; index < left.Length; index++)
        {
            if (left[index].StartAddress != right[index].StartAddress ||
                !left[index].Data.Span.SequenceEqual(right[index].Data.Span)) return true;
        }
        return false;

        static MemorySegment Clip(MemorySegment segment, uint from, uint to)
        {
            var clippedStart = Math.Max(segment.StartAddress, from);
            var clippedEnd = Math.Min(segment.EndAddress, to);
            var offset = checked((int)(clippedStart - segment.StartAddress));
            var count = checked((int)((ulong)clippedEnd - clippedStart + 1));
            return new MemorySegment(clippedStart, segment.Data.Span.Slice(offset, count));
        }
    }
}
