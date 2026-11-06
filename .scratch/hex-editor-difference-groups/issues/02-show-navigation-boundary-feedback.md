# 02: Show difference-navigation boundary feedback

Status: resolved

Spec: [Comparison Difference Groups](../spec.md)

## What to build

Leave the current Compare address unchanged when no later or earlier group exists and report that navigation reached the end.

## Acceptance

- [x] Next with no later group reports that no later differences were found.
- [x] Previous with no earlier group reports that no earlier differences were found.
- [x] Neither action wraps or advances by one byte inside the current group.

## Verification

`ClickingVisibleCompareBytesKeepsBothViewportsAndTheSelectedAddress` covers the single long-group boundary; comparison-model tests cover group selection.
