# Open, Performance, Blocks, and Navigation: Grill Notes

Status: agreed

## Current request

- Opening a file should replace the image in the Editor after offering to save unsaved changes.
- The main window title should show the open file name.
- The supported data size is to be limited to 100 MB; 5 MB images should open and scroll quickly, and loading must not freeze the UI.
- A large contiguous segment must remain one zero-based Block; scrolling and Find navigation must remain usable across the full 32-bit address domain.

## Verified code facts

- `MainWindow.OpenImage_Click` calls `PrepareOpenAsync`, then `MainViewModel.ApplyImport` and `EditorDocumentWorkspace.ApplyImport`. The preview uses the current Editor image, and applying it merges incoming records with that image. The merge is committed as a new document, marked modified when an image was already open.
- `PrepareOpenAsync` parses on a worker thread. `ApplyImport`, `RefreshImage`, block-list refresh, and title refresh run after the await on the UI thread. The current import preview stores incoming source records and builds per-address dictionaries during application.
- The import preview also builds an incoming-byte dictionary to detect overlaps even when no Editor image is open. Replacing a document directly can avoid this merge-specific work. Existing title acceptance tests assert the `Window.Title` property, not the pixels on the native title bar.
- No `HexEditor.UI` process was running when the current workspace was inspected, so the exact executable used for the reported missing caption is unknown. The new acceptance check must cover a visible native caption in the actual application, in addition to `Window.Title`.
- `MainViewModel.RefreshTitle` builds a file-name-based Editor title and a two-file Compare title. `MainWindow` assigns `Title` on property changes and after several actions; the user reports that the system title bar still lacks the name.
- The glossary defines a Block as a contiguous occupied memory segment, independent of source-record boundaries. `MainViewModel.RefreshImage` currently numbers displayed Blocks from 1.
- `MemoryImage.FromRanges` keeps adjacent storage ranges separate when materializing them together would exceed 1,048,576 bytes. `MainViewModel.RefreshImage` and the Compare block lists map each storage range directly to one displayed Block. A large contiguous occupied segment can therefore appear as several Blocks even though the glossary defines it as one.
- The Editor grid enables row virtualization; its containing view disables logical content scrolling. Find calls `ApplySelection`, which asks the Editor view to navigate to the match row; the user reports that the viewport does not visibly jump to the result.
- `0x10000000` is 268,435,456 bytes. Ten blocks of `0x1000000` bytes total 167,772,160 bytes. Both exceed a 100,000,000-byte limit, so the requested numbers cannot describe the same occupied-byte quantity under that interpretation.

## Decision tree: unsettled frontier

None. The user confirmed the full design after clarifying that the 32-bit range is the address domain, not a requirement to occupy all addresses.

## Settled decisions (round 1)

- The original round-1 decision limited each input file to 100,000,000 occupied bytes after decoding, independent of encoded file size and address span. The user then expanded the cap to every resulting image, as clarified in round 5.
- A displayed Block is one maximal contiguous occupied memory segment. Splitting a contiguous segment into several displayed Blocks is a bug, even when its backing storage has multiple ranges.
- Open always replaces the Editor document, including when Compare is active. Comparison views based on that document close after successful replacement; independent comparisons stay open.
- After file selection, a modified Editor document prompts Save / Don't save / Cancel. The previous document and its views remain until the new image has loaded successfully. Load failure or cancellation retains the previous document, including changes that the user chose not to save.
- A 5 MB occupied-data fixture from a local SSD should reach an interactive Editor within 2 seconds in Release. During larger loads, Cancel and window input should respond within 100 ms.
- Wheel scrolling and dragging update rows continuously in Editor and both Compare panels, including for 100 MB images. The vertical scrollbar thumb is at least about 30 px tall.
- Find Next, Previous, and F3 navigate to the complete match and highlight HEX and ASCII. The match's first row should become the first visible row; Compare navigates the matching side and synchronizes the other panel. At least the match's starting HEX byte must be horizontally visible.
- The prior approved title format remains: the active Editor file name and unsaved marker, or both active Compare names and base-image state. The new requirement is that this text be visibly rendered in the native main-window title bar; the existing property-only acceptance test is insufficient.

