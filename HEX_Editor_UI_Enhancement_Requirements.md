# HEX Editor — UI and Workflow Enhancement Requirements

**Document type:** Supplementary requirements specification  
**Revision:** 1.1  
**Date:** 2026-10-30  
**Status:** Agreed for implementation  
**Applies to:** Existing C# / .NET / WPF HEX Editor supporting Intel HEX and Motorola S-Record  
**Relationship:** This document supplements the original `HEX_Editor_Requirements.md`. Existing functionality remains required unless explicitly changed below.

## 1. Purpose and scope

Improve the desktop application's usability by simplifying the main toolbar, moving contextual commands into menus, supporting address-aware in-place editing through a dedicated byte editor dialog, refining compare workflow, and fixing an unhandled exception when double-clicking a row in Compare mode. Apply the **WPF UI** design system and distribute the program as a single executable file.

The Editor workspace SHALL use a full-width HEX table below its memory-block list, analogous to the layout of each Compare panel in the supplied visual reference. The Compare workspace SHALL retain the side-by-side panels, block lists, HEX tables, and result area shown there.

## 2. Main window command layout

### UI-ADD-001 — Menu organization

The application SHALL provide conventional top-level menus, including **File**, **Edit**, and **View**. Commands currently shown in the second toolbar row or in always-visible command panels SHALL be moved into appropriate menus, submenus, or contextual menus.

Recommended organization (menu names and placement can be refined, but all actions must remain accessible):

| Menu | Commands |
|---|---|
| File | Open; Save; Save As; Close Image; Export > Export Image / Export Selected Segments / Export Address Range |
| Edit | Undo; Redo; Edit Selected Row/Bytes; Insert; Fill Range; Cut; Copy; Paste; Delete; Select Range; Relocate Selected Segment; Checksum / Calculate |
| View | Show / Hide ASCII; Bytes per Row (8, 16, 32); Go to Address; Show Memory Blocks; Compare Display Options |
| Compare | Compare with File; Next Difference; Previous Difference; Differences Only / All Regions |
| Merge | Merge Images; conflict-resolution operations where applicable |

A menu command must be enabled only when its prerequisites are satisfied. Existing keyboard shortcuts should be preserved where possible; standard shortcuts should be added when absent.

### UI-ADD-002 — Minimal persistent toolbar

Directly below the menu bar, the main window SHALL display **only** these primary command groups:

1. **File:** Open, Save, Save As.
2. **Comparison:** Compare, Merge.
3. **History:** Undo, Redo.
4. **View:** Bytes per Row selector and Show ASCII toggle (or one compact View control exposing these settings).
5. **Navigate:** Address input and Go action.

The toolbar SHALL NOT permanently display **Selection / Range**, **Edit**, **Relocate**, **Checksum**, or **Export** groups. All corresponding operations SHALL remain reachable through menus and, when useful, context menus. The Compare workspace retains its view and navigation settings row from the visual reference; controls repeated there and in the main toolbar SHALL remain synchronized for the active tab. Bytes-per-row and Show ASCII settings are per-tab; the main toolbar shows and changes the active tab's values. At narrow window widths, primary commands that no longer fit SHALL move into a "More" overflow menu rather than force horizontal scrolling of the workspace.

### UI-ADD-003 — Use WPF UI visual system

The application SHALL adopt the **WPF UI** component library and its Fluent-style visual language for the main window, menus, toolbars, dialogs, navigation, buttons, inputs and notifications, to the extent supported by the library.

Requirements:
- Consistent typography, margins, spacing, corner radii, icons and visual states.
- Theme resources for light and dark appearance. Follow the Windows theme by default and provide manual light/dark overrides persisted across restarts.
- Accessible keyboard focus, disabled states, hover states and readable contrast.
- Reuse a consistent dialog and message presentation pattern instead of mixing unrelated native/control styles.
- Keep the hex viewer's monospace byte alignment, independently of the general UI font.

## 3. File comparison workflow

### CMP-ADD-001 — Compare with current image

