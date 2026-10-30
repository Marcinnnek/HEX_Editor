namespace HexEditor.UI;

/// <summary>Reports the current Windows app appearance when it changes.</summary>
public interface IWindowsAppearanceSource
{
    bool IsDark { get; }
    event Action? Changed;
}

/// <summary>Resolves the saved appearance choice for the current application session.</summary>
public sealed class AppearanceSession : IDisposable
{
    private readonly AppearanceSettings _settings;
    private readonly IWindowsAppearanceSource _windows;
    private bool _followingWindows;
    private bool _disposed;

    public AppearanceSession(AppearanceSettings settings, IWindowsAppearanceSource windows)
    {
        _settings = settings;
        _windows = windows;
        EffectiveTheme = _settings.GetEffectiveTheme(_windows.IsDark);
        UpdateSubscription();
    }

    public AppearanceMode Mode => _settings.Mode;
    public AppearanceMode EffectiveTheme { get; private set; }
    public event Action<AppearanceMode>? ThemeChanged;

    public void SetMode(AppearanceMode mode)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (_settings.Mode != mode) _settings.SetMode(mode);
        UpdateSubscription();
        Refresh();
    }

    public void Refresh()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        var theme = _settings.GetEffectiveTheme(_windows.IsDark);
        if (theme == EffectiveTheme) return;
        EffectiveTheme = theme;
        ThemeChanged?.Invoke(theme);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        if (_followingWindows) _windows.Changed -= Refresh;
        _followingWindows = false;
    }

    private void UpdateSubscription()
    {
        var follow = _settings.Mode == AppearanceMode.Automatic;
        if (follow == _followingWindows) return;
        if (follow) _windows.Changed += Refresh;
        else _windows.Changed -= Refresh;
        _followingWindows = follow;
    }
}
