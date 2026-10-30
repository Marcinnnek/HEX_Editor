# Window, Block List, and Merge Polish — Confirmed Design Decisions

Status: confirmed by the user

## Scope

Refine the active-window title, memory-segment presentation, save-on-close dialog, HEX/ASCII row density, Merge workflow, and application icon. These decisions supersede conflicting presentation details in earlier UI specs. Memory-image and comparison semantics remain as defined in `GLOSSARY.md`.

## Window title

- Derive the main window title from the active tab and update it on tab switches, document edits, saves, and Save As.
- For Editor, show the current file's basename and a `*` when it has unsaved changes, followed by ` - HEX Editor`.
- For Compare, show `Compare: fileA.hex ↔ fileB.hex - HEX Editor`. Place `*` after file A's name when its editable base image is dirty. Returning to Editor restores its title.
- Retain the existing untitled title when Editor has no file.

## Memory blocks

- **Block** is the user-facing name for a memory segment: one contiguous range of occupied addresses, independent of source-file record boundaries. This term is recorded in `GLOSSARY.md`.
- Replace the segment tables in Editor and both Compare panels with clickable lists. Keep each entry on one line in this form: `Block 1 Starts at: 0x08000000 Ends at: 0x080015BB (Length: 0x15BC = 5564)`.
- Number blocks from 1 in ascending start-address order; recompute numbering when segments change. Render addresses and the hexadecimal length with uppercase digits and no unnecessary leading zeroes in the length. Render the decimal length without grouping separators.
- Preserve the full description with horizontal scrolling when the panel is narrow, both in Editor and Compare. Do not wrap or truncate entries.
- Preserve existing interactions: a single click navigates to the block's start, an Editor double-click opens relocation, and Ctrl/Shift selection supports operations on multiple blocks such as export.

## Save-on-close dialog and document lifecycle

- Present a readable message naming the document, with a subtle warning icon matching the active theme. Place `Save`, `Don't save`, and `Cancel` in one aligned row. Make Save the default action; X has the same effect as Cancel.
- On application close, prompt once for each distinct dirty editable document: Editor first, then editable base images of Compare tabs. Never prompt twice for multiple views of the same document.
- Cancel at any point aborts application close. Earlier `Don't save` choices in that attempt do not discard the in-memory edits while the application remains open.
- Preserve the existing individual Compare-tab close rule: prompt when closing the final view of a dirty editable base image.

## HEX/ASCII row density

- In Editor and both Compare panels, `Vertical space between rows = 0` should leave approximately 0–1 visible pixel between consecutive HEX/ASCII text lines at normal display scaling. Higher settings should progressively increase that gap.
- Preserve accurate byte hit testing, row selection, and alignment between HEX and ASCII. The segment lists are outside this spacing preference.
- Earlier UI-polish notes specified about 0.2 character height at zero; this tighter target supersedes them.

## Merge workflow

- When an image is open in the Editor tab, use its current in-memory state as the Merge destination, including unsaved changes, even if Compare is the active tab. Ask the user to choose only the source file. The result stays in the Editor document, with the existing preview, conflict handling, and Undo behavior.
- In this one-file-selection flow, permit the selected source to be the same path as the Editor document. Read the source from disk so the user can merge the open image with the disk version.
- Hide the reverse merge direction when the Editor image is the fixed destination.
- When Editor has no image, retain the two-file selection flow and both merge directions, including when a Compare tab is active.

## Icon

- Use the supplied root `HexEditor.ico` for the EXE, main window and taskbar, and application-owned dialogs. The file is a valid 256 × 256 ICO image supplied by the user.

## Documentation decision

No ADR is needed for these reversible UI and workflow choices. The only new domain term is **Block**, recorded as an alias for **memory segment** in `GLOSSARY.md`.
