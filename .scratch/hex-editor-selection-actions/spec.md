# Selection Editing and Clipboard Paste

Status: accepted

Planning provenance: This specification preceded implementation; its [implementation tickets](../hex-editor-2026-11-06/spec.md) were added retrospectively.

On Delete with an occupied-byte selection, choose Remove bytes, Set bytes to `00`, or Cancel without first asking for a manual range. Zero only occupied selected bytes, preserving sparse gaps. Put Fill with Pattern in the selection context menu and prefill the existing Fill dialog with selected start and end while retaining its fill policy. Provide these actions in Editor and the editable Compare base panel, never the reference panel.

The Editor context menu has one fill entry, labeled `Fill with Pattern...`, which opens the same Fill dialog as the Edit menu command.

Ctrl+V and Paste clipboard bytes parse system clipboard HEX text into a contiguous byte sequence beginning at the selection start. A single confirmation shows byte count, target address, overlap count, and Overwrite/Keep existing/Cancel choices. The operation affects exactly the clipboard byte count, leaves any longer selected tail untouched, rejects 32-bit overflow atomically, and is one Undo step. Retain the separate sparse internal Copy range/Paste workflow.

Draw selection as an exterior outline in HEX and ASCII, without lines between adjacent selected bytes horizontally or vertically. Address gaps split visible outlines. Right-click preserves the selection and exposes applicable actions. Repair context menu rendering in Light and Dark themes.

Adjacent edited bytes likewise share one exterior outline in HEX and ASCII, including across row boundaries. Unedited bytes and address gaps separate edited outlines. Difference backgrounds, Find markers, and selection outlines remain visible.
