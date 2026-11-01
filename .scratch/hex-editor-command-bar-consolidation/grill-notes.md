# Command Bar Design Decisions

Confirmed by the user on 2026-11-01.

1. Move Bytes per row to the top row and make it operate on the active Editor or Compare tab.
2. Move ASCII to the same row; remove the separate toolbar.
3. Move Compare images and Merge images into File immediately after Close Image, preserving their behavior.
4. Remove the duplicate Bytes per row and Show ASCII controls from Compare.
5. Initially, place Bytes per row and ASCII after View and before Tools and Preferences.
6. Keep the existing row-width presets and Show ASCII command in View.
7. Remove the duplicate top-level Export menu; File retains the export commands.

The user later clarified that Bytes per row and ASCII belong after Preferences. This supersedes decision 5 and sets the final top-row order to File, Edit, View, Tools, Preferences, Bytes per row, ASCII.

No glossary term or ADR is needed: these decisions change command placement and presentation, without changing a domain concept or making a hard-to-reverse architectural choice.