The Block and memory-segment definitions in `GLOSSARY.md` have been sharpened to say "maximal contiguous range." The later size-policy change is recorded in ADR 0003.

## Settled decisions (round 2)

- Displayed Block numbers are zero-based ordinals sorted by start address, independently in Editor and in each Compare file. They are recalculated after segment changes and are not stable identities. Export choices use the same numbering.
- This zero-based display requirement supersedes the earlier one-based numbering in the window-and-merge polish specification; the glossary's Block definition remains about occupied-address continuity rather than its label.
- The 100,000,000-byte input cap counts unique occupied addresses after decoding. Duplicate or overlapping source records do not count repeatedly; existing overlap decisions remain available.
- Opening the current source path reloads the disk version through the same Save / Don't save / Cancel flow. The old image remains until successful reload.
- A successful Open activates the Editor tab with the new image, even when invoked from an independent Compare tab that remains open.
- Find places the first match row at the top of the viewport where possible. Near the end, it scrolls as far as possible without adding blank rows; a match longer than the viewport is fully marked while its beginning is shown.
- When a Find result has no byte on the other Compare side, that panel scrolls to the corresponding gap/address position while preserving synchronized navigation.
- The 2-second 5 MB benchmark uses 5,000,000 occupied bytes in one contiguous segment, encoded as ordinary 16–32-byte records in both Intel HEX and S-Record, from a local SSD in Release. Fragmented images are covered for correctness and responsiveness without the 2-second total target.
- Wheel and scrollbar-drag updates target about 100 ms in Editor and Compare with ASCII shown and any supported row width (8, 16, or 32). Compare must handle two input images of up to 100 MB each.

## Settled decisions (round 3)

- The user withdraws the earlier 4 GiB Fill requirement. Fill must respect a 100 MB ceiling; later rounds resolved its relationship to existing occupied bytes and other editing operations. ADR 0003 supersedes the conflicting user-facing requirement in ADR 0002.
- No separate encoded-text file-size cap is wanted. A large text file with repeated records may be accepted if its unique decoded occupancy remains within the data cap; loading must remain cancellable and show progress.
- All Block actions operate on the whole logical Block, even if storage uses several ranges: click navigates, double-click relocates, Delete removes, and selected-Block export includes all occupied bytes. Out-of-range relocation reports an error.
- While replacement Open loads, the old image remains visible and can be browsed or scrolled, but editing, Save, and starting another Open/Compare/Merge are disabled. Cancel stops loading.
- Open from an independent Compare prompts only for changes to the Editor document being replaced. The independent Compare and its unsaved base remain open.

## Settled decisions (round 4)

- The user described the new limit as "100 MB of bytes in a session." Round 5 clarified that this means each image independently, not an application-wide sum.
- Absolute addresses remain unsigned 32-bit, including a valid occupied byte at `0xFFFFFFFF`. The size cap concerns occupancy, not address span.
- Fill's cap is based on the total unique occupied bytes in the resulting image. Replacing already occupied bytes may stay within the cap; adding enough new occupied addresses to exceed it is rejected.
- Merge results exceeding 100,000,000 unique occupied bytes are rejected atomically without changing the destination image.
- Keep the existing wide logical-range representation in Core. The product layer enforces the new size limit; previously implemented 4 GiB Core semantics need not be removed. ADR 0003 records this boundary.

## Settled decisions (round 5)

- "Session" means each current memory image/document separately, not the sum across the application. Editor, Compare A, and Compare B may each contain up to 100,000,000 unique occupied byte addresses.
- ADR 0003 records the new product boundary and supersedes ADR 0002's user-facing 4 GiB Fill requirement. ADR 0002's wide Core range model remains in use.

## Clarification after round 5

- The user meant that absolute addresses must have no artificial restriction within `0x00000000`–`0xFFFFFFFF`. A memory image may have small occupied segments at widely separated addresses. The address gap between them remains unoccupied and does not count toward the 100,000,000-byte limit.
- This clarification is reflected explicitly in ADR 0003 and was included in the final confirmation.

## Final confirmation

The user confirmed the complete Open, title, Block, size-limit, performance, scrolling, and Find behavior in the final round. This interview defines scope and domain decisions; implementation remains a separate step.
