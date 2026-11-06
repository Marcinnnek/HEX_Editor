# 01: Delete or zero selected occupied bytes

Status: resolved

Spec: [Selection Editing and Clipboard Paste](../spec.md)

## What to build

Make Delete on an occupied-byte selection offer removal, zeroing, or cancellation in Editor and the editable Compare base.

## Acceptance

- [x] Delete uses the selected address interval without reopening a manual range dialog.
- [x] Remove deletes occupied addresses; Set bytes to `00` changes only occupied addresses and leaves gaps empty.
- [x] Cancel changes nothing, and each successful choice is one Undo step.
- [x] The Compare reference panel remains read-only.

## Verification

`SelectedRangeZeroTests` cover sparse zeroing and Undo; the Delete choice is wired to both editable views.
