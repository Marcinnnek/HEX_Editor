# 11: Match Compare Block header margins to Editor

Status: resolved

Spec: [Window, Block List, and Merge Polish](../spec.md)

## Problem

The filename above each Compare Block list uses the GroupBox's default header spacing. The Editor applies a custom `6,4,4,6` margin to its filename, so the headers do not align visually.

## Acceptance

- [x] Editor and both Compare Block-list filename headers use the same text margins.
- [x] The shared styling does not change Block-list selection, scrolling, or panel layout.

## Answer

The Editor and both Compare GroupBoxes now reference one `MemoryBlockGroupHeaderTemplate` with `6,4,4,6` text margins. All eight existing Editor and Compare Block-list layout tests pass. A standalone win-x64 build is available at `artifacts/publish/win-x64-block-header-margins/HexEditor.UI.exe`.
