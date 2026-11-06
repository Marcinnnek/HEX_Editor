# 06: Configure selected and edited byte backgrounds and borders

Status: resolved

Spec: [Selection Editing and Clipboard Paste](../spec.md)

## What to build

Fill selected and edited byte cells in HEX and ASCII while retaining their exterior outlines. Expose independent background and border colors for both states in Preferences for Light and Dark themes.

## Acceptance

- [x] Selected byte background matches selected row background and changes with its preference.
- [x] Edited bytes have a distinct configurable background; selected background takes precedence when both states apply.
- [x] Edited and selected outlines and Find markers remain visible over the fills.
- [x] Four colors are editable per palette, persisted, resettable, and loaded safely from older palette files.

## Verification

`AsciiRenderingTests` passed (4), including HEX and ASCII fill precedence and outline visibility. `PreferencesSessionTests` passed (10), including palette persistence, legacy migration, and the expanded Preferences window. Row selection acceptance checks passed (4) in both layouts.
