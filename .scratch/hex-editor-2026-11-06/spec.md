# Firmware Image Editing Extensions

Status: accepted decisions; retrospective planning record

The user confirmed the design decisions across Q1–Q42 before implementation. The six feature specifications below record those decisions. Implementation began before implementation tickets were written; the tickets linked here were created retrospectively on 2026-11-06. Their resolved status describes the current code, not a completed pre-implementation review.

| Feature | Specification | Implementation tickets |
| --- | --- | --- |
| Difference groups | [Comparison difference groups](../hex-editor-difference-groups/spec.md) | [01 model navigation](../hex-editor-difference-groups/issues/01-navigate-contiguous-difference-groups.md), [02 Compare feedback](../hex-editor-difference-groups/issues/02-show-navigation-boundary-feedback.md) |
| VBF import | [Ford/Volvo VBF import](../hex-editor-vbf-import/spec.md) | [01 parse and validate](../hex-editor-vbf-import/issues/01-parse-and-validate-vbf-blocks.md), [02 compression and protection](../hex-editor-vbf-import/issues/02-decode-unencrypted-compressed-blocks.md), [03 application workflows](../hex-editor-vbf-import/issues/03-integrate-vbf-open-and-save-as.md) |
| Bank remapping | [Whole-image bank remapping](../hex-editor-bank-remapping/spec.md) | [01 transformation and collisions](../hex-editor-bank-remapping/issues/01-remap-all-occupied-banks.md), [02 preview and commit](../hex-editor-bank-remapping/issues/02-preview-and-apply-bank-remap.md) |
| Ordered merge | [Multi-file merge](../hex-editor-multi-file-merge/spec.md) | [01 ordered overlay](../hex-editor-multi-file-merge/issues/01-overlay-files-in-order.md), [02 ordering and preview UI](../hex-editor-multi-file-merge/issues/02-order-files-and-preview-merge.md) |
| Selection actions | [Selection editing and clipboard paste](../hex-editor-selection-actions/spec.md) | [01 Delete choices](../hex-editor-selection-actions/issues/01-delete-or-zero-selected-bytes.md), [02 Fill context action](../hex-editor-selection-actions/issues/02-fill-selected-range-with-pattern.md), [03 system clipboard paste](../hex-editor-selection-actions/issues/03-paste-system-clipboard-bytes.md), [04 exterior outline and menus](../hex-editor-selection-actions/issues/04-render-selection-outline-and-context-menus.md) |
| Menu and row width | [Export, gap labels, and active width](../hex-editor-menu-and-row-width-polish/spec.md) | [01 Export](../hex-editor-menu-and-row-width-polish/issues/01-unify-export-entry.md), [02 Empty gap label](../hex-editor-menu-and-row-width-polish/issues/02-name-address-gaps-empty.md), [03 active row width](../hex-editor-menu-and-row-width-polish/issues/03-synchronize-active-row-width.md) |

The bank mapping formula and rationale are in [ADR 0004](../../docs/adr/0004-linearize-spaced-source-banks.md). Domain terms are in [GLOSSARY.md](../../GLOSSARY.md).

## Review notes

- The implementation tickets did not serve as a gate before coding. Review the specifications and tickets as a retrospective record before treating this effort as accepted.
- VBF codec tests use constructed fixtures. Real Ford/Volvo files can reveal variants not described by the available Vector manual or represented in the fixtures.
- The context-menu and selection-outline work has automated model and WPF coverage; a visual Light/Dark review on a running app remains useful.
