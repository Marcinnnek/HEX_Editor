using HexEditor.UI;

namespace HexEditor.Tests;

[TestClass]
public class UserDataDiagnosticsTests
{
    [TestMethod]
    public void Default_log_location_is_under_local_application_data()
    {
        var diagnosticsPath = Path.GetFullPath(new UserDataDiagnostics().DirectoryPath);
        var localAppData = Path.GetFullPath(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData));

        Assert.IsTrue(diagnosticsPath.StartsWith(localAppData + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase));
    }

    [TestMethod]
    public void Error_entry_records_operation_and_address_without_exception_message_or_path()
    {
        var directory = Path.Combine(Path.GetTempPath(), "hex-editor-diagnostics-" + Guid.NewGuid());
        var sourcePath = Path.Combine(Path.GetTempPath(), "private-firmware.hex");
        try
        {
            var diagnostics = new UserDataDiagnostics(directory);
            diagnostics.RecordError("Edit", new IOException($"failed for {sourcePath}: DEADBEEF"), 0x1234, 0x1237);

            var entry = File.ReadAllText(Path.Combine(directory, "diagnostics.log"));
            StringAssert.Contains(entry, "Edit");
            StringAssert.Contains(entry, "0x00001234-0x00001237");
            StringAssert.Contains(entry, "IOException");
            Assert.IsFalse(entry.Contains(sourcePath, StringComparison.OrdinalIgnoreCase));
            Assert.IsFalse(entry.Contains("DEADBEEF", StringComparison.Ordinal));
        }
        finally
        {
            if (Directory.Exists(directory)) Directory.Delete(directory, true);
        }
    }

    [TestMethod]
    public void Logs_rotate_to_five_files_and_never_exceed_one_mib()
    {
        var directory = Path.Combine(Path.GetTempPath(), "hex-editor-diagnostics-" + Guid.NewGuid());
        try
        {
            var diagnostics = new UserDataDiagnostics(directory);
            for (var index = 0; index < 60000; index++)
                diagnostics.RecordError(new string('O', 64), new IOException());

            var files = Directory.GetFiles(directory, "diagnostics*.log");
            Assert.HasCount(UserDataDiagnostics.MaximumFiles, files);
            Assert.IsTrue(files.All(path => new FileInfo(path).Length <= UserDataDiagnostics.MaximumFileBytes));
        }
        finally
        {
            if (Directory.Exists(directory)) Directory.Delete(directory, true);
        }
    }

    [TestMethod]
    public void Unwritable_diagnostics_location_does_not_throw()
    {
        var parent = Path.Combine(Path.GetTempPath(), "hex-editor-diagnostics-" + Guid.NewGuid());
        Directory.CreateDirectory(parent);
        try
        {
            var fileAsDirectory = Path.Combine(parent, "file");
            File.WriteAllText(fileAsDirectory, "occupied");
            var diagnostics = new UserDataDiagnostics(Path.Combine(fileAsDirectory, "child"));

            diagnostics.RecordError("Open", new IOException());
        }
        finally
        {
            Directory.Delete(parent, true);
        }
    }
}
