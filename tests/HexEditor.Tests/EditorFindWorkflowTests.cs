using HexEditor.Core;
using HexEditor.UI;

namespace HexEditor.Tests;

[TestClass]
public sealed class EditorFindWorkflowTests
{
    [TestMethod]
    public void Find_candidate_respects_inclusive_and_exclusive_compare_cursor_boundaries()
    {
        var image = MemoryImage.FromSegments([new MemorySegment(0x1000, [0xAA, 0xAA, 0xAA])]);

        var inclusive = EditorFindSearch.FindCandidate(image, "AA", FindPatternMode.Hex, 0x1001, forward: true, inclusive: true);
        var exclusive = EditorFindSearch.FindCandidate(image, "AA", FindPatternMode.Hex, 0x1001, forward: true, inclusive: false);
        var previousInclusive = EditorFindSearch.FindCandidate(image, "AA", FindPatternMode.Hex, 0x1001, forward: false, inclusive: true);
        var previousExclusive = EditorFindSearch.FindCandidate(image, "AA", FindPatternMode.Hex, 0x1001, forward: false, inclusive: false);

        Assert.AreEqual((uint)0x1001, inclusive.MatchAddress);
        Assert.AreEqual((uint)0x1002, exclusive.MatchAddress);
        Assert.AreEqual((uint)0x1001, previousInclusive.MatchAddress);
        Assert.AreEqual((uint)0x1000, previousExclusive.MatchAddress);
    }

    [TestMethod]
    public async Task Editor_find_searches_full_range_boundaries_without_materializing_bytes()
    {
        var image = MemoryImage.FromRepeatedRange(0, uint.MaxValue, 0xAA);
        var workspace = new EditorDocumentWorkspace();
        workspace.OpenImage(image, null, null);

        var first = await workspace.FindNextAsync("AA AA", FindPatternMode.Hex, null, null, CancellationToken.None, null);
        var interior = await workspace.FindNextAsync("AA AA", FindPatternMode.Hex, 0x7FFFFFFF, 0x7FFFFFFF, CancellationToken.None, null);
        var final = await workspace.FindPreviousAsync("AA AA", FindPatternMode.Hex, null, null, CancellationToken.None, null);
        var wrappedAtZero = await workspace.FindPreviousAsync("AA AA", FindPatternMode.Hex, 0, 0, CancellationToken.None, null);
        var wrappedAtEnd = await workspace.FindNextAsync("AA AA", FindPatternMode.Hex, uint.MaxValue, uint.MaxValue, CancellationToken.None, null);

        Assert.AreEqual((uint)0, first.MatchAddress);
        Assert.AreEqual((uint)0x80000000, interior.MatchAddress);
        Assert.AreEqual(uint.MaxValue - 1, final.MatchAddress);
        Assert.AreEqual(uint.MaxValue - 1, wrappedAtZero.MatchAddress);
        Assert.AreEqual((uint)0, wrappedAtEnd.MatchAddress);
    }

    [TestMethod]
    public void Compare_candidate_skips_entire_repeated_range_when_pattern_cannot_match()
    {
        var image = MemoryImage.FromRepeatedRange(0, uint.MaxValue, 0xAA);
        var progress = new RecordingProgress();

        var result = EditorFindSearch.FindCandidate(image, "AA AB", FindPatternMode.Hex, null, forward: true,
            inclusive: true, progress: progress);

        Assert.IsNull(result.MatchAddress);
        Assert.IsLessThan((ulong)16, progress.LastCompleted,
            $"Expected a periodicity-bounded search, got {progress.LastCompleted} candidate addresses.");
    }

