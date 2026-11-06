# 01: Use one Export menu entry

Status: resolved

Spec: [Export, Gap Labels, and Active Row Width](../spec.md)

## What to build

Replace the three File export commands with one Export command that opens the existing options dialog.

## Acceptance

- [x] The File menu contains one Export entry.
- [x] The dialog starts with selected Blocks, otherwise the selected byte range, otherwise the entire image.
- [x] The dialog still allows changing scope, output format, and record size.
- [x] Exported Blocks use logical Block indexes rather than backing-range indexes.
- [x] VBF remains import-only and is absent from output choices.

## Verification

`MainWindowToolbarAcceptanceTests` cover the single File entry; file-service tests cover export scopes.
