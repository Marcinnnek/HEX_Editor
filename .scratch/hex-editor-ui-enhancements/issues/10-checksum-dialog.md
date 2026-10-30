# 10: Calculate and write checksums through a dialog

**What to build:** Checksum and CRC operations use a focused dialog for algorithm, address range, missing-address treatment, width, byte order, custom parameters, and optional result writing instead of permanent toolbar inputs.

**Blocked by:** 02: Give the Editor a stable image document.

**Status:** ready-for-human

- [x] The dialog exposes the existing additive, standard CRC, and custom CRC choices with all required parameters and validates them before calculation.
- [x] The user can inspect a result without changing the memory image or save state.
- [x] Writing a result requires an explicit address and overlap decision and is one reversible data change.
- [x] Editing any input byte marks the displayed result stale or recomputes it through the base document's change notification.
- [x] Cancel, invalid parameters, or declined overlap leave bytes unchanged; errors identify the invalid field or address.
- [x] Preserve existing known-vector and sparse-gap calculation behavior through public calculation/workspace tests.
- [ ] Inspect the WPF dialog.

## Comments

- Merged on `integration` as `7fb06c6`. The implementer reports the solution test suite passed before and after merging. Live dialog inspection remains outstanding.
