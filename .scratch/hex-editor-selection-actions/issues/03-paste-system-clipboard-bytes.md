# 03: Paste system clipboard HEX bytes

Status: resolved

Spec: [Selection Editing and Clipboard Paste](../spec.md)

## What to build

Paste a contiguous HEX byte sequence from the system clipboard at the start of an editable byte selection.

## Acceptance

- [x] Ctrl+V and the context menu parse whitespace-separated HEX byte text.
- [x] A confirmation shows byte count, target addresses, and overlap count, with overwrite/keep/cancel choices.
- [x] Exactly the clipboard byte count is written; a longer selected tail is untouched.
- [x] Reject 32-bit address overflow before changing the image.
- [x] One paste is one Undo step; the internal sparse Copy range/Paste workflow remains separate.
- [x] The Compare reference panel cannot be pasted into.

## Verification

`SystemClipboardPasteTests` cover parsing, overlap policy, overflow, selected-tail preservation, and Undo.
