# 01: Navigate contiguous difference groups

Status: resolved

Spec: [Comparison Difference Groups](../spec.md)

## What to build

Treat a maximal run of consecutive differing addresses as one navigation target, independent of display row boundaries.

## Acceptance

- [x] Equal bytes and unoccupied address gaps split groups.
- [x] Next from inside a group finds the first address of the next group; Previous finds the last address of the preceding group.
- [x] A selected comparison address range limits navigation; navigation never wraps.
- [x] Group navigation keeps the two Compare panels synchronized.

## Verification

`DifferenceNavigationSkipsTheCurrentContiguousGroup` and existing comparison-model navigation tests.
