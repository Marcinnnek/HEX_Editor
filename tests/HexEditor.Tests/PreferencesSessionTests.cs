using HexEditor.UI;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Threading;
using System.Windows.Media;

namespace HexEditor.Tests;

[TestClass]
public sealed class PreferencesSessionTests
{
    [TestMethod]
    public void Preferences_radio_indicator_is_visible_in_both_appearances()
    {
        var failure = RunOnSta(() =>
        {
            foreach (var theme in new[] { AppearanceMode.Light, AppearanceMode.Dark })
            {
                var unique = Guid.NewGuid().ToString("N");
                using var appearance = new AppearanceSession(new AppearanceSettings(
                    Path.Combine(Path.GetTempPath(), unique + "-appearance.json")), new FixedWindowsAppearance());
                var window = new PreferencesWindow(new PreferencesSession(appearance,
                    new GridSpacingSettings(Path.Combine(Path.GetTempPath(), unique + "-spacing.json"))))
                    { ShowInTaskbar = false };
                ApplicationAppearance.ApplyToWindow(window, theme);
                window.Show();
                window.UpdateLayout();
                var selected = Children<RadioButton>(window).Single(radio => radio.IsChecked == true);
                selected.ApplyTemplate();
                var indicator = selected.Template.FindName("PART_Indicator", selected) as FrameworkElement;
                Assert.IsNotNull(indicator);
                Assert.AreEqual(Visibility.Visible, indicator.Visibility,
                    $"The selected radio indicator should be visible in {theme}.");
                window.Close();
            }
        });
        if (failure is not null) throw failure;
    }

    private static Exception? RunOnSta(Action action)
    {
        Exception? failure = null;
        var thread = new Thread(() => { try { action(); } catch (Exception exception) { failure = exception; } });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
        return failure;
    }

