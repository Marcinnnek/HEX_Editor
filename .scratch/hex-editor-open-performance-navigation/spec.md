# Open, Performance, Blocks, and Find Navigation

Status: ready-for-agent

## Problem Statement

Open currently imports a selected file into the Editor image instead of replacing the open document. This can unexpectedly add data and leaves the user without a reliable save-or-discard decision for the image being replaced. The active filename is still missing from the visible native window caption, even though title text is computed internally.

Loading and displaying larger images can take too long or make the interface appear frozen. A typical image contains about 5 MB of occupied bytes and should be ready almost immediately. Scrolling with either the mouse wheel or the vertical scrollbar must remain responsive in Editor and Compare. The scrollbar thumb becomes difficult to grab for large images.

Storage chunks are exposed as separate Blocks even when their occupied addresses form one contiguous memory segment. Block labels start at one rather than zero. Find bytes can report a result without placing it visibly in the data grid. These problems make it difficult to inspect large or sparse images reliably.

## Solution

Make Open replace the Editor document through a cancellable, atomic workflow. Prompt for unsaved Editor changes, keep the old image usable for viewing while the replacement loads, and replace it only after a successful read. Show the active tab's filename and dirty state in the native main-window caption.

Enforce a ceiling of 100,000,000 **unique occupied byte addresses per memory image** after loading or any user edit. Keep the full unsigned 32-bit address domain: small segments may be arbitrarily far apart, and gaps do not consume the byte allowance. Optimize the load and presentation paths so representative 5 MB Intel HEX and S-Record images open within two seconds in Release on a local SSD. Keep loading cancellable and the UI responsive; update scrolling continuously in Editor and Compare and provide an easily grabbable scrollbar thumb.

Present each maximal contiguous occupied memory segment as one zero-based Block, regardless of its technical storage chunks. Make Find navigation move the matching byte sequence into the viewport, with its start as the first visible row where possible, and synchronize Compare panels to the result address.

## User Stories

