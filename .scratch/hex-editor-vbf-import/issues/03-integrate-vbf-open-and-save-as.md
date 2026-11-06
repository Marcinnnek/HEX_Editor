# 03: Integrate VBF open and Save As workflows

Status: resolved

Spec: [VBF Import](../spec.md)

## What to build

Make VBF available wherever firmware images are opened, while keeping it input-only.

## Acceptance

- [x] Open, Compare, and Merge file pickers include `.vbf`.
- [x] Internal block overlap choices are offered before a VBF image enters Open, Compare, or Merge.
- [x] Save for a VBF source opens Save As for Intel HEX or Motorola S-Record.
- [x] Export and Save As never offer VBF output; the file service rejects a VBF export request.
- [x] The active status identifies a VBF source.

## Verification

`VbfImportTests` cover import-only save/export behavior; workflow tests cover Open and Compare image loading.
