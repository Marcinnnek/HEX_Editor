# 02: Order files and preview the merge

Status: resolved

Spec: [Ordered Multi-File Merge](../spec.md)

## What to build

Allow selecting multiple files, arranging them top to bottom by drag and drop, and inspecting the resolved image before Apply.

## Acceptance

- [x] A nonempty Editor image appears as a fixed first row; imported files can be reordered below it.
- [x] An empty Editor requires at least two imported files.
- [x] Dragging above or below a row places the file at the corresponding position, including the final position.
- [x] Preview shows input order, occupied-byte count, overlap counts, and Before/Resolved image comparison.
- [x] Applying the preview commits the result once; closing either dialog without Apply changes nothing.

## Verification

The ordering dialog and preview dialog are wired through `MainWindow`; `OrderedMemoryMergeTests` verify the resulting precedence. Drag placement and preview layout are suitable for a human UI review.