1. As an Editor user, I want Open to replace the current document, so that choosing a file never silently adds its bytes to my current image.
2. As a user viewing Compare, I want Open to replace the Editor document, so that the active comparison does not change the destination of Open.
3. As a user with an unsaved Editor image, I want a Save, Don't save, or Cancel decision before replacement, so that I control what happens to my changes.
4. As a user choosing Save, I want the current Editor image saved before loading the replacement, so that the disk copy includes my changes.
5. As a user choosing Don't save, I want the replacement to proceed without saving, so that I can reload or switch files intentionally.
6. As a user choosing Cancel, I want Open to stop before changing the document, so that I can continue my current work.
7. As a user opening the same path, I want the disk version read again through the same decision flow, so that Open acts as a real reload.
8. As a user whose replacement read fails or is canceled, I want my previous image and edits to remain in memory, so that a failed Open cannot lose work even after I chose Don't save.
9. As a user waiting for a replacement file, I want the old image to remain visible and scrollable, so that the application still feels usable.
10. As a user waiting for a replacement file, I want to switch tabs and cancel loading, so that I retain control during a long read.
11. As a user waiting for a replacement file, I want editing, Save, and new Open, Compare, or Merge operations temporarily unavailable, so that the pending replacement has a stable source document.
12. As a user after a successful Open, I want the new image shown in Editor, so that the result of the command is immediately visible.
13. As a user with Compare views based on the replaced Editor document, I want those views closed only after successful replacement, so that they cannot show a stale base image.
14. As a user with independent Compare views, I want those views and their unsaved base images retained, so that replacing Editor does not disturb unrelated work.
15. As a user invoking Open from an independent Compare view, I want the save prompt to concern only the Editor document, so that unrelated Compare work remains untouched.
16. As a user with a large input file, I want visible progress and cancellation, so that I can stop an expensive load without freezing the window.
17. As a user with a 5 MB image on a local SSD, I want it ready to inspect within two seconds in Release, so that normal files open quickly.
18. As a user opening Intel HEX or S-Record, I want the same responsiveness target for both formats, so that performance does not depend on the source format.
19. As a user opening a fragmented image, I want correct bytes and responsive progress even if the total read takes longer than the contiguous-file benchmark, so that sparse files remain usable.
20. As a user with repeated source records, I want the size limit based on distinct occupied addresses, so that duplicate records do not count multiple times.
21. As a user with a large encoded text file but at most 100,000,000 unique occupied addresses, I want it eligible for loading with progress and cancellation, so that textual file size alone is not a rejection reason.
22. As a user with an image exceeding 100,000,000 occupied bytes, I want a clear rejection with my prior image preserved, so that I understand the boundary and lose no data.
23. As a user with a byte at `0xFFFFFFFF`, I want that byte supported, so that the full 32-bit address domain remains available.
24. As a user with small segments at distant addresses, I want empty address gaps excluded from the size count, so that sparse firmware layouts remain valid.
25. As a Compare user, I want each image to have its own 100,000,000-byte allowance, so that comparing two valid large files is possible.
26. As a user editing through Fill, Insert, Paste, relocation, Merge, or another operation, I want the resulting image checked against the same allowance, so that every saved image can be opened again.
27. As a user overwriting already occupied bytes with Fill, I want that operation allowed when the final occupied count stays within the limit, so that the interval length alone does not reject a valid edit.
28. As a user adding bytes beyond the allowance, I want the operation rejected without a partial image or undo entry, so that the document remains consistent.
29. As a user merging images whose occupied-address union exceeds the limit, I want the merge rejected before it changes the destination, so that no partial result is committed.
30. As an Editor user, I want the native window caption to show the active file's basename, so that I can identify it from the Windows title bar.
31. As an Editor user with unsaved changes, I want the active filename marked with an asterisk, so that the caption reflects the save state.
32. As a Compare user, I want the caption to show both filenames and the editable base image's dirty state, so that I know which comparison is active.
33. As a user switching tabs, editing, saving, or using Save As, I want the visible caption refreshed immediately, so that it never describes an old tab or filename.
34. As a user opening an empty Editor, I want the existing untitled caption behavior, so that an empty document is recognizable.
35. As a user inspecting Blocks, I want one entry per maximal contiguous occupied memory segment, so that backing chunks and source-record boundaries do not appear as false breaks.
36. As a user inspecting Blocks, I want them ordered by increasing start address and numbered `0, 1, 2...`, so that each list is predictable.
37. As a user comparing images, I want Editor and each Compare side numbered independently from zero, so that a Block number refers to the local list.
38. As a user changing an image, I want Block numbers recomputed after a deletion, addition, connection, or relocation, so that the numbers remain correct ordinals.
39. As a user clicking a Block, I want to jump to its first address, so that I can inspect the whole segment from its start.
40. As a user double-clicking an Editor Block, I want to relocate the entire contiguous segment, so that storage chunking cannot move only part of it.
41. As a user deleting or exporting a selected Block, I want the operation to include all of its occupied bytes, so that the displayed Block is the unit of action.
42. As a user attempting to relocate a Block beyond `0xFFFFFFFF`, I want a clear error and no change, so that every address stays valid.
43. As a user scrolling a large Editor image with the mouse wheel, I want new rows drawn continuously within about 100 ms, so that navigation feels immediate.
44. As a user dragging a vertical scrollbar, I want the visible rows updated while I drag, so that I can navigate by position without releasing the thumb.
45. As a Compare user, I want the same live wheel and drag behavior in both data panels, so that large comparisons remain navigable.
46. As a user viewing ASCII with 8, 16, or 32 bytes per row, I want scrolling to remain responsive, so that layout settings do not make navigation stall.
47. As a user with a very large image, I want the vertical scrollbar thumb to be at least about 30 pixels tall, so that I can grab it with a mouse.
48. As a user finding bytes, I want Find Next, Find Previous, and F3 to scroll to the result, so that the reported address is visible in the grid.
49. As a user finding a multi-byte sequence, I want every byte in the current result highlighted in HEX and ASCII, including across row boundaries, so that I can see the complete match.
50. As a user finding bytes, I want the result's first row at the top where possible, so that I can locate the match without searching the middle of the viewport.
51. As a user finding near the end of an image, I want the result start as high as the available rows allow, so that the match is visible without artificial blank rows.
52. As a user finding a sequence longer than the viewport, I want its start shown and its whole range marked as the current match, so that navigation is still unambiguous.
53. As a user finding beyond the horizontal viewport, I want at least the first HEX cell visible, so that I can identify the result immediately.
54. As a Compare user finding bytes in A or B, I want the matching side navigated and the other side synchronized to the corresponding address or gap, so that both panels remain aligned without invented bytes.

