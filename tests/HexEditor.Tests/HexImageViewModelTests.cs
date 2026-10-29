using System.Text;
using HexEditor.Core;
using HexEditor.Formats;
using HexEditor.UI;

namespace HexEditor.Tests;

[TestClass]
public sealed class HexImageViewModelTests
{
    [TestMethod]
    public void Row_UsesAbsoluteAlignedAddressAndDistinguishesHolesFromStoredBytes()
    {
        var image = MemoryImage.FromSegments(
        [
            new MemorySegment(0x1001, [0x00]),
            new MemorySegment(0x1003, [0x20, 0x7E, 0x7F, 0xFF])
        ]);
        var view = new HexImageViewModel(image, bytesPerRow: 8);

        var row = view.Rows[0];

        Assert.AreEqual("00001000", row.Address);
        Assert.AreEqual("   00    20 7E 7F FF   ", row.HexData);
        Assert.AreEqual(" .  ~.. ", row.Ascii);
        Assert.IsFalse(row.IsGapMarker);
    }

    [TestMethod]
    public void Rows_KeepOneEmptyRowButCollapseLongAddressGap()
    {
        var image = MemoryImage.FromSegments(
        [
            new MemorySegment(0x100, [0x41]),
            new MemorySegment(0x110, [0x42]),
            new MemorySegment(0x200, [0x43])
        ]);
        var view = new HexImageViewModel(image, bytesPerRow: 8);

        Assert.HasCount(5, view.Rows);
        Assert.AreEqual("00000100", view.Rows[0].Address);
        Assert.AreEqual("00000108", view.Rows[1].Address);
        Assert.AreEqual("00000110", view.Rows[2].Address);
        Assert.IsTrue(view.Rows[3].IsGapMarker);
        StringAssert.Contains(view.Rows[3].GapDescription!, "00000118");
        StringAssert.Contains(view.Rows[3].GapDescription!, "000001F8");
        Assert.AreEqual("00000200", view.Rows[4].Address);
    }

    [TestMethod]
    public void RowWidthAndAsciiCanBeChangedWithoutExpandingAddressGaps()
    {
        var image = MemoryImage.FromSegments(
        [
            new MemorySegment(0xA0000000, [0x41, 0x42]),
            new MemorySegment(0xFFFFFFFE, [0x00, 0xFF])
        ]);
        var view = new HexImageViewModel(image);

        view.BytesPerRow = 32;
        view.ShowAscii = false;

        Assert.HasCount(3, view.Rows);
        Assert.AreEqual("A0000000", view.Rows[0].Address);
        Assert.IsNull(view.Rows[0].Ascii);
        Assert.IsTrue(view.Rows[1].IsGapMarker);
        Assert.AreEqual("FFFFFFE0", view.Rows[2].Address);
        Assert.IsTrue(view.Rows[2].HexData.EndsWith("00 FF", StringComparison.Ordinal));
    }

    [TestMethod]
    public void Navigation_FindsAbsoluteAddressAndSegmentStartWithoutWalkingRows()
    {
        var image = MemoryImage.FromSegments(
        [
            new MemorySegment(0x1000, [0x11]),
            new MemorySegment(0xA0000000, [0x22])
        ]);
        var view = new HexImageViewModel(image, bytesPerRow: 16);

        Assert.IsTrue(view.TryNavigateToAddress(0xA0000000, out var addressRow));
        Assert.AreEqual("A0000000", view.Rows[addressRow].Address);
        Assert.IsTrue(view.TryNavigateToSegment(1, out var segmentRow));
        Assert.AreEqual(addressRow, segmentRow);
        Assert.IsTrue(view.TryNavigateToAddress(0x5000, out var gapRow));
        Assert.IsTrue(view.Rows[gapRow].IsGapMarker);
    }

    [TestMethod]
    public void Rows_AreIndexedLazilyForLargeOccupiedSegments()
    {
        var image = MemoryImage.FromSegments([new MemorySegment(0, new byte[10 * 1024 * 1024])]);
        var view = new HexImageViewModel(image, bytesPerRow: 32);

        Assert.HasCount(327680, view.Rows);
        Assert.AreEqual("00000000", view.Rows[0].Address);
        Assert.AreEqual("009FFFE0", view.Rows[^1].Address);
    }

    [TestMethod]
    public void MainViewModel_ExportsOnlySelectedSegmentAndCanReimportIt()
    {
        const string sourceRecords = ":020010001011CD\n:0100200020BF\n:00000001FF\n";
        var fileService = new MemoryImageFileService();
        var viewModel = new MainViewModel();
        using var source = new MemoryStream(Encoding.ASCII.GetBytes(sourceRecords));
        viewModel.ApplyImport(
            fileService.PrepareImport(source),
            OverlapDecision.OverwriteIncoming);
        using var exported = new MemoryStream();

        viewModel.ExportSegments([1], MemoryImageExportFormat.SRecord, 8, exported);
        exported.Position = 0;
        var imported = fileService.PrepareImport(exported).Apply(OverlapDecision.OverwriteIncoming)!.Image;

        Assert.HasCount(1, imported.Segments);
        Assert.AreEqual(0x20u, imported.Segments[0].StartAddress);
        CollectionAssert.AreEqual(new byte[] { 0x20 }, imported.Segments[0].Data.ToArray());
    }
}
