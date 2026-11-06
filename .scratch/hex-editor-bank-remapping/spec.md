# Whole-Image Bank Remapping

Status: accepted

Planning provenance: This specification preceded implementation; its [implementation tickets](../hex-editor-2026-11-06/spec.md) were added retrospectively.

Offer address remapping for the active editable image, including the base image in Compare. Default the inclusive source range to the minimum and maximum occupied addresses; allow editing it. Single bank size and offset between bank starts both default to `0x10000`, are configurable, and require `offset >= size > 0`. Require an explicit linear destination base. Anchor bank windows at the selected source start and map every occupied byte in those windows by ADR 0004. Keep bytes outside the source range at their addresses. Reject an occupied byte in the space between bank windows, destination overflow, or an invalid resulting image atomically.

Preview destination collisions after vacating all remapped source bytes. Offer a global keep/overwrite/cancel decision with per-range exceptions. Keeping an existing destination byte discards the moved byte; show this count. Apply the entire result as one undoable edit, preserving the image's source association and dirty state semantics.
