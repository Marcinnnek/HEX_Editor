# 09: Use focused dialogs for range, insert, and fill

**What to build:** Select Range, Edit/Insert, and Fill collect addresses and data in task-specific dialogs, so the user can perform these operations without permanent toolbar fields.

**Blocked by:** 02: Give the Editor a stable image document.

**Status:** ready-for-agent

- [ ] Select Range accepts validated hexadecimal Start and End addresses and updates the active Editor selection without changing data.
- [ ] Edit/Insert collects a target address and bytes, preserves explicit overlap decisions, and never silently replaces occupied bytes.
- [ ] Fill collects a valid range and pattern, applies one reversible transaction, and preserves absolute-address semantics.
- [ ] Cancel or validation failure leaves the document unchanged; dialogs prefill sensible values from the current selection where available.
- [ ] Menu and useful context-menu entry points work without reading hidden toolbar controls.
- [ ] Exercise each end-to-end operation through public workspace behavior and inspect dialog focus and errors in WPF.
