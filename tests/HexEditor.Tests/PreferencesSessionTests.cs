using HexEditor.UI;
using System.Windows;
using System.Windows.Controls;

namespace HexEditor.Tests;

[TestClass]
public sealed class PreferencesSessionTests
{
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
}
