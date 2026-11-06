# 01: Overlay merge inputs in order

Status: resolved

Spec: [Ordered Multi-File Merge](../spec.md)

## What to build

Fold importable memory images into the current Editor image in the displayed order, with the last supplier of an address winning.

## Acceptance

- [x] An occupied Editor image is the fixed first input, including unsaved bytes.
- [x] With an empty Editor, the first selected file is the base; the result has no source-file association and requires Save As.
- [x] Every later input replaces overlapping bytes, whether values differ or are identical; there are no per-range choices for merge order.
- [x] Reject a result beyond the occupied-byte limit and apply a successful merge as one Undo step.
- [x] Import failure or Cancel leaves the Editor image unchanged.

## Verification

`OrderedMemoryMergeTests` cover three-way precedence, empty Editor, occupied-byte counts, and Undo.
