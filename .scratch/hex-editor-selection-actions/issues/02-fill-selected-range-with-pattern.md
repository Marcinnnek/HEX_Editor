# 02: Fill the selected range with a pattern

Status: resolved

Spec: [Selection Editing and Clipboard Paste](../spec.md)

## What to build

Expose the existing Fill operation from the selection context menu with the selected addresses prefilled.

## Acceptance

- [x] Editor and the editable Compare base expose Fill with Pattern after a byte selection.
- [x] The Editor context menu contains only one fill entry; it does not duplicate the same dialog as Fill Range.
- [x] The dialog initially uses the selected inclusive start and end addresses.
- [x] The existing fill pattern, keep/overwrite policy, progress, cancellation, and Undo behavior remain available.
- [x] The Compare reference panel has no Fill action.

## Verification

The context menus and `Fill_Click` use the active editable document and the selected range.

## Comments

- 2026-11-06: Removed the duplicate `Fill Range...` item from the Editor context menu. `Fill with Pattern...` remains the single context command for the existing Fill dialog.
