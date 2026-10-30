# HEX Editor UI Polish — Confirmed Design Decisions

Status: confirmed by the user

## Scope

Refine Edit Row, memory-segment lists, appearance consistency, and the zero setting for vertical HEX-row spacing. These decisions supersede conflicting UI details in `.scratch/hex-editor-ui-regressions/spec.md`; they do not change memory-image semantics.

## Edit Row

- Keep one horizontal line of 8, 16, or 32 byte columns, matching the active view's **Bytes per row** setting.
- Center the content of every byte column horizontally and vertically, including offsets, original HEX/ASCII values, and proposed input values. Keep row labels left-aligned.
- Tab through editable Proposed HEX cells in ascending byte order, then through editable Proposed ASCII cells in the same order. Skip unoccupied addresses, whose inputs remain disabled. Shift+Tab traverses in reverse. After the final ASCII input, use the normal Windows focus sequence to reach OK.
- Select the entire input value when a cell gains focus by Tab, Shift+Tab, or its first mouse click. A later click in the already-focused input places the caret for a targeted edit.
- Size the dialog height to its controls and keep that height fixed during normal use. Allow emergency vertical scrolling if the available work area is too short; retain horizontal scrolling when a wide row cannot fit at readable size.
- Remove the visible **Selected byte** heading. Keep the selected address as the initial focus and highlight target. This replaces the earlier spec's requirement to show the selected absolute address in the heading.

## Memory-segment lists

- Apply these rules to the Editor list and both Compare lists. A *memory segment* is the contiguous occupied-address range defined in `GLOSSARY.md`; the lists already receive segments in ascending start-address order from the model.
- Show byte count as uppercase, unpadded hexadecimal followed by plain decimal, for example `0x15BC = 5564`.
- Reduce vertical separation between segment rows to about half a character height. Left-align all column values.
- Disable user sorting by column headers. Preserve ascending start-address order, including after an edit or comparison refresh.
- In narrow Compare panels, preserve the entire byte-count text on one line and provide horizontal scrolling rather than clipping or wrapping it.

## Appearance

- Preferences and every other open or newly opened dialog must use the effective application theme. The reported failure sequence is **Dark → Light → Dark**: Preferences reopens with a pale, washed-out surface instead of a coherent Dark appearance.
- A saved Light choice must produce a Light dialog. A saved Dark choice, or Follow Windows when Windows is dark, must produce a Dark dialog. A live Windows theme change must also update dialogs that are already open.
- The current Preferences implementation uses native RadioButton and Slider controls without theme-specific styles; this is an investigation lead, not a confirmed root cause.

## HEX-row density

- At vertical spacing **0**, the visible gap between consecutive text lines should be approximately 0.2 of the character height (about 3 px for a 16 px character), in Editor and both Compare panels. Preserve accurate HEX/ASCII byte hit testing and row selection.
- The current setting of 0 removes only an added row margin; the remaining DataGrid row/cell chrome appears to account for the excess gap. This is an investigation lead, not a confirmed root cause.
- Keep horizontal byte spacing independent of vertical row spacing.

## Documentation decision

No new glossary term is needed: `GLOSSARY.md` already defines **memory segment** and related memory-image concepts. No ADR is warranted for these reversible UI details.
