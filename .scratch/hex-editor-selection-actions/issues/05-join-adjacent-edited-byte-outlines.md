# 05: Join outlines around adjacent edited bytes

Status: resolved

Spec: [Selection Editing and Clipboard Paste](../spec.md)

## What to build

Render only exterior edges around groups of edited bytes in the HEX and ASCII columns, using the same horizontal and vertical adjacency rules as selection.

## Acceptance

- [x] Horizontally and vertically adjacent edited bytes do not draw internal borders.
- [x] Unedited bytes and absent-address gaps split edited groups.
- [x] Difference backgrounds, Find markers, and selection outlines remain visible.

## Verification

`ByteOutlineEdgesTests` cover adjacency and gaps. `AsciiRenderingTests` checks an edited group in HEX and ASCII and preserves the existing Find and difference indicator checks. All seven focused tests pass.
