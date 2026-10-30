# 08: Application icon

**What to build:** Use the supplied HEX Editor icon consistently in Windows and application-owned windows.

**Blocked by:** None (can start immediately).

**Status:** resolved

- [x] The Windows executable contains the supplied icon resource and displays it in Explorer and taskbar representations.
- [x] The main window and application-owned dialogs use the same icon in Light and Dark themes.
- [x] Existing window controls and dialog behavior remain unchanged while the icon is applied.
- [x] Build-artifact inspection and representative rendered WPF scenarios verify the icon; native file-picker chrome remains controlled by Windows.

## Answer

The supplied root HexEditor.ico is embedded as a WPF resource and configured as the Windows executable's application icon. ApplicationAppearance assigns the same icon to loaded WPF windows, including the main window and application-owned dialogs, independently of Light/Dark appearance. Native Windows picker chrome remains outside scope.

Verified with ApplicationIconAcceptanceTests rendering the main window and save dialog in Light and Dark themes. Published a temporary win-x64 single-file executable and successfully extracted its 32x32 associated icon with Windows System.Drawing.Icon.ExtractAssociatedIcon.
