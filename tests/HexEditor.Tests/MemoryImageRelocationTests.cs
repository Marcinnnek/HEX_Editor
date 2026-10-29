using HexEditor.Core;
using HexEditor.UI;

namespace HexEditor.Tests;

[TestClass]
public sealed class MemoryImageRelocationTests
{
    [TestMethod]
    public void Relocation_PreservesBytesAndRelativeOffsetsAndRemovesOriginalSegment()
    {
        var image = MemoryImage.FromSegments(
        [
            new MemorySegment(0x1000, [0x10, 0x20]),
            new MemorySegment(0x2000, [0x30])
        ]);

        var result = image.PrepareRelocation(0, 0xA0000000).Apply(RelocationOverlapDecision.Overwrite)!;

        Assert.HasCount(2, result.Segments);
        Assert.IsFalse(result.TryGetByte(0x1000, out _));
        Assert.IsTrue(result.TryGetByte(0xA0000000, out var first));
        Assert.AreEqual((byte)0x10, first);
        Assert.IsTrue(result.TryGetByte(0xA0000001, out var second));
        Assert.AreEqual((byte)0x20, second);
    }

    [TestMethod]
    public void Relocation_ReportsOverlapsEvenWhenValuesAreIdenticalAndCanBeCancelled()
    {
        var image = MemoryImage.FromSegments(
        [
            new MemorySegment(0x1000, [0x11, 0x22]),
            new MemorySegment(0x2001, [0x22])
        ]);

        var preview = image.PrepareRelocation(0, 0x2000);

        Assert.HasCount(1, preview.Overlaps);
        Assert.AreEqual(0x2001u, preview.Overlaps[0].Address);
        Assert.IsTrue(preview.Overlaps[0].ValuesAreIdentical);
        Assert.IsNull(preview.Apply(RelocationOverlapDecision.Cancel));
        Assert.IsTrue(image.TryGetByte(0x1000, out _));
        Assert.IsTrue(image.TryGetByte(0x2001, out var unchanged));
        Assert.AreEqual((byte)0x22, unchanged);
    }

    [TestMethod]
    public void Relocation_CanKeepExistingOrOverwriteConflictingDestinationBytes()
    {
        var image = MemoryImage.FromSegments(
        [
            new MemorySegment(0x1000, [0xAA, 0xBB]),
            new MemorySegment(0x2001, [0xCC])
        ]);
        var preview = image.PrepareRelocation(0, 0x2000);

        var kept = preview.Apply(RelocationOverlapDecision.KeepExisting)!;
        var overwritten = preview.Apply(RelocationOverlapDecision.Overwrite)!;

        Assert.IsTrue(kept.TryGetByte(0x2001, out var keptValue));
        Assert.AreEqual((byte)0xCC, keptValue);
        Assert.IsTrue(overwritten.TryGetByte(0x2001, out var replacedValue));
        Assert.AreEqual((byte)0xBB, replacedValue);
    }

    [TestMethod]
    public void Relocation_RejectsDestinationThatWouldOverflow32BitAddressSpace()
    {
        var image = MemoryImage.FromSegments([new MemorySegment(0x1000, [0x01, 0x02])]);

        Assert.Throws<ArgumentOutOfRangeException>(() => image.PrepareRelocation(0, 0xFFFFFFFF));
    }

    [TestMethod]
    public void Relocation_RejectsNonexistentSourceSegment()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => MemoryImage.Empty.PrepareRelocation(0, 0));
    }

    [TestMethod]
    public void Relocation_RefreshesVisibleImageAndSegmentsAndCanBeUndoneAndRedone()
    {
        var image = MemoryImage.FromSegments([new MemorySegment(0x1000, [0x41, 0x42])]);
        var viewModel = new MainViewModel(image);
        var preview = viewModel.PrepareRelocation(0, 0xA0000000);

        Assert.IsTrue(viewModel.ApplyRelocation(preview, RelocationOverlapDecision.Overwrite));
        Assert.HasCount(1, viewModel.Segments);
        Assert.AreEqual("A0000000", viewModel.Segments[0].StartAddress);
        Assert.IsTrue(viewModel.ImageView.TryNavigateToAddress(0xA0000001, out _));
        Assert.IsTrue(viewModel.CanUndo);
        Assert.IsTrue(viewModel.Undo());
        Assert.AreEqual("00001000", viewModel.Segments[0].StartAddress);
        Assert.IsTrue(viewModel.CanRedo);
        Assert.IsTrue(viewModel.Redo());
        Assert.AreEqual("A0000000", viewModel.Segments[0].StartAddress);
    }
}