When the main Editor window already has an active/open memory image, invoking **Compare** SHALL ask the user to select **only one additional file**. The active image becomes the left/base image and the newly chosen file becomes the right/comparison image.

When there is no active image, the application MAY prompt for two files sequentially (base, then comparison), or offer a clearly labeled two-file selection flow. The first/base file SHALL also become the active Editor image. If the user then cancels the second-file selection, that first file SHALL remain open in Editor without creating a Compare tab. The Editor and all Compare views of the same base image SHALL share current in-memory bytes and one Undo/Redo history; edits SHALL appear immediately in every view. Opening another file in Editor SHALL NOT discard an already open Compare tab or its base image and unsaved changes. Invoking Compare from an active Compare tab SHALL create a new Compare tab with the same left/base image and request only a new right/reference file.

Requirements:
- Do not reopen or ask for the already active image.
- Preserve the active image and any unsaved modifications; comparison must use the current in-memory data, not silently reload the original on-disk file.
- Show both file names (and clearly indicate unsaved changes where applicable).
- If the user cancels the second-file selection, retain the current Editor session unchanged.
- Loading/parsing errors must be handled with an actionable message without losing the existing image.

### CMP-ADD-002 — Compare viewer alignment

Both panels SHALL continue to show **Address | HEX Data | ASCII** side-by-side, aligned by **absolute memory address**, rather than record order or file offset. An absent byte SHALL display as an empty placeholder, distinct from `00` or `FF`.

The **Show ASCII** setting SHALL hide/show ASCII columns in both Compare panels. The **Bytes per Row** setting SHALL support 8, 16, and 32; synchronization must remain valid after changing either setting. Compare navigation and difference highlighting from the main specification remain applicable.

### CMP-ADD-003 — Double-click exception bug fix

**Known defect:** Double-clicking a HEX row in **Compare** mode currently triggers an unhandled exception.

Required behavior:
- Double-clicking a valid, data-bearing row or byte SHALL open the appropriate edit dialog (see Section 4), **if editing that side is allowed**.
- The left/base image is editable in Compare; the right/comparison image is a read-only reference. Double-clicking the right side SHALL show a short explanation in the status bar without opening a modal dialog; it must never crash.
- Double-clicking an address header, separator, empty area, virtualized/unloaded row, or absent-data cell SHALL be safely handled.
- Any unexpected failure SHALL be caught at the command/UI boundary, logged with sufficient context, and reported without terminating the process.
- The underlying defect SHALL be diagnosed and corrected; global exception handling is not a substitute for fixing event routing, null selection, stale indexes or virtualization-related issues.

## 4. Byte editing and segment relocation

### EDT-ADD-001 — Double-click row to edit bytes

Double-clicking a data row in **Editor** mode SHALL open a dedicated **Edit Row** modal dialog (the requested editing workflow is a dialog launched directly from the row, rather than editing text in the main grid cell).

The dialog SHALL:
- Display the row's absolute start address and the currently selected byte address, if applicable.
- Present the bytes in **HEX** and their corresponding **ASCII** characters, with direct editing of individual bytes or printable ASCII characters.
- Keep HEX and ASCII representations synchronized during editing.
- Validate input: HEX bytes must be two valid hexadecimal digits and may represent any value from `0x00` to `0xFF`. Direct ASCII character editing accepts printable characters `0x20`–`0x7E`; nonprintable bytes are shown as dots and are changed through HEX input.
- Provide **Apply/OK** and **Cancel**; Cancel must not mutate the image.
- Commit the modification as one atomic Undo/Redo action, update the memory model, mark the image modified, refresh the viewers, and invalidate/recompute affected comparison results and any displayed calculated checksums.
- Correctly handle partial rows, sparse memory regions and bytes missing from the image. It must not implicitly fill holes unless the user explicitly requests an Insert/Fill action.

Recommended usability: highlight changed bytes, show original and proposed values, support keyboard navigation and prevent invalid commits.

### EDT-ADD-002 — Editing from Compare mode

