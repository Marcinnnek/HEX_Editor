using HexEditor.Core;
using HexEditor.UI;

namespace HexEditor.Tests;

[TestClass]
public sealed class MemoryBankRemapTests
{
    [TestMethod]
    public void RemapsEveryBlockAcrossSpacedSourceBanksAndKeepsOutsideBytes()
    {
        var image = MemoryImage.FromSegments([
            new MemorySegment(0x0800, [0x01]),
            new MemorySegment(0x1000, [0x11, 0x12]),
            new MemorySegment(0x3001, [0x21]),
            new MemorySegment(0x5000, [0x31]),
            new MemorySegment(0x6000, [0x41])]);

        var preview = new MemoryBankRemapPreview(image,
            new BankRemapConfiguration(0x1000, 0x5000, 0x100, 0x2000, 0x8000));
        var remapped = preview.Resolve(MemoryImageMergeChoice.KeepSource)!;

        Assert.AreEqual(0L, preview.Collisions.TotalOverlapByteCount);
        Assert.IsTrue(remapped.TryGetByte(0x0800, out var before) && before == 0x01);
        Assert.IsTrue(remapped.TryGetByte(0x8000, out var first) && first == 0x11);
        Assert.IsTrue(remapped.TryGetByte(0x8001, out var second) && second == 0x12);
        Assert.IsTrue(remapped.TryGetByte(0x8101, out var middle) && middle == 0x21);
        Assert.IsTrue(remapped.TryGetByte(0x8200, out var last) && last == 0x31);
        Assert.IsTrue(remapped.TryGetByte(0x6000, out var after) && after == 0x41);
        Assert.IsFalse(remapped.TryGetByte(0x1000, out _));
    }

    [TestMethod]
    public void VacatedSourceAddressesDoNotCountAsCollisionsButUnchangedDestinationsDo()
    {
        var image = MemoryImage.FromSegments([
            new MemorySegment(0x1000, [0x11]),
            new MemorySegment(0x2000, [0x22, 0x44]),
            new MemorySegment(0x3000, [0x33])]);
        var preview = new MemoryBankRemapPreview(image,
            new BankRemapConfiguration(0x1000, 0x2000, 1, 0x1000, 0x2000));

        Assert.AreEqual(1L, preview.Collisions.TotalOverlapByteCount);
        var moved = preview.Resolve(MemoryImageMergeChoice.KeepSource)!;
        Assert.IsTrue(moved.TryGetByte(0x2000, out var first) && first == 0x11);
        Assert.IsTrue(moved.TryGetByte(0x2001, out var second) && second == 0x22);
        var kept = preview.Resolve(MemoryImageMergeChoice.KeepDestination)!;
        Assert.IsTrue(kept.TryGetByte(0x2001, out var existing) && existing == 0x44);
        Assert.IsTrue(moved.TryGetByte(0x3000, out var outside) && outside == 0x33);
    }

    [TestMethod]
    public void RejectsOccupiedInterbankAddressesAndDestinationOverflow()
    {
        var gapByte = MemoryImage.FromSegments([new MemorySegment(0x1100, [0x7A])]);
        Assert.Throws<ArgumentException>(() => new MemoryBankRemapPreview(gapByte,
            new BankRemapConfiguration(0x1000, 0x3000, 0x100, 0x2000, 0x8000)));
        var image = MemoryImage.FromSegments([new MemorySegment(0x1000, [0x10, 0x11])]);
        Assert.Throws<ArgumentOutOfRangeException>(() => new MemoryBankRemapPreview(image,
            new BankRemapConfiguration(0x1000, 0x1001, 0x100, 0x100, uint.MaxValue)));
    }

    [TestMethod]
    public void ApplyingAllBanksIsOneUndoableEdit()
    {
        var original = MemoryImage.FromSegments([
            new MemorySegment(0x1000, [0x11]), new MemorySegment(0x2000, [0x22])]);
        var editing = new ImageEditingSession();
        editing.Reset(original);
        var preview = editing.PreviewBankRemap(new BankRemapConfiguration(0x1000, 0x2000, 1, 0x1000, 0x8000));

        Assert.IsTrue(editing.ApplyBankRemap(preview, MemoryImageMergeChoice.KeepSource));
        Assert.IsTrue(editing.CurrentImage.TryGetByte(0x8001, out var mapped) && mapped == 0x22);
        Assert.IsTrue(editing.Undo());
        Assert.AreSame(original, editing.CurrentImage);
        Assert.IsFalse(editing.CanUndo);
    }

    [TestMethod]
    public void CollisionOverridesPreserveLargeRepeatedRangesWithoutMaterializingThem()
    {
        var destination = MemoryImage.FromRepeatedRange(0x1000, 0x20FFFF, 0xAA);
        var source = MemoryImage.FromRepeatedRange(0x1000, 0x20FFFF, 0xBB);
        var preview = new MemoryImageMergePreview(destination, source);
        var resolved = preview.Resolve(MemoryImageMergeChoice.KeepDestination,
            [new MemoryImageMergeRangeOverride(0x2000, 0x2FFF, MemoryImageMergeChoice.KeepSource)])!;

        Assert.AreEqual(destination.OccupiedByteCount, resolved.OccupiedByteCount);
        Assert.IsTrue(resolved.TryGetByte(0x1000, out var first) && first == 0xAA);
        Assert.IsTrue(resolved.TryGetByte(0x2000, out var middle) && middle == 0xBB);
        Assert.IsTrue(resolved.TryGetByte(0x3000, out var after) && after == 0xAA);
        Assert.IsTrue(resolved.TryGetByte(0x20FFFF, out var last) && last == 0xAA);
    }
}
