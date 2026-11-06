# 07: Style the Fill dialog controls

Status: resolved

Spec: [Selection Editing and Clipboard Paste](../spec.md)

## What to build

Apply the existing form text style to the repeating-pattern field, use a themed button for the whole-image range shortcut, and show a filled selection dot in the Fill policy radio buttons.

## Acceptance

- [x] The repeating-pattern field has inner padding, centered text height, and spacing below it.
- [x] `Get fill all region` has the same themed appearance as other dialog actions and retains its enabled and disabled behavior.
- [x] Keep existing is visibly selected on open; switching to Overwrite moves the filled dot and retains the fill policy behavior.
- [x] The controls remain readable in Light and Dark appearances.

## Verification

`FillRangeDialogAcceptanceTests` passed (3), including the radio indicator and form field geometry. `PreferencesSessionTests` passed (10) after sharing the radio template; the address field acceptance test passed (1). Rendered and inspected the Fill dialog in Light and Dark.