Editing from Compare SHALL target only the left/base image and identify its filename in the dialog. The right/comparison image SHALL be read-only, with its edit action disabled and double-click handled safely. After left-side edits, the comparison result SHALL refresh.

### EDT-ADD-003 — Relocate selected segment dialog

In **Editor** mode, selecting/clicking a memory block in the segment list SHALL make it the active segment. The **Relocate** action (available from the Edit menu and preferably the segment context menu) SHALL open a modal popup containing:

- Selected segment's current start address, end address and size (read-only).
- Editable **New Start Address** field, accepting hexadecimal absolute addresses.
- Optional calculated **New End Address** preview.
- **Relocate** and **Cancel** actions.

Validation SHALL reject malformed addresses, arithmetic overflow, and out-of-range addresses for the selected export format. A collision with occupied destination bytes SHALL block relocation by default; overwriting those bytes requires a separate, explicit confirmation before any data changes. On confirmation, relocate the entire selected segment while retaining its bytes and length, refresh segment list and editor, update the dirty state, and support Undo/Redo. On cancellation or validation failure, keep the image unchanged.

**Interaction clarification:** A single click selects a segment; it should **not automatically relocate** it. The dialog opens when the user explicitly invokes **Relocate** on the selected segment (menu/context menu). This prevents accidental modification while navigating blocks.

## 5. Export behavior

### EXP-ADD-001 — Ask record byte count at export time

Each export operation SHALL present a dialog where the user can select the **maximum data bytes per output record/line**, with these choices: **8, 16, or 32 bytes**.

The dialog SHALL also show the output format (**Intel HEX** or **Motorola S-Record**), target path and export scope (entire image, selected segments, or selected address range). The initial default is 16 bytes per record; remember the most recently selected size across application restarts, without bypassing the user's ability to change it for the current export.

The record-size choice controls file serialization only; it SHALL NOT change bytes-per-row in the Editor/Compare UI or modify the in-memory image. Export Image writes a copy and SHALL NOT change the active image's path or saved/modified state; Save As changes the active file association. Generate valid address-extension, checksum and termination records as required by the chosen format, with robust error handling.

## 6. Packaging and deployment

### DEP-ADD-001 — Single-file executable

Release builds SHALL be published as **one Windows x64 `.exe` file** that users can run without additional application `.dll` files alongside it.

Recommended publish settings (adjust runtime identifier and framework version to the project):

```xml
<PropertyGroup>
  <RuntimeIdentifier>win-x64</RuntimeIdentifier>
  <SelfContained>true</SelfContained>
  <PublishSingleFile>true</PublishSingleFile>
  <IncludeNativeLibrariesForSelfExtract>true</IncludeNativeLibrariesForSelfExtract>
</PropertyGroup>
```

Example:

```powershell
dotnet publish .\HexEditor.UI\HexEditor.UI.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true
```

Acceptance criteria:
- Distribute and launch the publish output using the `.exe` alone, with no required application DLLs placed beside it.
- Test on a clean compatible Windows machine without a separately installed .NET desktop runtime.
- Embedded/native components may be extracted to an operating-system temporary location internally; **single-file distribution does not mean no runtime extraction**.
- Verify whether project-specific configuration files, logging destinations or application resources are required; embed defaults or create user-data files at runtime rather than depending on adjacent distribution files.
- Do not enable trimming without verifying WPF and WPF UI compatibility; trimming is not required for this objective.

## 7. Cross-cutting requirements

### NFR-ADD-001 — Error handling and diagnostics

UI event handlers and commands SHALL validate selection and address state before acting. Recoverable errors SHALL produce readable notifications and structured logs (operation, active mode, selected segment/row/address, exception type and stack trace), without crashing the app. Avoid logging sensitive data buffers unnecessarily.

### NFR-ADD-002 — Undo/Redo and data integrity

Each edit, fill, deletion, paste or segment-relocation transaction SHALL preserve undo/redo semantics and not partially update the model on validation failure. All views of one base image SHALL share its Undo/Redo history. Save and export must not mutate the image. When Compare is active, Save and Save As SHALL target its left/base image; the right reference SHALL remain unchanged. A tab's close button SHALL close only that view and retain its base image if another view uses it. File > Close Image SHALL close the active image and every view of that image, with a single unsaved-change prompt if needed. Closing a view of a modified image SHALL otherwise prompt only when it is the image's last view.

