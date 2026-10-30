# 19: Keep Previous/Next Difference navigation safe

**What to build:** Previous Difference and Next Difference navigate between comparison differences without terminating the application, including when two different files are compared. If no row is selected, use the address in the comparison address field as the starting point.

**Blocked by:** None (can start immediately; difference navigation already exists).

**Status:** ready-for-agent

- [ ] Reproduce and fix the reported exception when using Previous Difference or Next Difference while comparing two different files.
- [ ] With a selected byte, Next selects the closest difference at a higher address and Previous selects the closest difference at a lower address.
- [ ] Without a selected row, use the address field as the starting address and search in the requested direction.
- [ ] If no difference exists in the requested direction, leave the current view and address unchanged and keep the application open.
- [ ] Log recoverable failures and show a concise status-bar message; never let a navigation exception close the application or comparison tab.
- [ ] Cover different files, selected and unselected rows, sparse address gaps, and no further differences in regression checks.

## Comments

- User report (2026-10-30): clicking Previous Difference or Next Difference while comparing two different files closes the program with an exception.
- User decision: when no row is selected, search from the address shown in the comparison address field, in the direction requested.
