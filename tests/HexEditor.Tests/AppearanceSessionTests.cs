using HexEditor.UI;

namespace HexEditor.Tests;

[TestClass]
public class AppearanceSessionTests
{
    [TestMethod]
    public void Follow_Windows_tracks_changes_and_manual_choice_stops_tracking_until_reselected()
    {
        var directory = Path.Combine(Path.GetTempPath(), "hex-editor-appearance-" + Guid.NewGuid());
        var path = Path.Combine(directory, "appearance.json");
        try
        {
            var windows = new TestWindowsAppearance();
            using var session = new AppearanceSession(new AppearanceSettings(path), windows);
            var shown = new List<AppearanceMode>();
            session.ThemeChanged += shown.Add;

            Assert.AreEqual(AppearanceMode.Light, session.EffectiveTheme);
            Assert.AreEqual(1, windows.SubscriberCount);

            windows.ChangeTo(true);
            Assert.AreEqual(AppearanceMode.Dark, session.EffectiveTheme);
            CollectionAssert.AreEqual(new[] { AppearanceMode.Dark }, shown);

            session.SetMode(AppearanceMode.Light);
            Assert.AreEqual(0, windows.SubscriberCount);
            windows.ChangeTo(false);
            windows.ChangeTo(true);
            Assert.AreEqual(AppearanceMode.Light, session.EffectiveTheme);
            CollectionAssert.AreEqual(new[] { AppearanceMode.Dark, AppearanceMode.Light }, shown);

            session.SetMode(AppearanceMode.Dark);
            windows.ChangeTo(false);
            Assert.AreEqual(AppearanceMode.Dark, session.EffectiveTheme);
            Assert.AreEqual(0, windows.SubscriberCount);

            session.SetMode(AppearanceMode.Automatic);
            session.SetMode(AppearanceMode.Automatic);
            Assert.AreEqual(1, windows.SubscriberCount);
            Assert.AreEqual(AppearanceMode.Light, session.EffectiveTheme);
            Assert.AreEqual(AppearanceMode.Automatic, new AppearanceSettings(path).Mode);

            windows.ChangeTo(true);
            Assert.AreEqual(AppearanceMode.Dark, session.EffectiveTheme);
            CollectionAssert.AreEqual(new[]
            {
                AppearanceMode.Dark, AppearanceMode.Light, AppearanceMode.Dark,
                AppearanceMode.Light, AppearanceMode.Dark
            }, shown);

            session.Dispose();
            Assert.AreEqual(0, windows.SubscriberCount);
        }
        finally
        {
            if (Directory.Exists(directory)) Directory.Delete(directory, true);
        }
    }

    private sealed class TestWindowsAppearance : IWindowsAppearanceSource
    {
        private Action? _changed;
        public bool IsDark { get; private set; }
        public int SubscriberCount => _changed?.GetInvocationList().Length ?? 0;
        public event Action? Changed
        {
            add => _changed += value;
            remove => _changed -= value;
        }

        public void ChangeTo(bool dark)
        {
            IsDark = dark;
            _changed?.Invoke();
        }
    }
}
