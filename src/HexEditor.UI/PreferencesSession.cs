using System.IO;

namespace HexEditor.UI;

/// <summary>Stages display choices until the user saves Preferences.</summary>
public sealed class PreferencesSession
{
    private readonly AppearanceSession _appearance;
    private readonly GridSpacingSettings _spacing;

    public PreferencesSession(AppearanceSession appearance, GridSpacingSettings spacing)
    {
        _appearance = appearance;
        _spacing = spacing;
        Appearance = appearance.Mode;
        HorizontalByteSpacing = spacing.HorizontalByteSpacing;
        VerticalRowSpacing = spacing.VerticalRowSpacing;
    }

    public AppearanceMode Appearance { get; set; }
    public int HorizontalByteSpacing { get; set; }
    public int VerticalRowSpacing { get; set; }

    public void Save()
    {
        if (!Enum.IsDefined(Appearance)) throw new ArgumentOutOfRangeException(nameof(Appearance));
        if (HorizontalByteSpacing is < 0 or > 12) throw new ArgumentOutOfRangeException(nameof(HorizontalByteSpacing));
        if (VerticalRowSpacing is < 0 or > 8) throw new ArgumentOutOfRangeException(nameof(VerticalRowSpacing));
        var writes = new[]
        {
            new StagedWrite(_spacing.FilePath, GridSpacingSettings.Serialize(HorizontalByteSpacing, VerticalRowSpacing)),
            new StagedWrite(_appearance.SettingsPath, AppearanceSettings.Serialize(Appearance))
        };
        if (Path.GetFullPath(writes[0].Path).Equals(Path.GetFullPath(writes[1].Path), StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Appearance and spacing settings must use different files.");

        try
        {
            foreach (var write in writes) write.Stage();
            foreach (var write in writes) write.Commit();
        }
        catch (Exception failure)
        {
            var rollbackFailures = new List<Exception>();
            foreach (var write in writes.Reverse())
            {
                try { write.Rollback(); }
                catch (Exception exception) { rollbackFailures.Add(exception); }
            }
            if (rollbackFailures.Count > 0)
                throw new AggregateException("Preferences save and rollback failed.", [failure, .. rollbackFailures]);
            throw;
        }
        finally
        {
            foreach (var write in writes) write.Cleanup();
        }

        _spacing.ApplySavedSpacing(HorizontalByteSpacing, VerticalRowSpacing);
        _appearance.ApplySavedMode(Appearance);
    }

    private sealed class StagedWrite
    {
        private readonly string _content;
        private readonly string _temporaryPath;
        private byte[]? _original;
        private bool _existed;
        private bool _committed;

        public StagedWrite(string path, string content)
        {
            Path = path;
            _content = content;
            _temporaryPath = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        }

        public string Path { get; }

        public void Stage()
        {
            var directory = System.IO.Path.GetDirectoryName(Path) ??
                throw new InvalidOperationException("The settings path has no directory.");
            Directory.CreateDirectory(directory);
            _existed = File.Exists(Path);
            if (_existed) _original = File.ReadAllBytes(Path);
            File.WriteAllText(_temporaryPath, _content);
        }

        public void Commit()
        {
            File.Move(_temporaryPath, Path, true);
            _committed = true;
        }

        public void Rollback()
        {
            if (!_committed) return;
            if (_existed)
            {
                File.WriteAllBytes(_temporaryPath, _original!);
                File.Move(_temporaryPath, Path, true);
            }
            else File.Delete(Path);
            _committed = false;
        }

        public void Cleanup()
        {
            // A failed rollback leaves its original bytes in this file for manual recovery.
            if (_committed) return;
            try { if (File.Exists(_temporaryPath)) File.Delete(_temporaryPath); }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }
    }
}
