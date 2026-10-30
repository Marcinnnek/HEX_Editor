# 03: Compare the current base image with one new file

**What to build:** Compare uses the active in-memory base image and asks for only a reference file. With no open image, it takes two selections and opens the first in Editor; canceling the second selection leaves that Editor image open.

**Blocked by:** 02: Give the Editor a stable image document.

**Status:** resolved

- [x] Compare from Editor with an open image requests only one additional file and uses unsaved in-memory bytes as the left base.
- [x] Compare with no image selects a base and a reference; the base opens in Editor.
- [x] Canceling reference selection after loading a new base leaves the base in Editor and creates no Compare tab.
- [x] Canceling with an existing base or encountering a reference parse error leaves that document and its dirty state intact.
- [x] Verify these flows through workspace operations and a picker-driven UI acceptance scenario.

## Comments

- Implemented `CompareCurrentImageWorkflow.StartAsync` as the picker-driven workspace operation. The WPF Compare command supplies the native file picker and selects Editor as soon as a newly selected base is imported. The reference import is independent of the active document, including when the selected reference path equals the base file path.
- Automated picker scenarios cover one-prompt dirty-base comparison, two-prompt empty-Editor comparison, second-picker cancellation, existing-base cancellation and reference parse failure, and comparing unsaved bytes against the saved file. The full test project passed: 109 tests, 0 failures. WPF compiled; native dialog interaction was not manually exercised.
