Status: ready-for-agent

# Self-contained Windows x64 release

## Problem Statement

Firmware engineers need to run HEX Editor on a supported Windows computer without installing a separate .NET Desktop Runtime or copying application DLLs, resources, or configuration files alongside the program. A release that depends on files beside the executable is easy to break when copied, and settings or diagnostics stored beside it may fail in read-only or isolated locations.

The release must preserve the completed Editor and Compare workflows, including opening, comparing, editing, saving, exporting, and closing memory images. Users also need appearance and export preferences to work on first launch, and diagnostic information to help investigate errors without recording firmware contents or requiring the executable's directory to be writable.

## Solution

Publish a self-contained Windows x64 executable for Windows 11 version 24H2 or newer. The local Release output contains only the executable. Users can copy that file into an isolated directory and launch it without adjacent application files. Required managed application files and supported native components are included; trimming remains disabled unless compatibility is proven separately.

Keep application preferences and bounded diagnostic logs in the user's local application data. First launch supplies defaults: automatic Windows appearance and 16 data bytes per export record. Persist the user's appearance override and most recent export record size. Write an error-only rolling diagnostic log capped at five 1 MiB files. Include the operation and affected address or selection when available; omit firmware byte contents, full file paths, routine successes, and telemetry.

Validate the published executable on the agreed Windows 11 24H2+ x64 development system by copying only the executable and exercising open, compare, edit, save, export, and close. Record the installed .NET Desktop Runtime environment. This host has .NET Desktop Runtimes installed, so the check is not evidence that the executable runs on a machine without those runtimes. The product must remain self-contained even though clean-machine runtime independence is not proven by this acceptance environment.

## User Stories

1. As a firmware engineer, I want one Windows x64 executable, so that I can start HEX Editor without installing a separate .NET Desktop Runtime.
2. As a firmware engineer, I want to copy only the executable into an isolated directory and launch it, so that deployment does not depend on adjacent application DLLs or resources.
3. As a firmware engineer, I want the executable to include the managed application files and supported native components it needs, so that the published program can run as a self-contained application.
4. As a firmware engineer, I want the release to target Windows 11 version 24H2 or newer on x64, so that the supported operating system and architecture are clear.
5. As a firmware engineer, I want the release to preserve Editor and Compare behavior, so that deployment does not remove the workflows I use to inspect firmware.
6. As a firmware engineer, I want to open a memory image in the published application, so that I can inspect firmware after copying the executable.
7. As a firmware engineer, I want to compare a base image with a reference image in the published application, so that I can review differences without installing a runtime.
8. As a firmware engineer, I want to edit a base image, save it, and close it in the published application, so that I can complete my normal editing workflow.
9. As a firmware engineer, I want to export a memory image from the published application, so that I can produce a HEX or S-Record copy for downstream tools.
10. As a firmware engineer, I want export to default to 16 data bytes per record and remember my most recent choice among 8, 16, and 32, so that repeated exports retain my preference.
11. As a firmware engineer, I want theme and export preferences stored in my user data, so that they persist across launches without writing beside the executable.
12. As a firmware engineer, I want automatic Windows appearance as the first-launch default, so that the application follows my desktop until I choose an override.
13. As a firmware engineer, I want application errors recorded in a local diagnostic log, so that I can investigate failures after they occur.
14. As a firmware engineer, I want diagnostic entries to identify the operation and affected address or selection when available, so that an error has useful context.
15. As a firmware engineer, I want diagnostic logs to omit firmware bytes and full file paths, so that the log does not expose firmware contents or unnecessary path information.
16. As a firmware engineer, I want routine successful operations and telemetry omitted from diagnostics, so that logging remains focused on errors and does not report usage activity.
17. As a firmware engineer, I want diagnostic logs limited to five files of at most 1 MiB each, so that they cannot grow without bound.
18. As a firmware engineer, I want preferences and diagnostics stored under local application data, so that the executable can run from a read-only or isolated directory.
19. As a maintainer, I want the Release publish directory to contain only the executable, so that the local artifact is straightforward to copy and review.
20. As a maintainer, I want trimming disabled unless compatibility is established, so that publishing does not remove code required by WPF or application workflows.
21. As a maintainer, I want validation to exercise the published executable rather than only a development build, so that packaging issues are visible.
22. As a maintainer, I want validation to cover open, compare, edit, save, export, and close, so that the main workflows are checked in the release.
23. As a maintainer, I want the validation environment and installed .NET Desktop Runtime versions recorded, so that reviewers can interpret what the launch check demonstrates.
24. As a maintainer, I want the validation record to state that a runtime-installed host does not prove clean-machine runtime independence, so that the evidence is not overstated.
25. As a firmware engineer, I want a local publish artifact rather than automatic GitHub Release publication, so that producing the executable does not publish it externally.

