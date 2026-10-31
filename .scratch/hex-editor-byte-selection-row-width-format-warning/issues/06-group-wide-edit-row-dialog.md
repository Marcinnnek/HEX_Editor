# 06: Group wide Edit Row dialogs

**What to build:** Edit Row remains compact through 32 bytes and presents wider rows as readable groups with predictable keyboard navigation.

**Blocked by:** 04 — Use a custom 1–128-byte row width in Editor.

**Status:** resolved

- [x] Widths 1–32 retain one visual group. Widths 33–128 use consecutive groups of at most 32 bytes, each with offsets and original/proposed HEX and ASCII values; 80 bytes appears as 32 + 32 + 16.
- [x] Tab visits editable HEX fields, then editable ASCII fields within each group, before moving to the next group. Shift+Tab reverses that order and unoccupied addresses are skipped.
- [x] The dialog fits its content up to the available screen height and adds vertical scrolling only when necessary.
- [x] Editing, validation, OK, Cancel, and one-Undo behavior still apply to the entire logical display row.
- [x] Edit Row session and WPF acceptance scenarios cover a sparse wide row, a full 128-byte row, focus order, and scrolling.
