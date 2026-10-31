# 04: Render a readable Dark appearance throughout the app

**What to build:** Selecting Dark gives the main workspace and its dialogs a coherent, readable appearance while retaining clear selection, focus, ASCII, and comparison-difference cues.

**Blocked by:** None (can start immediately).

**Status:** resolved

- [x] Dark applies to the main window, menus, toolbar, Editor, Compare, status, and existing dialogs rather than leaving mismatched light surfaces.
- [x] Text, inputs, disabled controls, focus, selection, and comparison highlights have readable contrast in Dark.
- [x] Custom HEX/ASCII rendering and window content use theme-appropriate colors without changing memory-image data or absolute-address alignment.
- [x] Switching between Light and Dark updates already open views and newly opened dialogs consistently.
- [x] Existing manual appearance choice remains stored and restored after restart.
- [x] Inspect representative Editor, Compare, Edit Row, and Preferences flows in WPF; test observable appearance state through the existing settings seam.

## Answer

Dark palette resources and control styles now cover the workspace, menus, dialogs, status, and custom HEX/ASCII rendering. Theme switching and persistence tests passed, including an offscreen Dark-to-Light WPF check.
