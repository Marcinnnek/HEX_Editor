# 12: Delete Block in the relocation dialog

**What to build:** Let a user delete one selected Editor Block from the existing Relocate memory segment dialog while retaining current block navigation and relocation behavior.

**Blocked by:** None (can start immediately).

**Status:** resolved

- [x] Delete is available only for one selected editable Block in the relocation dialog, whether opened from the menu or by double-click; it is unavailable for multiple selections.
- [x] Delete targets the original selected Block even if the destination address field contains an uncommitted value.
- [x] Confirmation displays the Block's start, end, and length; Cancel makes no change.
- [x] Confirmed Delete removes the entire memory segment, closes the dialog, and is reversible with one Undo.
- [x] The next Block is selected after deletion, or the previous Block when the deleted Block was last, without an automatic view jump.
- [x] No action buttons are added to Block lists, single-click navigation stays in place, and the Compare reference image remains read-only.