## Implementation Decisions

- Preserve the existing single-context memory-image vocabulary: an image contains occupied bytes at unsigned 32-bit addresses, and a Block is a **maximal contiguous** occupied memory segment. Technical storage ranges may subdivide a Block but must not define its displayed boundaries or action scope.
- Treat Open as a document-replacement operation, separate from Merge and Import. It always targets Editor, regardless of the selected tab. Reuse the existing save-decision behavior for dirty documents; Cancel or failed Save ends the operation before loading.
- Prepare the replacement off the UI thread and commit it only after parsing and validation succeed. A cancellation or failure leaves the previous document and dependent views intact; its dirty state reflects any Save that already completed. A prior Don't save choice authorizes replacement after success; it does not erase the old in-memory document before success.
- During loading, expose a read-only viewing state for the old document. Keep scrolling and tab switching live, offer progress and Cancel, and block commands that could modify the target or start conflicting file work. On success, activate Editor and close only comparison views that depended on the replaced document.
- Enforce the product limit of **100,000,000 unique occupied byte addresses per image** at the boundary of every user-visible load or edit. Apply it independently to Editor and each Compare image. Compute the prospective result before commit so failure is atomic. For Fill and Merge, count the resulting occupied-address union, rather than the requested interval or sum of source lengths. Preserve existing overlap choices and Undo behavior for accepted operations.
- Keep the full address domain `0x00000000` through `0xFFFFFFFF`; an unoccupied gap does not count toward the limit. Do not add an encoded text-file size limit. Allow duplicate or overlapping source records to be resolved under the existing policy while counting each final occupied address once.
- Retain the Core range representation and its wide logical-address capability. The 100 MB restriction is a user-facing image policy, not a narrower address type or a request to remove Core's existing wide-range semantics.
- Optimize the load path for replacement rather than doing per-byte merge-preview work when Open has no merge semantics. Keep expensive decoding, size accounting, and other large-image preparation off the UI dispatcher; limit visible-grid work to the current viewport and avoid rebuilding all rows for each scroll position.
- Make Block presentation use logical adjacency across backing ranges. Number displayed entries from zero independently in Editor and each Compare side, sorted by start address, and recalculate labels after image changes. Treat labels as ordinals, not persistent identifiers. Preserve the existing one-line Block description and horizontal scrolling; selected-Block export uses the same ordinals.
- Derive the main-window caption from the active workspace tab and its live document state. Editor uses `<basename><dirty marker> - HEX Editor`; Compare uses `Compare: <file A><dirty marker> ↔ <file B> - HEX Editor`. The requirement is for visible native caption text, not only an internal title property. Preserve the existing untitled state.
- Keep the vertical scrollbar thumb at least approximately 30 device-independent pixels at ordinary scaling. Wheel and thumb dragging must update visible rows during movement in Editor and both Compare panels, including with ASCII visible and all supported row widths.
- On a successful Find result, navigate the relevant data panel to the match start, place its first row at the top when possible, and make the starting HEX cell horizontally visible. Near the end, scroll as far as the existing content permits. Keep the complete sequence highlighted in both HEX and ASCII; Compare synchronizes the other side to the corresponding address position even when it is a gap.
- Preserve the previously specified Find pattern rules, wrapping, current-match-only highlight, keyboard shortcuts, modeless window, progress, and cancellation. This spec changes the viewport outcome of a successful search, not the search language.

## Testing Decisions

