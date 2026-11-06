# 04: Render one exterior selection outline and themed context menus

Status: resolved

Spec: [Selection Editing and Clipboard Paste](../spec.md)

## What to build

Make selected bytes readable as one region in HEX and ASCII, and remove the default context-menu icon gutter that appears as a light vertical band.

## Acceptance

- [x] Draw only exterior edges where selected occupied bytes touch horizontally or vertically.
- [x] Keep separate outlines across absent-address gaps and preserve difference, edit, and Find indicators.
- [x] Right-click exposes applicable actions without clearing the byte selection.
- [x] Editor and Compare base menu items and separators use explicit themed templates in Light and Dark.

## Verification

`ByteOutlineEdgesTests` cover horizontal and vertical adjacency and gaps. The Editor context menu was opened and rendered in Light and Dark to check its final appearance.

## Comments

- 2026-11-06: A screenshot showed stray light fragments in the context menu icon gutter. Added an explicit `ContextMenu` template for Editor and Compare, removing the default gutter. Rendered the opened Editor menu in Light and Dark; both show aligned items and a clean separator.
