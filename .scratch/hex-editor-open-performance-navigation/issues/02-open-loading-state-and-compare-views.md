# 02: Open loading state and Compare views

**What to build:** Replacement Open remains responsive while reading and handles dependent and independent Compare views correctly after the result is known.

**Blocked by:** 01: Open replaces the Editor document.

**Status:** resolved

- [x] During loading, the previous image remains visible and scrollable; tab switching, progress reporting, and Cancel remain available.
- [x] Editing, Save, and starting another Open, Compare, or Merge are unavailable until loading ends; normal command state returns after success, cancellation, or failure.
- [x] Open invoked from any Compare tab still targets Editor and prompts only about the Editor document.
- [x] On success, Compare views based on the replaced Editor document close and independent Compare views, including their unsaved work, remain open.
- [x] On cancellation or failure, all existing Compare views and the old Editor image remain; a successful Open activates Editor.
- [x] Workspace and WPF scenarios verify document identity, command availability, progress, cancellation, and tab behavior.
