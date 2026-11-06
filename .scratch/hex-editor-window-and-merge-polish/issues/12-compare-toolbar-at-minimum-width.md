# 12: Arrange Compare toolbar at minimum width

Status: resolved

Spec: [Window, Block List, and Merge Polish](../spec.md)

## Problem

At the minimum window width, Compare's top WrapPanel wraps individual controls. The address box is separated from Go and difference navigation, and row and control margins are inconsistent.

## Acceptance

- [x] Start, End, and Apply range stay together in one row at the minimum supported width.
- [x] Address, Go, Previous difference, and Next difference stay together in one row.
- [x] Wrapped rows have consistent spacing and all controls remain within the available width.
- [x] Existing modes, range validation, and difference navigation retain their behavior.

## Answer

The Compare toolbar now wraps three complete groups: the view selector, range controls, and address/difference navigation. Group margins provide 8 px row and control spacing. A rendered WPF acceptance test checks alignment and available width with the content width inside a 620 px main window. Both Compare address-control tests, all four navigation-session tests, and all three block-list layout tests pass when run in their respective test processes. Running the UI test classes together can trigger an existing WPF cross-thread shared-brush error; the block-list tests pass in isolation.
