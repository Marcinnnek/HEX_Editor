# Comparison Difference Groups

Status: accepted

Planning provenance: This specification preceded implementation; its [implementation tickets](../hex-editor-2026-11-06/spec.md) were added retrospectively.

Navigate between maximal runs of differing consecutive addresses, rather than between individual bytes or display rows. Next from within a run skips to the first address of the next run; Previous skips to the last address of the preceding run. Respect the selected comparison address range, do not wrap, and leave the cursor unchanged with a status message when no further group exists. Sparse gaps and equal addresses split groups. Keep both panels synchronized and preserve existing difference colors.
