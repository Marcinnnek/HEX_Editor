# 02: Preview and apply a bank remap

Status: resolved

Spec: [Whole-Image Bank Remapping](../spec.md)

## What to build

Offer the transformation in Editor and the editable Compare base, review collisions, then apply it as one reversible document edit.

## Acceptance

- [x] Show moved-byte and collision counts before Apply.
- [x] Provide global overwrite/keep choices and per-collision-range exceptions; Cancel changes nothing.
- [x] Keeping an existing destination byte discards its colliding moved byte.
- [x] Apply to the active editable document with one Undo and preserve its source association and dirty state.
- [x] Reject a stale preview if the source image changed before Apply.

## Verification

`MemoryBankRemapTests` cover collision resolution, large repeated ranges, and one-step Undo.
