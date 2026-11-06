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

`SelectionOutlineEdgesTests` cover horizontal and vertical adjacency and gaps. A visual Light/Dark review remains useful for final appearance.
