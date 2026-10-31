# Copying Byte Selections and Global Row Alignment

Status: ready-for-agent

## Problem Statement

The Editor displays sparse memory images in rows whose width can be customized. Row labels and byte positions must follow one address grid starting at zero. A memory segment that starts in the middle of a display row should appear at the corresponding cell, with preceding unoccupied cells left blank. Restarting the grid at each segment shifts bytes and makes rows inconsistent with their absolute addresses and with source records.

Users can select an occupied byte or a range with Shift+click, but Ctrl+C does not copy that selection as a readable byte sequence to the system clipboard. The existing Copy range command stores a sparse image for the application's paste workflow; it does not provide plain-text hex copying.

## Solution

For a configured width `W`, each 16 MiB address region has one row-grid origin at its start (the lower 24 address bits are zero). Within that region, rows start at `origin + n * W`; the grid does not reset at memory Block or source-record boundaries. Each occupied byte appears at the cell for its absolute offset from that region's origin. Unoccupied cells remain blank. For example, at width 17 (`0x11`), row starts in the region beginning at `0xA0000000` include `0xA0000000`, `0xA0000011`, `0xA0000022`, and so on. A byte at `0xA0000002` appears at offset 2 in row `0xA0000000`; a byte at `0xA0001002` appears in row `0xA0001001`. In the region beginning at zero, a byte at `0x00000002` appears in row `0x00000000`.

Ctrl+C copies the active byte or selected range to the system clipboard as uppercase, two-digit hexadecimal tokens separated by one space, such as `B6 5A C6`. A single-byte selection produces one token. When the selection spans an address gap, copy only occupied bytes in ascending address order and omit gap placeholders.

Editor and Compare use the same row alignment. Compare's current model appears to align globally already; verify it against the acceptance examples and preserve or correct it as necessary.

## User Stories

1. As an Editor user, I want rows to start on one width-based grid within each 16 MiB address region, so that Block and source-record boundaries do not shift row addresses.
2. As an Editor user, I want a segment beginning at `0xA0000002` to appear in the row beginning at `0xA0000000` when the width is 17, so that the address offset remains visible.
3. As an Editor user, I want the next width-17 row to begin at `0xA0000011`, so that custom-width row addresses advance by the configured width.
4. As an Editor user, I want occupied bytes to remain at cells matching their absolute addresses, so that changing row width does not shift data.
5. As an Editor user, I want cells for unoccupied addresses to remain blank, so that the display does not invent byte values.
6. As an Editor user, I want a display row to be allowed to begin before a memory segment, so that the first byte of a segment can appear at a nonzero cell offset.
7. As an Editor user, I want source-record boundaries not to affect display rows, so that imported S-Record data is laid out by address rather than by record.
8. As an Editor user, I want global alignment to work for every supported width from 1 through 128, so that custom widths behave consistently with presets.
9. As an Editor user, I want the last partial row near `0xFFFFFFFF` to remain safe, so that the highest valid addresses can be displayed without overflow.
10. As an Editor user, I want address navigation to open the globally aligned row containing that address, so that navigation agrees with the visible grid.
11. As an Editor user, I want selection and editing to retain the byte's absolute address after realignment, so that grid changes do not alter which data I selected.
12. As a Compare user, I want both panels to use the same row-grid origin for each address region, so that corresponding addresses remain aligned.
13. As a Compare user, I want differences-only navigation to use the same region-aligned row starts, so that filtered and full views agree.
14. As a user, I want Compare's row alignment verified with nonzero offsets and custom widths, so that it follows the same rule as Editor.
15. As an Editor user, I want Ctrl+C to copy a selected byte to the system clipboard, so that I can paste its hex value into another application.
16. As an Editor user, I want Ctrl+C to copy a selected range as a space-separated byte sequence, so that I can reuse multiple values directly.
17. As a user, I want copied bytes formatted as uppercase two-digit hex, so that output is consistent regardless of their displayed column or current focus.
18. As a user, I want exactly one space between copied byte tokens, so that the result has the requested `XX XX XX` format.
19. As a user, I want one selected byte copied without leading or trailing spaces, so that the clipboard text is clean.
20. As a user, I want bytes in a copied range ordered by ascending address, so that the sequence follows the memory image.
21. As a user, I want unoccupied addresses in a copied range omitted, so that the clipboard contains only stored byte values.
22. As an Editor user, I want a plain byte click to define the Ctrl+C source, so that the active byte is easy to copy.
23. As an Editor user, I want a Shift+click range to define the Ctrl+C source, so that I can copy a contiguous address selection across rows.
24. As a Compare user, I want Ctrl+C to copy the selection from the panel where I made it, so that the source of the copied bytes is clear.
25. As a Compare user, I want copying from one panel to leave the other panel's selection unchanged, so that copying does not disturb my comparison context.
26. As a user of the existing Copy range and Paste editing commands, I want those commands to retain their sparse-image semantics, so that text copying does not disrupt edit operations.

