using System.Windows;
using Wpf.Ui.Appearance;
using WindowBackdropType = Wpf.Ui.Controls.WindowBackdropType;

namespace HexEditor.UI;

/// <summary>Applies the effective application appearance to WPF and Wpf.Ui windows.</summary>
public static class ApplicationAppearance
{
    static ApplicationAppearance() => EventManager.RegisterClassHandler(
        typeof(Window), FrameworkElement.LoadedEvent, new RoutedEventHandler(Window_Loaded), handledEventsToo: true);

    public static void Apply(AppearanceMode theme)
    {
        if (theme is not (AppearanceMode.Light or AppearanceMode.Dark))
            throw new ArgumentOutOfRangeException(nameof(theme), "An effective Light or Dark appearance is required.");

        var application = Application.Current;
        if (application is null || !application.Dispatcher.CheckAccess()) return;

        var dark = theme == AppearanceMode.Dark;
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
        if (theme is not (AppearanceMode.Light or AppearanceMode.Dark))
            throw new ArgumentOutOfRangeException(nameof(theme), "An effective Light or Dark appearance is required.");

        AppearancePalette.Apply(window.Resources, theme == AppearanceMode.Dark);
        BindWindow(window);
    }

    private static void Window_Loaded(object sender, RoutedEventArgs e)
    {
        if (sender is Window window) BindWindow(window);
    }

    private static void BindWindow(Window window)
    {
        window.SetResourceReference(Window.BackgroundProperty, "HexWindowBrush");
        window.SetResourceReference(Window.ForegroundProperty, "HexTextBrush");
    }
}