    [TestMethod]
    public void Compare_find_candidates_cover_both_panels_at_the_full_range_end()
    {
        var baseImage = MemoryImage.FromRepeatedRange(0, uint.MaxValue, 0xAA);
        var referenceImage = MemoryImage.FromRepeatedRange(0, uint.MaxValue, 0xAA);

        var baseMatch = EditorFindSearch.FindCandidate(baseImage, "AA AA", FindPatternMode.Hex,
            uint.MaxValue, forward: false, inclusive: true);
        var referenceMatch = EditorFindSearch.FindCandidate(referenceImage, "AA AA", FindPatternMode.Hex,
            uint.MaxValue, forward: false, inclusive: true);

        Assert.AreEqual(uint.MaxValue - 1, baseMatch.MatchAddress);
        Assert.AreEqual(uint.MaxValue - 1, referenceMatch.MatchAddress);
    }
    [TestMethod]
    public async Task Editor_find_matches_across_adjacent_ranges_but_stops_at_address_gaps()
    {
        var workspace = new EditorDocumentWorkspace();
        workspace.OpenImage(MemoryImage.FromSegments([
            new MemorySegment(0x1000, [0xAA, 0xDE]),
            new MemorySegment(0x1002, [0xAD, 0xBE, 0xEF]),
            new MemorySegment(0x1010, [0xDE, 0xAD, 0xBE, 0xEF])]), null, null);

        var first = await workspace.FindNextAsync("DE AD BE EF", FindPatternMode.Hex,
            null, null, CancellationToken.None, null);
        var second = await workspace.FindNextAsync("DE AD BE EF", FindPatternMode.Hex,
            first.MatchAddress, first.MatchEndAddress, CancellationToken.None, null);
        var wrapped = await workspace.FindNextAsync("DE AD BE EF", FindPatternMode.Hex,
            second.MatchAddress, second.MatchEndAddress, CancellationToken.None, null);

        Assert.AreEqual((uint)0x1001, first.MatchAddress);
        Assert.AreEqual((uint)0x1004, first.MatchEndAddress);
        Assert.AreEqual((uint)0x1010, second.MatchAddress);
        Assert.AreEqual(first.MatchAddress, wrapped.MatchAddress);

        var gapWorkspace = new EditorDocumentWorkspace();
        gapWorkspace.OpenImage(MemoryImage.FromSegments([
            new MemorySegment(0x1100, [0xDE, 0xAD]),
            new MemorySegment(0x1104, [0xBE, 0xEF])]), null, null);
        var acrossGap = await gapWorkspace.FindNextAsync("DEADBEEF", FindPatternMode.Hex,
            null, null, CancellationToken.None, null);
        Assert.IsNull(acrossGap.MatchAddress);
    }

    [TestMethod]
    public async Task Editor_find_keeps_ascii_drafts_case_sensitive_and_rejects_non_ascii()
    {
        var workspace = new EditorDocumentWorkspace();
        workspace.OpenImage(MemoryImage.FromSegments([new MemorySegment(0x2000, "Aba"u8)]), null, null);

        var uppercase = await workspace.FindNextAsync("A", FindPatternMode.Ascii,
            null, null, CancellationToken.None, null);
        var lowercase = await workspace.FindNextAsync("a", FindPatternMode.Ascii,
            0x2000, 0x2000, CancellationToken.None, null);
        var invalid = await workspace.FindNextAsync("Ā", FindPatternMode.Ascii,
            null, null, CancellationToken.None, null);

        Assert.AreEqual((uint)0x2000, uppercase.MatchAddress);
        Assert.AreEqual((uint)0x2002, lowercase.MatchAddress);
        Assert.IsFalse(invalid.IsValidPattern);
        Assert.IsNull(invalid.MatchAddress);
    }

    [TestMethod]
    public async Task Editor_find_previous_wraps_and_rejects_incomplete_hex_pairs()
    {
        var workspace = new EditorDocumentWorkspace();
        workspace.OpenImage(MemoryImage.FromSegments([new MemorySegment(0x3000, [0xDE, 0xAD, 0x00, 0xDE, 0xAD])]), null, null);

        var last = await workspace.FindPreviousAsync("DEAD", FindPatternMode.Hex,
            null, null, CancellationToken.None, null);
        var wrapped = await workspace.FindPreviousAsync("DEAD", FindPatternMode.Hex,
            last.MatchAddress, last.MatchEndAddress, CancellationToken.None, null);
        var invalid = await workspace.FindNextAsync("A", FindPatternMode.Hex,
            0x3001, 0x3001, CancellationToken.None, null);
        var splitByte = await workspace.FindNextAsync("D E", FindPatternMode.Hex,
            0x3001, 0x3001, CancellationToken.None, null);

        Assert.AreEqual((uint)0x3003, last.MatchAddress);
        Assert.AreEqual((uint)0x3000, wrapped.MatchAddress);
        Assert.IsFalse(invalid.IsValidPattern);
        Assert.IsNull(invalid.MatchAddress);
        Assert.IsFalse(splitByte.IsValidPattern);
    }

