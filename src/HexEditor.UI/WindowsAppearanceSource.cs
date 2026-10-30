using System.Windows;
using System.Windows.Interop;
using System.Security;
using Microsoft.Win32;
using Wpf.Ui.Appearance;

namespace HexEditor.UI;

/// <summary>Observes Windows appearance messages without changing the user's desktop settings.</summary>
internal sealed class WindowsAppearanceSource : IWindowsAppearanceSource, IDisposable
{
    private const int WmSettingChange = 0x001A;
    private const int WmThemeChanged = 0x031A;
    private readonly Window _window;
    private HwndSource? _source;

    internal WindowsAppearanceSource(Window window)
    {
        _window = window;
        _window.SourceInitialized += OnSourceInitialized;
        _window.Closed += OnClosed;
        Attach();
    }

    public bool IsDark
    {
        get
        {
            try
            {
                using var personalize = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
                if (personalize?.GetValue("AppsUseLightTheme") is int light) return light == 0;
            }
            catch (SecurityException) { }
            catch (UnauthorizedAccessException) { }
            return ApplicationThemeManager.GetSystemTheme() == SystemTheme.Dark;
        }
    }

    public event Action? Changed;

    public void Dispose()
    {
        _window.SourceInitialized -= OnSourceInitialized;
        _window.Closed -= OnClosed;
        _source?.RemoveHook(WndProc);
        _source = null;
    }

    private void OnSourceInitialized(object? sender, EventArgs e) => Attach();
    private void OnClosed(object? sender, EventArgs e) => Dispose();

    private void Attach()
    {
        if (_source is not null) return;
        _source = PresentationSource.FromVisual(_window) as HwndSource;
        _source?.AddHook(WndProc);
    }

    private IntPtr WndProc(IntPtr hwnd, int message, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (message is WmSettingChange or WmThemeChanged) Changed?.Invoke();
        return IntPtr.Zero;
    }
}
