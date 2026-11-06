# 02: Name address gaps Empty

Status: resolved

Spec: [Export, Gap Labels, and Active Row Width](../spec.md)

## What to build

Replace the misleading `Skipped` label for a collapsed address gap with `Empty`.

## Acceptance

- [x] Editor and Compare gap descriptions say `Empty` with the gap address range.
- [x] The marker represents absent occupied bytes and does not insert `00` values.

## Verification

Gap-row rendering and selection tests continue to pass with sparse gaps.
