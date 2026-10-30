using System.Windows;
using System.Windows.Media.Imaging;
using System.Runtime.InteropServices;
using Wpf.Ui.Appearance;
using WindowBackdropType = Wpf.Ui.Controls.WindowBackdropType;

namespace HexEditor.UI;

/// <summary>Applies the effective application appearance to WPF and Wpf.Ui windows.</summary>
public static class ApplicationAppearance
{
    private static readonly Uri IconUri = new("pack://application:,,,/HexEditor.UI;component/HexEditor.ico", UriKind.Absolute);
    private static AppearanceMode _effectiveTheme = AppearanceMode.Light;

    static ApplicationAppearance() => EventManager.RegisterClassHandler(
        typeof(Window), FrameworkElement.LoadedEvent, new RoutedEventHandler(Window_Loaded), handledEventsToo: true);

    public static void Apply(AppearanceMode theme)
    {
        ValidateEffectiveTheme(theme);

        var application = Application.Current;
        if (application is null || !application.Dispatcher.CheckAccess()) return;

        var dark = theme == AppearanceMode.Dark;
        _effectiveTheme = theme;
        ApplicationThemeManager.Apply(dark ? ApplicationTheme.Dark : ApplicationTheme.Light, WindowBackdropType.None);
        AppearancePalette.Apply(dark);

        // Rebind existing windows explicitly. This keeps dialogs with local background values
        // following the application palette when Wpf.Ui replaces its merged theme dictionaries.
        foreach (Window window in application.Windows)
            BindWindow(window);
    }

    /// <summary>Binds a dialog's surface and text to its current appearance resources.</summary>
    public static void ApplyToWindow(Window window, AppearanceMode theme)
    {
        ArgumentNullException.ThrowIfNull(window);
        ValidateEffectiveTheme(theme);

        _effectiveTheme = theme;
        AppearancePalette.Apply(window.Resources, theme == AppearanceMode.Dark);
        BindWindow(window);
    }

    private static void ValidateEffectiveTheme(AppearanceMode theme)
    {
        if (theme is not (AppearanceMode.Light or AppearanceMode.Dark))
            throw new ArgumentOutOfRangeException(nameof(theme), "An effective Light or Dark appearance is required.");
    }

    private static void Window_Loaded(object sender, RoutedEventArgs e)
    {
        if (sender is Window window) BindWindow(window);
    }

    private static void BindWindow(Window window)
    {
        window.SourceInitialized -= Window_SourceInitialized;
        window.SourceInitialized += Window_SourceInitialized;
        window.SetResourceReference(Window.BackgroundProperty, "HexWindowBrush");
        window.SetResourceReference(Window.ForegroundProperty, "HexTextBrush");
        window.Icon = new BitmapImage(IconUri);
        ApplyTitleBar(window, _effectiveTheme == AppearanceMode.Dark);
    }

    private static void Window_SourceInitialized(object? sender, EventArgs e)
    {
        if (sender is Window window) ApplyTitleBar(window, _effectiveTheme == AppearanceMode.Dark);
    }

    private static void ApplyTitleBar(Window window, bool dark)
    {
        var handle = new System.Windows.Interop.WindowInteropHelper(window).Handle;
        if (handle == IntPtr.Zero) return;
        var value = dark ? 1 : 0;
        // Attribute 20 is supported by current Windows builds; 19 covers earlier builds.
        if (DwmSetWindowAttribute(handle, 20, ref value, sizeof(int)) < 0)
            _ = DwmSetWindowAttribute(handle, 19, ref value, sizeof(int));
    }

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);
}
