using HexEditor.UI;

namespace HexEditor.Tests;

[TestClass]
public class AppearanceSettingsTests
{
    [TestMethod]
    public void Appearance_choice_survives_restart_and_can_return_to_automatic()
    {
        var directory = Path.Combine(Path.GetTempPath(), "hex-editor-appearance-" + Guid.NewGuid());
        var path = Path.Combine(directory, "appearance.json");
        try
        {
            var firstRun = new AppearanceSettings(path);
            Assert.AreEqual(AppearanceMode.Automatic, firstRun.Mode);

            firstRun.SetMode(AppearanceMode.Dark);
            Assert.AreEqual(AppearanceMode.Dark, new AppearanceSettings(path).Mode);

            new AppearanceSettings(path).SetMode(AppearanceMode.Light);
            Assert.AreEqual(AppearanceMode.Light, new AppearanceSettings(path).Mode);

            new AppearanceSettings(path).SetMode(AppearanceMode.Automatic);
            Assert.AreEqual(AppearanceMode.Automatic, new AppearanceSettings(path).Mode);
        }
        finally
        {
            if (Directory.Exists(directory)) Directory.Delete(directory, true);
        }
    }

    [TestMethod]
    public void Invalid_preference_uses_automatic_and_default_path_is_in_user_data()
    {
        var defaultPath = new AppearanceSettings().FilePath;
        var userData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        Assert.IsTrue(Path.GetFullPath(defaultPath).StartsWith(
            Path.GetFullPath(userData) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase));

        var directory = Path.Combine(Path.GetTempPath(), "hex-editor-appearance-" + Guid.NewGuid());
        Directory.CreateDirectory(directory);
        try
        {
            var path = Path.Combine(directory, "appearance.json");
            File.WriteAllText(path, "{bad json}");
            Assert.AreEqual(AppearanceMode.Automatic, new AppearanceSettings(path).Mode);
            File.WriteAllText(path, "{\"Mode\":99}");
            Assert.AreEqual(AppearanceMode.Automatic, new AppearanceSettings(path).Mode);
        }
        finally
        {
            Directory.Delete(directory, true);
        }
    }
}
