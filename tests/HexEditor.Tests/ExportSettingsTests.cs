using HexEditor.UI;

namespace HexEditor.Tests;

[TestClass]
public class ExportSettingsTests
{
    [TestMethod]
    public void Record_size_defaults_to_sixteen_and_last_choice_survives_restart()
    {
        var directory = Path.Combine(Path.GetTempPath(), "hex-editor-export-" + Guid.NewGuid());
        var path = Path.Combine(directory, "export.json");
        try
        {
            Assert.AreEqual(16, new ExportSettings(path).MaxDataBytesPerRecord);
            new ExportSettings(path).SetMaxDataBytesPerRecord(32);
            Assert.AreEqual(32, new ExportSettings(path).MaxDataBytesPerRecord);
        }
        finally
        {
            if (Directory.Exists(directory)) Directory.Delete(directory, true);
        }
    }

    [TestMethod]
    public void Invalid_export_record_size_is_rejected_without_changing_preference()
    {
        var directory = Path.Combine(Path.GetTempPath(), "hex-editor-export-" + Guid.NewGuid());
        var path = Path.Combine(directory, "export.json");
        try
        {
            var settings = new ExportSettings(path);
            Assert.Throws<ArgumentOutOfRangeException>(() => settings.SetMaxDataBytesPerRecord(24));
            Assert.AreEqual(16, new ExportSettings(path).MaxDataBytesPerRecord);
        }
        finally
        {
            if (Directory.Exists(directory)) Directory.Delete(directory, true);
        }
    }
}