### NFR-ADD-003 — Responsive UI

The editor and compare views SHALL remain responsive for large, sparse memory images. Use data virtualization for viewers and non-blocking long-running import/export/compare operations. Do not generate rows for unallocated address gaps.

## 8. Acceptance test cases

| ID | Scenario | Expected outcome |
|---|---|---|
| AT-ADD-001 | Open `A.hex` in Editor; click Compare | File picker requests only comparison file `B.*`; `A.hex` remains base |
| AT-ADD-002 | Edit `A.hex` without saving; then compare with `B.s19` | Compare uses unsaved in-memory `A` bytes |
| AT-ADD-003 | Cancel selecting comparison file | Current image and editor remain unchanged |
| AT-ADD-004 | Show/hide ASCII in Compare | Both ASCII columns toggle; address/HEX data stay aligned |
| AT-ADD-005 | Switch Compare bytes/row among 8/16/32 | Both panels align on the same absolute addresses |
| AT-ADD-006 | Double-click valid HEX row in Editor | Edit Row dialog opens with correct address and values |
| AT-ADD-007 | Change HEX byte, then ASCII byte, and confirm | Correct byte modifications applied; Undo restores prior data |
| AT-ADD-008 | Cancel Edit Row dialog | No byte changes or dirty-state changes |
| AT-ADD-009 | Double-click row, absent byte and blank area in Compare | No unhandled exception; edit dialog or safe no-op per permissions |
| AT-ADD-010 | Select segment; invoke Relocate; enter valid new address | Whole segment moved, list/view refreshed, Undo works |
| AT-ADD-011 | Relocate into an occupied address range | Operation blocked by default; no data changes unless overwriting is separately and explicitly confirmed |
| AT-ADD-012 | Export using 8, 16, 32 bytes per record | Each file imports correctly; generated data-record lengths obey selection |
| AT-ADD-013 | Inspect main window on launch | Persistent toolbar contains only File, Compare/Merge, Undo/Redo, View, Navigate |
| AT-ADD-014 | Use Edit, Relocate, Checksum, Selection/Range, Export | Commands accessible from menu/context menus, not persistent large panels |
| AT-ADD-015 | Switch Windows light/dark appearance | WPF UI styling is consistent, labels/HEX data remain readable |
| AT-ADD-016 | Copy only released `.exe` to clean Windows PC | App launches; no additional application DLLs needed beside `.exe` |
| AT-ADD-017 | Edit one base image shown in Editor and multiple Compare tabs | All views update, with one shared Undo/Redo history |
| AT-ADD-018 | Save while a Compare tab is active | Left/base image is saved; right reference remains read-only |
| AT-ADD-019 | Close one of several views of a modified base image | No data lost; unsaved-change prompt appears when closing the last view |
| AT-ADD-020 | Invoke Compare from a Compare tab | New tab shares its left/base image and requests only a new reference file |
| AT-ADD-021 | Cancel reference-file selection after choosing a base file with no prior open image | Base file remains open in Editor; no Compare tab is created |
| AT-ADD-022 | File > Close Image while several tabs show a modified base image | All its views close after one unsaved-change decision; unrelated images remain open |
| AT-ADD-023 | Set different bytes-per-row and ASCII visibility in two tabs, then switch tabs | Each tab retains its values; main toolbar and Compare workspace controls reflect the active tab |
| AT-ADD-024 | Narrow the main window until five toolbar groups no longer fit | Excess commands remain reachable through the More overflow menu; workspace does not scroll horizontally because of the toolbar |
| AT-ADD-025 | Choose a manual light/dark theme, restart, then restore automatic theme | Override persists across restart; automatic mode follows the Windows theme |
| AT-ADD-026 | Export with 8 or 32 bytes per record, then restart and export again | Last choice is offered by default and can be changed; a fresh installation defaults to 16 |

