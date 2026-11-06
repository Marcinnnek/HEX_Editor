# Ordered Multi-File Merge

Status: accepted

Planning provenance: This specification preceded implementation; its [implementation tickets](../hex-editor-2026-11-06/spec.md) were added retrospectively.

Allow selecting several importable files for Merge and rearranging them by drag and drop in a top-to-bottom list. When Editor contains an image, show it as a fixed first entry and reorder files below it. When Editor is empty, the first file is the base image. Apply entries in list order; the last file supplying an address wins, including identical-value overlaps. This ordering is the only conflict rule; do not offer per-range overrides. Show a preview before applying and commit the result as one undoable edit. If Editor was empty, open the result as an unsaved image requiring Save As, protecting all input files. Cancel and any failed import leave Editor unchanged.
