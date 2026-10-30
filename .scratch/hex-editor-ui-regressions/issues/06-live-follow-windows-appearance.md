# 06: Make Follow Windows react without restart

**What to build:** Follow Windows renders the app in the current operating-system appearance and switches all open views when Windows changes between light and dark during the session.

**Blocked by:** 04: Render a readable Dark appearance throughout the app.

**Status:** ready-for-agent

- [ ] Choosing Follow Windows applies the current Windows light or dark setting across the main window, Editor, Compare, menus, status, and dialogs.
- [ ] Changing the Windows appearance while the app is open updates already visible content without restarting.
- [ ] A manual Light or Dark override prevents subsequent Windows changes from replacing the selected mode.
- [ ] Returning to Follow Windows resumes live operating-system tracking, and the chosen mode persists across restart.
- [ ] Repeated mode changes do not duplicate event subscriptions or leave stale colors in custom HEX/ASCII rendering.
- [ ] Verify observable appearance-setting behavior and inspect live operating-system theme changes in WPF.