    [TestMethod]
    public void Open_and_reopened_preferences_follow_the_effective_theme_across_repeated_changes()
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try
            {
                var folder = Path.Combine(Path.GetTempPath(), "hex-editor-theme-cycle-" + Guid.NewGuid());
                try
                {
                    var windows = new MutableWindowsAppearance();
                    var appearancePath = Path.Combine(folder, "appearance.json");
                    using var appearance = new AppearanceSession(
                        new AppearanceSettings(appearancePath), windows);
                    var choices = new PreferencesSession(appearance,
                        new GridSpacingSettings(Path.Combine(folder, "spacing.json")));
                    var alreadyOpen = new PreferencesWindow(choices)
                    {
                        Left = -3000, Top = -3000, ShowInTaskbar = false, ShowActivated = false
                    };
                    appearance.ThemeChanged += theme => ApplicationAppearance.ApplyToWindow(alreadyOpen, theme);
                    ApplicationAppearance.ApplyToWindow(alreadyOpen, appearance.EffectiveTheme);
                    alreadyOpen.Show();
                    alreadyOpen.UpdateLayout();

                    appearance.SetMode(AppearanceMode.Dark);
                    Assert.AreEqual(Color.FromRgb(0x17, 0x19, 0x1D), ((SolidColorBrush)alreadyOpen.Background).Color);
                    Assert.AreEqual(Color.FromRgb(0xF2, 0xF4, 0xF7), ((SolidColorBrush)Children<RadioButton>(alreadyOpen).First().Foreground).Color);
                    Assert.AreEqual(Color.FromRgb(0xF2, 0xF4, 0xF7), ((SolidColorBrush)Children<Slider>(alreadyOpen).First().Foreground).Color);
                    appearance.SetMode(AppearanceMode.Light);
                    Assert.AreEqual(Color.FromRgb(0xF8, 0xF9, 0xFB), ((SolidColorBrush)alreadyOpen.Background).Color);
                    appearance.SetMode(AppearanceMode.Dark);

                    var reopened = new PreferencesWindow(new PreferencesSession(appearance,
                        new GridSpacingSettings(Path.Combine(folder, "spacing.json"))))
                    {
                        Left = -3000, Top = -3000, ShowInTaskbar = false, ShowActivated = false
                    };
                    ApplicationAppearance.ApplyToWindow(reopened, appearance.EffectiveTheme);
                    reopened.Show();
                    reopened.UpdateLayout();
                    Assert.AreEqual(AppearanceMode.Dark, appearance.EffectiveTheme);
                    Assert.AreEqual(AppearanceMode.Dark, appearance.Mode);
                    Assert.AreEqual(AppearanceMode.Dark, new AppearanceSettings(appearancePath).Mode);
                    Assert.AreEqual(Color.FromRgb(0x17, 0x19, 0x1D), ((SolidColorBrush)reopened.Resources["HexWindowBrush"]).Color);
                    Assert.AreEqual(Color.FromRgb(0x17, 0x19, 0x1D), ((SolidColorBrush)reopened.Background).Color);

                    appearance.SetMode(AppearanceMode.Automatic);
                    windows.ChangeTo(true);
                    ApplicationAppearance.ApplyToWindow(reopened, appearance.EffectiveTheme);
                    windows.ChangeTo(false);
                    ApplicationAppearance.ApplyToWindow(reopened, appearance.EffectiveTheme);
                    Assert.AreEqual(Color.FromRgb(0xF8, 0xF9, 0xFB), ((SolidColorBrush)alreadyOpen.Background).Color);
                    Assert.AreEqual(Color.FromRgb(0x1B, 0x1D, 0x21), ((SolidColorBrush)Children<RadioButton>(alreadyOpen).First().Foreground).Color);
                    Assert.AreEqual(AppearanceMode.Automatic, new AppearanceSettings(appearancePath).Mode);
                    alreadyOpen.Close();
                    reopened.Close();
                }
                finally { if (Directory.Exists(folder)) Directory.Delete(folder, true); }
            }
            catch (Exception exception) { failure = exception; }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
        if (failure is not null) throw failure;
    }

    [TestMethod]
    public void Preferences_are_staged_until_save_and_saved_choices_survive_restart()
    {
        var folder = Path.Combine(Path.GetTempPath(), "hex-editor-preferences-" + Guid.NewGuid());
        try
        {
            var appearancePath = Path.Combine(folder, "appearance.json");
            var spacingPath = Path.Combine(folder, "grid-spacing.json");
            var appearance = new AppearanceSettings(appearancePath);
            var spacing = new GridSpacingSettings(spacingPath);
            using var active = new AppearanceSession(appearance, new FixedWindowsAppearance());

            var cancelled = new PreferencesSession(active, spacing)
            {
                Appearance = AppearanceMode.Dark,
                HorizontalByteSpacing = 7,
                VerticalRowSpacing = 6
            };

            Assert.AreEqual(AppearanceMode.Automatic, active.Mode);
            Assert.AreEqual(AppearanceMode.Light, active.EffectiveTheme);
            Assert.AreEqual(2, spacing.HorizontalByteSpacing);
            Assert.AreEqual(1, spacing.VerticalRowSpacing);
            Assert.AreEqual(AppearanceMode.Automatic, new AppearanceSettings(appearancePath).Mode);
            Assert.AreEqual(2, new GridSpacingSettings(spacingPath).HorizontalByteSpacing);

            var saved = new PreferencesSession(active, spacing);
            Assert.AreEqual(AppearanceMode.Automatic, saved.Appearance);
            Assert.AreEqual(2, saved.HorizontalByteSpacing);
            Assert.AreEqual(1, saved.VerticalRowSpacing);
            saved.Appearance = AppearanceMode.Dark;
            saved.HorizontalByteSpacing = 5;
            saved.VerticalRowSpacing = 4;
            saved.Save();

            Assert.AreEqual(AppearanceMode.Dark, active.EffectiveTheme);
            Assert.AreEqual(5, spacing.HorizontalByteSpacing);
            Assert.AreEqual(4, spacing.VerticalRowSpacing);
            Assert.AreEqual(AppearanceMode.Dark, new AppearanceSettings(appearancePath).Mode);
            Assert.AreEqual(5, new GridSpacingSettings(spacingPath).HorizontalByteSpacing);
            Assert.AreEqual(4, new GridSpacingSettings(spacingPath).VerticalRowSpacing);
        }
        finally
        {
            if (Directory.Exists(folder)) Directory.Delete(folder, true);
        }
    }

    private sealed class FixedWindowsAppearance : IWindowsAppearanceSource
    {
        public bool IsDark => false;
        public event Action? Changed { add { } remove { } }
    }

    private sealed class MutableWindowsAppearance : IWindowsAppearanceSource
    {
        public bool IsDark { get; private set; }
        public event Action? Changed;
        public void ChangeTo(bool dark) { IsDark = dark; Changed?.Invoke(); }
    }

    [TestMethod]
    public void Failed_save_leaves_active_and_persisted_preferences_unchanged()
    {
        var folder = Path.Combine(Path.GetTempPath(), "hex-editor-preferences-" + Guid.NewGuid());
        try
        {
            Directory.CreateDirectory(folder);
            var spacingPath = Path.Combine(folder, "grid-spacing.json");
            var appearancePath = Path.Combine(folder, "appearance.json");
            var spacing = new GridSpacingSettings(spacingPath);
            spacing.SetSpacing(3, 2);
            Directory.CreateDirectory(appearancePath); // A destination that rejects the second commit.
            using var appearance = new AppearanceSession(new AppearanceSettings(appearancePath),
                new FixedWindowsAppearance());
            var choices = new PreferencesSession(appearance, spacing)
            {
                Appearance = AppearanceMode.Dark,
                HorizontalByteSpacing = 6,
                VerticalRowSpacing = 4
            };

            Assert.ThrowsExactly<UnauthorizedAccessException>(choices.Save);

            Assert.AreEqual(AppearanceMode.Automatic, appearance.Mode);
            Assert.AreEqual(AppearanceMode.Light, appearance.EffectiveTheme);
            Assert.AreEqual(3, spacing.HorizontalByteSpacing);
            Assert.AreEqual(2, spacing.VerticalRowSpacing);
            Assert.AreEqual(3, new GridSpacingSettings(spacingPath).HorizontalByteSpacing);
            Assert.AreEqual(2, new GridSpacingSettings(spacingPath).VerticalRowSpacing);
            Assert.AreEqual(AppearanceMode.Automatic, new AppearanceSettings(appearancePath).Mode);
        }
        finally
        {
            if (Directory.Exists(folder)) Directory.Delete(folder, true);
        }
    }

    [TestMethod]
    public void Preferences_window_shows_current_choices_and_native_close_control()
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try
            {
                var folder = Path.Combine(Path.GetTempPath(), "hex-editor-preferences-" + Guid.NewGuid());
                using var appearance = new AppearanceSession(
                    new AppearanceSettings(Path.Combine(folder, "appearance.json")),
                    new FixedWindowsAppearance());
                var choices = new PreferencesSession(appearance,
                    new GridSpacingSettings(Path.Combine(folder, "grid-spacing.json")));
                var window = new PreferencesWindow(choices);
                Assert.AreEqual(WindowStyle.SingleBorderWindow, window.WindowStyle);
                Assert.AreEqual(ResizeMode.NoResize, window.ResizeMode);
                var radios = Children<RadioButton>(window).ToArray();
                CollectionAssert.AreEquivalent(new[] { "Follow Windows", "Light", "Dark" },
                    radios.Select(radio => radio.Content?.ToString()).ToArray());
                Assert.AreEqual("Follow Windows", radios.Single(radio => radio.IsChecked == true).Content);
                CollectionAssert.AreEquivalent(new[] { 2d, 1d },
                    Children<Slider>(window).Select(slider => slider.Value).ToArray());
                CollectionAssert.AreEquivalent(new[] { "Cancel", "Save" },
                    Children<Wpf.Ui.Controls.Button>(window)
                        .Select(button => button.Content?.ToString()).ToArray());
            }
            catch (Exception exception) { failure = exception; }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
        if (failure is not null) throw failure;
    }

    private static IEnumerable<T> Children<T>(DependencyObject parent) where T : DependencyObject
    {
        foreach (var child in LogicalTreeHelper.GetChildren(parent).OfType<DependencyObject>())
        {
            if (child is T match) yield return match;
            foreach (var descendant in Children<T>(child)) yield return descendant;
        }
    }

    [TestMethod]
    public void Preferences_dialog_cancel_and_close_discard_changes_while_save_stages_them()
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try
            {
                var folder = Path.Combine(Path.GetTempPath(), "hex-editor-preferences-" + Guid.NewGuid());
                try
                {
                    var appearancePath = Path.Combine(folder, "appearance.json");
                    var spacingPath = Path.Combine(folder, "grid-spacing.json");
                    var spacing = new GridSpacingSettings(spacingPath);
                    using var appearance = new AppearanceSession(new AppearanceSettings(appearancePath),
                        new FixedWindowsAppearance());

                    Assert.IsFalse(ShowOffscreen(new PreferencesSession(appearance, spacing), "Cancel").GetValueOrDefault());
                    Assert.IsFalse(ShowOffscreen(new PreferencesSession(appearance, spacing), "Close").GetValueOrDefault());
                    Assert.AreEqual(AppearanceMode.Automatic, appearance.Mode);
                    Assert.AreEqual(2, spacing.HorizontalByteSpacing);
                    Assert.AreEqual(1, spacing.VerticalRowSpacing);
                    Assert.AreEqual(AppearanceMode.Automatic, new AppearanceSettings(appearancePath).Mode);

                    var saved = new PreferencesSession(appearance, spacing);
                    Assert.IsTrue(ShowOffscreen(saved, "Save").GetValueOrDefault());
                    Assert.AreEqual(AppearanceMode.Automatic, appearance.Mode);
                    Assert.AreEqual(2, spacing.HorizontalByteSpacing);
                    saved.Save();
                    Assert.AreEqual(AppearanceMode.Dark, new AppearanceSettings(appearancePath).Mode);
                    Assert.AreEqual(6, new GridSpacingSettings(spacingPath).HorizontalByteSpacing);
                    Assert.AreEqual(3, new GridSpacingSettings(spacingPath).VerticalRowSpacing);
                }
                finally { if (Directory.Exists(folder)) Directory.Delete(folder, true); }
            }
            catch (Exception exception) { failure = exception; }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
        if (failure is not null) throw failure;
    }

    private static bool? ShowOffscreen(PreferencesSession choices, string action)
    {
        var window = new PreferencesWindow(choices)
        {
            Left = -3000, Top = -3000, WindowStartupLocation = WindowStartupLocation.Manual,
            ShowActivated = false, ShowInTaskbar = false
        };
        window.Dispatcher.BeginInvoke(DispatcherPriority.Loaded, () =>
        {
            Children<RadioButton>(window).Single(radio => Equals(radio.Content, "Dark")).IsChecked = true;
            var sliders = Children<Slider>(window).ToArray();
            sliders[0].Value = 6;
            sliders[1].Value = 3;
            if (action == "Close") window.Close();
            else Children<Wpf.Ui.Controls.Button>(window)
                .Single(button => Equals(button.Content, action))
                .RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
        });
        return window.ShowDialog();
    }
}