## 9. Suggested implementation priority

1. **P0:** Fix Compare double-click crash; ensure safe mouse/selection handling.
2. **P1:** Compare-with-current-image workflow; Edit Row dialog; segment Relocate dialog.
3. **P1:** Compact toolbar and menu migration; WPF UI styling.
4. **P1:** Export dialog with 8/16/32 record-size selector.
5. **P1:** Self-contained, single-file Windows release validation.
6. **P2:** Keyboard/navigation refinements, visual polish and additional regression tests.

## 10. Explicit decisions and assumptions

- **Toolbar layout:** The five primary groups in UI-ADD-002 remain visible as a compact toolbar. Selection/Range, Edit, Relocate, Checksum, and Export are removed from the permanent toolbar and remain available through menus and task-specific dialogs. The entire toolbar does not collapse as one unit.
- **Visual reference:** The supplied screenshot is the layout reference for the Editor and Compare workspace, including tabs, memory-block lists, HEX panels, and comparison result. Apply the WPF UI visual system to that structure; exact icon and color matching is not required.
- **Scope:** This supplementary document defines the scope of the next implementation, with the screenshot clarifying its layout.
- **Workspace settings:** Preserve the Compare workspace controls shown in the reference. The persistent View and Navigate controls act on the active tab; settings shown in both places stay synchronized.
- **Compare permissions:** Only the left/base image is editable in Compare. The right/comparison image is a read-only reference.
- **Range and operation parameters:** Select Range opens a Start/End dialog. Edit, Fill, and Checksum obtain their operation-specific inputs from dialogs rather than hidden permanent panels.
- **Export versus Save As:** Export Image writes a copy without changing the active image path or saved/modified state. Save As changes the active file association.
- **ASCII editing:** Direct character editing accepts printable ASCII bytes `0x20`-`0x7E`. All byte values `0x00`-`0xFF` remain editable in HEX; nonprintable values appear as dots in the ASCII presentation.
- **Relocation overlap:** A collision blocks relocation by default. Overwriting an occupied destination requires a separate explicit confirmation.
- **Shared base image:** Starting Compare without an open image also opens its first/base file in Editor. Editor and Compare views of that image share in-memory edits. Opening a different Editor file leaves existing Compare tabs and their base-image changes intact.
- **Editor arrangement:** The Editor shows its memory-block list above a full-width HEX table, using the same general panel structure as the Compare reference.
- **Narrow windows:** Toolbar commands that do not fit move into a "More" overflow menu.
- **Appearance:** Follow the Windows theme by default; persist an optional light/dark override across restarts.
- **Release architecture:** A Windows x64 single-file executable is sufficient.
- **Saving and closing shared images:** Save/Save As in Compare targets its left/base image. Closing one of several views of a modified base image retains its data; the unsaved-change prompt appears when closing the last view. Multiple Compare tabs and Editor share one Undo/Redo history for the same base image.
- **Per-tab viewing:** Bytes per row and Show ASCII belong to each tab. The main toolbar reflects the active tab.
- **Read-only feedback:** Double-clicking the right Compare panel shows a brief status-bar explanation.
- **Export record-size default:** Start at 16 bytes and remember the last 8/16/32-byte choice across restarts.
- **Compare from Compare:** Open a new Compare tab sharing its left/base image, and choose only a new right/reference file.
- **Canceled two-file flow:** If the reference-file picker is canceled, keep the already selected base file open in Editor.
- **Close semantics:** A tab close button closes one view. File > Close Image closes the active image in all its views and prompts once for unsaved changes.
- **Editing interaction:** Although described as “in-place,” the requested design is a dedicated edit dialog launched by double-clicking a row. Direct typing into the main grid is not required by this supplement.
- **Relocation:** Selection is by clicking a block; the popup is opened by the Relocate command, not automatically on every click.
- **Export line size:** “Bytes per line” means maximum **data bytes per Intel HEX / S-Record record**, not characters in a textual line.
- **Single-file release:** A single distributed executable is required; internal .NET/native extraction is acceptable.
