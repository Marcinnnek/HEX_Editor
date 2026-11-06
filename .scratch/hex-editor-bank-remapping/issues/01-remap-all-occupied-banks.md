# 01: Remap all occupied source banks

Status: resolved

Spec: [Whole-Image Bank Remapping](../spec.md); [ADR 0004](../../../docs/adr/0004-linearize-spaced-source-banks.md)

## What to build

Transform every occupied byte in the selected source range by the bank mapping formula, without treating source block boundaries as bank boundaries.

## Acceptance

- [x] Default the inclusive source range to the first through last occupied address; allow a different range.
- [x] Default bank size and offset to `0x10000`; require `offset >= size > 0` and an explicit linear base.
- [x] Keep occupied bytes outside the source range at their original addresses.
- [x] Reject occupied interbank gaps and 32-bit destination overflow without changing the image.
- [x] Evaluate collisions after vacating all moved source addresses.

## Verification

`MemoryBankRemapTests` cover spaced banks, unchanged bytes, collision accounting, and invalid configurations.
