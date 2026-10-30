# 18: Prevent Compare row activation from closing the application

**What to build:** Double-clicking the left Compare panel to edit a row must not terminate the application. Preserve the safe hit testing and read-only behavior established by issue 01, and report recoverable failures in the status bar with diagnostic context.

**Blocked by:** None (can start immediately; left-panel editing already exists).

**Status:** ready-for-agent

- [ ] Reproduce and fix the reported crash when double-clicking the left panel while comparing two files.
- [ ] A valid occupied base byte opens Edit Row; headers, gaps, separators, empty areas, and unrealized rows do not open it and do not throw an unhandled exception.
- [ ] The reference panel remains read-only and reports its behavior in the status bar.
- [ ] Recoverable failures are recorded in diagnostics with operation and address context where available, without logging byte values or closing the workspace.
- [ ] Verify behavior in the live WPF interaction path as well as the existing row-activation seam.

## Comments

- User report (2026-10-30): after adding files for comparison, double-clicking the left panel can freeze or terminate the program without a visible exception. Issue 01 was marked resolved, so treat this as a reported regression and retain its established safety criteria.
