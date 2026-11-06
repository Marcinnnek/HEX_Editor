# 09: Prevent transient viewport jump during edit

Status: resolved

Spec: [Selection Editing and Clipboard Paste](../spec.md)

## Problem

After Fill with Pattern, the Editor ends at the selected address but the data area visibly moves away and back. Earlier viewport tests checked only row indices, so they missed movement caused by layout changes.

## Acceptance

- [x] Fill with Pattern through the context menu, dialog, and asynchronous operation keeps the selected address and visible row.
- [x] Showing and hiding the Fill progress panel leaves the rendered Editor in the same position.
- [x] The progress panel stays above the byte grid rather than covering the visible data.
- [x] Existing edit, zero, delete, Undo/Redo, and new-document viewport behavior remains intact.

## Answer

The Fill command displays a progress panel docked above the workspace. In a rendered-window test, the panel moved the entire Editor down by 47 pixels, then moved it back when the operation finished. The logical row index never changed, which explains why the earlier tests passed while the user still saw a jump.

The progress panel now overlays the workspace above the byte grid instead of occupying layout space. A full context-menu Fill test measured the Editor's position during both visibility changes: it failed before the fix (Y=159.8 to 206.8 to 159.8) and passes after it. The prior row-redraw optimization was removed because it did not address this effect. Focused Editor viewport, file-operation, Compare scrolling, and title tests pass (18 tests).

A standalone win-x64 build of this fix is available at `artifacts/publish/win-x64-fill-overlay/HexEditor.UI.exe` for visual verification while the previously published executable is running.

## Comments

- User feedback: the visible jump still looks unchanged after this implementation. The recorded answer above is insufficient; investigate the rendered frame and focus or scroll behavior before resolving this ticket again.
- The user narrowed the effect to Fill with Pattern. The full context-menu, dialog, asynchronous Fill test reproduces a 47-pixel movement of the entire Editor when the progress panel becomes visible, then the same movement back when it collapses. The earlier tests only observed logical row indices and missed this layout shift.
