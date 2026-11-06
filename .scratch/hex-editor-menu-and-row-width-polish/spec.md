# Export, Gap Labels, and Active Row Width

Status: accepted

Planning provenance: This specification preceded implementation; its [implementation tickets](../hex-editor-2026-11-06/spec.md) were added retrospectively.

Replace three File export entries with one Export command opening the existing options dialog. Preselect selected Blocks when present, otherwise the selected byte range when present, otherwise the whole image; retain the dialog's other choices and export behavior. Replace `Skipped` in collapsed gap labels with `Empty`, meaning no stored bytes rather than `00` values.

Keep bytes per row separately for each Editor or Compare tab. The shared field always reflects the active tab and changing it updates that tab immediately, even when another tab's previous field text happened to equal the chosen value. Switching tabs discards uncommitted width text and shows the destination tab's stored width.