## Implementation Decisions

- Use the existing absolute-address byte and range selection model as the source for Ctrl+C. Plain byte selection copies one address; Shift+click copies the inclusive selected address range.
- Format clipboard text as uppercase `XX` values with one ASCII space between adjacent copied values. Do not include addresses, ASCII characters, line breaks, or placeholders for address gaps.
- Read only occupied bytes in the selected range and emit them in ascending address order.
- Keep system clipboard copying separate from the existing internal sparse-image clipboard used by Paste.
- Anchor rows at each 16 MiB address-region start. Within a region, determine row starts from that fixed origin and the current width; never rebase a row to the beginning of an individual Block or source record.
- Preserve sparse memory semantics. Address gaps remain blank and do not become occupied through display alignment or copying.
- Keep Editor and Compare row mappings consistent for ordinary navigation, difference navigation, selection, and address boundaries. Use the address region's 16 MiB boundary as the fixed origin shared across all Blocks and both Compare panels.
- Preserve current selection addresses when a row width changes and show the globally aligned row that contains the selection start.
- Keep behavior consistent for normal and windowed large-image views.
- No glossary term or ADR is required. These decisions use existing concepts of memory image, occupied byte, address gap, and selection and do not introduce a difficult-to-reverse architecture choice.

## Testing Decisions

- Prefer observable behavior at the highest useful seam. Test row address labels and byte-cell placement through the public Editor and Compare models, rather than testing private range arithmetic.
- Extend the existing STA WPF acceptance seams for Editor and Compare selection to invoke Ctrl+C and observe plain-text clipboard output. Keep tests serialized because WPF and the system clipboard are process-level resources.
- Cover the same interaction in normal and windowed Editor layouts, and in both Compare panels, where the existing acceptance setup permits.
- Cover one byte; multiple selected occupied bytes; ascending order; uppercase formatting; exact spacing; and ranges crossing rows and address gaps.
- Cover Editor widths 1, 3, 17, 24, 80, and 128, including a segment starting at `0xA0000002`, source records that begin at offset 2 and continue every `0x10`, sparse Blocks within one address region, neighboring 16 MiB regions, and the final address `0xFFFFFFFF`.
- Cover Compare at custom widths and verify ordinary and differences-only navigation use the address-region origin in both panels. Test both equal-width panels at identical addresses.
- Assert that changing width preserves selected absolute addresses and that blank cells remain unoccupied.
- Existing prior art includes public model tests for Editor and Compare row mapping, Editor and Compare WPF selection acceptance tests, and row-width acceptance tests.

## Out of Scope

- Adding mouse-drag range selection; existing plain click and Shift+click selection define this feature's copy source.
- Changing the internal sparse-image Copy range and Paste behavior or the edited memory image.
- Copying ASCII renderings, addresses, source-record text, or gap placeholders.
- Filling address gaps or changing memory-image occupancy, Block boundaries, parsing, saving, export formats, or the 32-bit address range.
- Adding a configurable clipboard format or delimiter.

## Further Notes

- A width-17 example with S-Record records beginning at `0x0002`, `0x0012`, and `0x0022` places bytes according to one grid anchored at `0x00000000`; record starts do not force row starts.
- The user confirmed that the width-17 grid in the region beginning at `0xA0000000` continues through `0xA0001002`, whose containing row begins at `0xA0001001`.
- The companion issues separate Editor row alignment from selection-to-system-clipboard behavior. Compare alignment is included as a verification criterion because its existing model arithmetic must be checked against the confirmed region-origin rule.
- The settled clipboard gap rule is: omit unoccupied addresses and copy only occupied bytes in the inclusive selected range.
