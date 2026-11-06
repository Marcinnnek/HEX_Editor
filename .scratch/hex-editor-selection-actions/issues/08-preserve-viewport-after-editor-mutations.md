# 08: Preserve viewport after editor mutations

Status: resolved

Spec: [Selection Editing and Clipboard Paste](../spec.md)

## What to build

Keep the Editor near the edited address after byte editing, selected-byte deletion or zeroing, and Fill with Pattern.

## Acceptance

- [x] Editing an occupied selected byte keeps that byte selected and visible at its prior viewport position.
- [x] Zeroing or filling selected bytes keeps the affected address in view.
- [x] Deleting selected bytes keeps the viewport near the removed range, even if its rows change.
- [x] Refreshes caused by Undo and Redo follow the same navigation rule.

## Verification

The live Editor regression test reproduced the bug: editing row 54 moved the first visible row from 50 to 0. Four live tests now pass for Edit Row, zero, asynchronous Fill, deletion, Undo, Redo, removal of several rows, and opening a different document. `RowViewportStateTests` (6) and the existing live byte-selection workflow (1) also pass.

## Answer

The viewport used the immutable image instance as its source identity. Every edit replaced that instance, so the viewport treated the result as a new file and reset to row zero. It now uses the stable Editor document identity, while a genuinely different document still resets. Editing refreshes also retain the Editor status selection.
