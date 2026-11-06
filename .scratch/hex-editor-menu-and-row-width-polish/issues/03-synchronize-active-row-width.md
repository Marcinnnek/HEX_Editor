# 03: Synchronize bytes per row with the active tab

Status: resolved

Spec: [Export, Gap Labels, and Active Row Width](../spec.md)

## What to build

Keep row width per tab while ensuring the shared control reflects and edits the currently selected Editor or Compare tab.

## Acceptance

- [x] Switching tabs updates both the field text and its selected preset.
- [x] Choosing a preset updates the active tab even if that preset was selected on the previous tab.
- [x] Switching tabs discards uncommitted numeric text and shows the destination tab's saved width.
- [x] Valid custom widths remain available, and other tabs retain their widths.

## Verification

`CompareRowWidthAcceptanceTests` reproduce the stale-preset defect and cover uncommitted text and custom widths.
