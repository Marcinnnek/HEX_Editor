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
        _spacing.SetSpacing(HorizontalByteSpacing, VerticalRowSpacing);
        _appearance.SetMode(Appearance);
    }
}