- Test externally observable behavior through the highest existing seams. Use public workspace, image, and file-service operations for document replacement, byte-count policy, Block actions, and data correctness. Use rendered WPF scenarios for native title visibility, scrolling, scrollbar thumb size, Find viewport placement, command availability, progress, and cancellation. Avoid tests tied to a private event handler, a storage chunk size, or a particular visual-control class.
- Extend existing workspace and file-service tests to cover Open with clean and dirty Editor documents, each Save/Don't save/Cancel decision, failed or canceled replacement, same-path reload, invocation from Compare, dependent versus independent Compare views, and preservation of old changes until successful commit.
- Verify the 100,000,000-byte boundary at exactly the limit and one byte beyond it. Cover duplicate records, far-apart low/high addresses, `0xFFFFFFFF`, sparse gaps, both Compare images independently, Fill overwrite versus added occupancy, Paste/Insert/edit paths, and Merge union accounting. Assert that rejected operations leave image bytes, dirty state, and Undo history unchanged. Test the public product boundary while retaining separate Core tests for wide logical ranges.
- Extend Block-list and memory-image tests with one contiguous segment backed by multiple technical ranges, separated segments, and segments changed by relocation, deletion, Fill, or Merge. Assert one logical Block per maximal contiguous range, zero-based labels, stable address order, whole-Block navigation, relocation, deletion, and export; do not assert the number of backing chunks.
- Extend title acceptance scenarios to inspect the **rendered native main-window caption** while switching Editor and Compare tabs, editing, saving, using Save As, reloading, and returning to an empty Editor. A `Window.Title` property assertion alone is insufficient.
- Measure a Release build on a local SSD using two representative files with 5,000,000 occupied bytes in one contiguous segment, one Intel HEX and one S-Record, using normal 16–32-byte source records. The elapsed time from Open to an interactive Editor must be at most two seconds for each format. Keep a fragmented fixture for correctness and responsiveness without a fixed two-second total.
- Use observable UI response and cancellation probes during larger loads to check that input and Cancel are handled within about 100 ms and that the previous image remains viewable. Verify the loading state disables editing, Save, and new Open/Compare/Merge commands without blocking tab changes or scrolling.
- Exercise wheel and live thumb dragging with representative small and large images, including up to 100,000,000 occupied bytes per Compare side, ASCII enabled, and row widths 8, 16, and 32. Check that the viewport changes during drag and refreshes in roughly 100 ms. Measure a minimum thumb height of approximately 30 pixels at ordinary scaling.
- Extend Find workflow and WPF scenarios for Next, Previous, and F3 in Editor and both Compare sides. Assert that the first matching row is the first visible row where possible, the starting HEX byte is visible horizontally, the full result remains marked across rows, end-of-image placement uses no synthetic rows, and the opposite Compare panel tracks a real address or gap.
- Reuse the existing workspace, file-service, active-title, Block-list, Find, and WPF acceptance suites as prior art. Run automated tests and the targeted Release benchmark after implementation; treat performance thresholds as measured acceptance criteria rather than assumptions about internal algorithms.

## Out of Scope

- Requiring the user interface to create, Fill, save, or reopen an image with more than 100,000,000 occupied bytes. The prior user-facing full-4-GiB Fill requirement is superseded by this product cap.
- Restricting valid addresses to a smaller interval, limiting the distance between segments, or imposing a separate encoded-text file-size limit.
- Changing Intel HEX or S-Record syntax, checksum rules, source-record boundaries, or existing overlap policy.
- Replacing the WPF application, changing the visual design of the Block list, or redesigning the Find dialog beyond navigation to a found result.
- Treating a displayed Block number as a persistent segment identity.

## Further Notes

- The user confirmed the full scope in the grill and explicitly clarified that `0x00000000–0xFFFFFFFF` describes the **allowed address space**, not a demand to occupy all 4 GiB. ADR 0003 records the 100,000,000-byte per-image product policy and supersedes ADR 0002 only for user-facing full-range Fill; the Core range model remains.
- This spec supersedes the earlier window-and-merge specification where it numbered Blocks from one. Block labels now start at zero in each list. Its active-tab title format remains applicable, with the added requirement to verify the actual Windows caption.
- The approved testing split is public workspace/model/file-service behavior for data workflows, rendered WPF scenarios for visible interactions, and a Release benchmark for representative 5 MB files.
- Existing Open behavior prepares an import preview and applies it to the current image; existing title tests check only an internal property; displayed Block lists currently follow backing ranges. These are investigation leads, not requirements to preserve their implementations.
