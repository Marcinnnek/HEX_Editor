# 09: Clear Editor dirty indicators after window save

Status: resolved

Spec: [Window, Block List, and Merge Polish](../spec.md)

## Problem

After editing and successfully saving from the main window, the Editor document is clean but the status bar still says `Modified` and the window title keeps its asterisk. The existing title test saves through `MainViewModel`, whereas the window's Save and Save As commands save the document directly.

## Acceptance

- [x] Save As from the main window clears the Editor `Modified` indicator and title asterisk immediately after a successful save.
- [x] Editing again and using Save from the main window clears both indicators again.
- [x] Save failure or cancellation retains the dirty indicators.
- [x] The saved byte markers and active Compare title continue to reflect the saved document.

## Answer

The window's asynchronous Save and Save As commands saved `EditorDocument` directly. That changed the document's saved revision, but bypassed the view model's post-save refresh, leaving the Editor status and title stale. Both window commands now run the same post-save refresh as the view model's save methods, only after successful completion. It updates the Editor state, title, and edited-byte display while preserving active Compare title behavior.

The new rendered-window regression test first failed with document state clean and status text still `Modified`; it now passes for Save As followed by another edit and Save. The focused save, cancellation, and active-title tests pass (9 tests), as do the status and viewport tests (7 tests).
