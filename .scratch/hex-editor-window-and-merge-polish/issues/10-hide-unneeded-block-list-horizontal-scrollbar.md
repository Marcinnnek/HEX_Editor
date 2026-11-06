# 10: Hide unneeded Block-list horizontal scrollbar

Status: resolved

Spec: [Window, Block List, and Merge Polish](../spec.md)

## Problem

Editor and Compare Block lists show a horizontal scrollbar even when every description is fully visible. The shared Block item template forces a 680-pixel minimum width.

## Acceptance

- [x] Editor Block list has no horizontal scrolling when all descriptions fit.
- [x] Both Compare Block lists have no horizontal scrolling when their descriptions fit.
- [x] Horizontal scrolling remains available for descriptions that exceed a narrow list's viewport.
- [x] Block descriptions remain on one line, and selection and navigation remain intact.

## Answer

The shared Block item template set `MinWidth="680"` on every description, creating about 100 pixels of horizontal overflow in the reproduced Editor and Compare layouts even though the text itself fit. Removing that minimum lets WPF size each item to its one-line text. Both new rendered-window tests failed before the change and pass afterward. The existing narrow-layout tests were updated to require real overflow, and all eight Editor and Compare Block-list tests pass.