## Implementation Decisions

- Preserve the existing Windows-only .NET 10 WPF and MVVM architecture established by the desktop architecture decision.
- Produce a self-contained, single-file Release executable for Windows x64. Include required native components for self-extraction, allow runtime extraction to a temporary location, and do not enable trimming without compatibility evidence.
- Accept the current executable size of approximately 64 MiB; runtime independence takes priority, with no smaller size limit.
- Keep the local publish artifact unsigned. Code signing and a public distribution channel are outside this release scope.
- Store appearance and export preferences under the current user's local application data. A new profile uses automatic appearance and a 16-byte export record size; subsequent export choices among 8, 16, and 32 bytes persist across restarts.
- Store an error-only rolling diagnostic log under the user's local application data, with a maximum of five 1 MiB files. Include operation and affected address or selection where available. Do not record firmware byte contents, full file paths, routine successes, or telemetry.
- Keep first-launch behavior independent of files beside the executable. Missing or unreadable preferences use defaults, and inability to write optional diagnostic data must not prevent the application from launching or using memory-image workflows.
- Write the Release output to a local directory that contains only the executable. No installer, GitHub Release automation, or other operating-system/architecture target is required.
- Use the published executable boundary as the primary release acceptance seam: run the executable copied alone into an isolated directory, exercise the public user workflows, and inspect the resulting exported files and user-data preferences/diagnostics.
- Retain public workspace-operation tests as prior art for open, compare, edit, save, export, and close behavior. Release acceptance focuses on packaging and the behavior of the published executable, not private event handlers or implementation details.
- Run acceptance on the agreed Windows 11 24H2+ x64 development system. Record its operating-system build and installed .NET Desktop Runtime environment. The user accepts that this host contains .NET Desktop Runtimes 8, 9, and 10; this acceptance does not prove runtime independence on a clean machine.

## Testing Decisions

- Exercise behavior at the highest practical seam: launch the published executable copied alone into an isolated directory and perform workflows through the application. Verify the selected export by reading or reimporting the generated memory image, and inspect user-data preferences and diagnostics after use.
- Verify the publish output contains only the executable and that startup does not depend on application DLLs, resources, configuration, or writable files beside it.
- Verify a first launch supplies automatic appearance and a 16-byte export record default without adjacent configuration files. Verify changed appearance and export preferences persist after restart.
- Cause representative recoverable errors and verify that diagnostic entries include operation and available address/selection context, omit firmware contents and full paths, and rotate within five 1 MiB files.
- Exercise open, compare, edit, save, export, and close using representative memory images in the published application. Confirm exported data preserves occupied addresses and gaps and does not change the active document association or dirty state.
- Record the actual Windows build, architecture, installed .NET/Desktop Runtime versions, publish command, executable path, and executable size. Do not claim that a host with .NET Desktop Runtime installed proves launch without it.
- Existing workspace-level tests for document state and file workflows provide behavioral prior art. Avoid assertions tied to private UI event handlers, exact control names, or theme resource structure.

## Out of Scope

- Linux, macOS, Windows x86, Windows ARM64, and operating systems older than Windows 11 version 24H2.
- A clean virtual machine or another clean physical computer for this release acceptance. Testing is accepted on the agreed development system, with the installed-runtime limitation recorded.
- Claiming that the current validation demonstrates runtime independence on a machine without a separately installed .NET Desktop Runtime. The self-contained executable requirement remains; only the clean-machine proof is unavailable in this acceptance plan.
- An installer, automatic update mechanism, GitHub Release publication, code signing, or a public distribution service.
- Trimming the application or preventing supported native components from extracting to temporary storage at runtime.
- Redesign of the Editor, Compare, editing, save, or export workflows beyond what is required to exercise them in the published application.

## Further Notes

- This focused release spec records decisions from the ongoing Windows HEX Editor UI and workflow enhancement work and refines its single-file release requirement.
- Release workflow validation depends on the existing UI and responsiveness prerequisites, including tickets 15 and 16. Export preference behavior is covered by the existing export-options ticket.
- The currently validated development host reported Windows NT 10.0.26300.0 x64 and .NET / Windows Desktop runtimes 8, 9, and 10. The publish artifact has started from an isolated directory containing only the executable, but the full workflow acceptance remains outstanding.