    [TestMethod]
    public void Editor_find_indicator_covers_each_byte_across_displayed_rows_and_can_be_cleared()
    {
        var view = new HexImageViewModel(MemoryImage.FromSegments([
            new MemorySegment(0x4000, Enumerable.Range(0, 24).Select(value => (byte)value).ToArray())]), bytesPerRow: 8);

        view.SetFindMatch(0x4007, 0x4009);

        Assert.IsTrue(view.Rows[0].Cells[7].IsFindMatch);
        Assert.IsTrue(view.Rows[1].Cells[0].IsFindMatch);
        Assert.IsTrue(view.Rows[1].Cells[1].IsFindMatch);
        view.ClearFindMatch();
        Assert.IsFalse(view.Rows[0].Cells[7].IsFindMatch);
        Assert.IsFalse(view.Rows[1].Cells[0].IsFindMatch);
    }

    [TestMethod]
    public void Compare_find_marker_layers_over_difference_and_edit_state_and_clears_independently()
    {
        var view = new CompareViewModel(
            MemoryImage.FromSegments([new MemorySegment(0x5000, [0xAA])]),
            MemoryImage.FromSegments([new MemorySegment(0x5000, [0xBB])]),
            isBaseByteEdited: address => address == 0x5000);

        view.SetFindMatch(fileA: true, start: 0x5000, end: 0x5000);

        var baseCell = view.FileARows.SelectMany(row => row.Cells).Single(cell => cell.Address == 0x5000);
        var referenceCell = view.FileBRows.SelectMany(row => row.Cells).Single(cell => cell.Address == 0x5000);
        Assert.AreEqual(ByteHighlightState.Different, baseCell.Highlight);
        Assert.IsTrue(baseCell.IsEdited);
        Assert.IsTrue(baseCell.IsFindMatch);
        Assert.AreEqual(ByteHighlightState.Different, referenceCell.Highlight);
        Assert.IsFalse(referenceCell.IsEdited);
        Assert.IsFalse(referenceCell.IsFindMatch);

        view.ClearFindMatch();
        baseCell = view.FileARows.SelectMany(row => row.Cells).Single(cell => cell.Address == 0x5000);
        Assert.AreEqual(ByteHighlightState.Different, baseCell.Highlight);
        Assert.IsTrue(baseCell.IsEdited);
        Assert.IsFalse(baseCell.IsFindMatch);
    }

    [TestMethod]
    public async Task Editor_find_can_cancel_a_sizable_scan()
    {
        var workspace = new EditorDocumentWorkspace();
        workspace.OpenImage(MemoryImage.FromSegments([new MemorySegment(0x6000, new byte[100_000])]), null, null);
        using var cancellation = new CancellationTokenSource();
        var progress = new CancellingProgress(cancellation);

        await Assert.ThrowsAsync<OperationCanceledException>(() => workspace.FindNextAsync("FF", FindPatternMode.Hex,
            null, null, cancellation.Token, progress));
    }

    private sealed class CancellingProgress(CancellationTokenSource cancellation) : IProgress<(ulong Completed, ulong Total)>
    {
        public void Report((ulong Completed, ulong Total) value)
        {
            if (value.Completed > 0) cancellation.Cancel();
        }
    }

    private sealed class RecordingProgress : IProgress<(ulong Completed, ulong Total)>
    {
        public ulong LastCompleted { get; private set; }
        public void Report((ulong Completed, ulong Total) value) => LastCompleted = value.Completed;
    }
}
