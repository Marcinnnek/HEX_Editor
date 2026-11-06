using System.Windows;
using System.Windows.Media;

namespace HexEditor.UI;

/// <summary>Colors for standard WPF controls and the custom byte renderer.</summary>
internal static class AppearancePalette
{
    internal static event Action? Changed;

    internal static void Apply(bool dark)
    {
        var resources = Application.Current?.Resources;
        if (resources is null) return;

        Apply(resources, dark);
    }

    internal static void Apply(ResourceDictionary resources, bool dark)
    {
        Set(resources, "HexWindowBrush", dark ? "#17191D" : "#F8F9FB");
        Set(resources, "HexSurfaceBrush", dark ? "#24272D" : "#FFFFFF");
        Set(resources, "HexRaisedBrush", dark ? "#30343B" : "#EDF0F4");
        Set(resources, "HexTextBrush", dark ? "#F2F4F7" : "#1B1D21");
        Set(resources, "HexMutedTextBrush", dark ? "#BBC3CE" : "#4C5561");
        Set(resources, "HexDisabledTextBrush", dark ? "#929BA6" : "#68717D");
        Set(resources, "HexBorderBrush", dark ? "#6B7582" : "#AAB3BE");
        Set(resources, "HexFocusBrush", dark ? "#79BCFF" : "#246CA4");
        Set(resources, "HexErrorBrush", dark ? "#FF9292" : "#A52828");
        Set(resources, "HexFindMatchBrush", dark ? "#FFFFB347" : "#FFB56600");
        Set(resources, "HexAsciiBackgroundBrush", dark ? "#244C6E93" : "#146B87A6");
        var colors = ComparisonColorSettings.Current.GetPalette(dark ? AppearanceMode.Dark : AppearanceMode.Light);
        Set(resources, "HexSelectionBrush", colors.Get(ComparisonColorKind.SelectedByteBackground));
        Set(resources, "HexSelectedByteBorderBrush", colors.Get(ComparisonColorKind.SelectedByteBorder));
        Set(resources, "HexDifferentBrush", colors.Different);
        Set(resources, "HexOnlyInABrush", colors.OnlyInA);
        Set(resources, "HexOnlyInBBrush", colors.OnlyInB);
        Set(resources, "HexEditedByteBrush", colors.EditedByte);
        Set(resources, "HexEditedByteBackgroundBrush", colors.Get(ComparisonColorKind.EditedByteBackground));
        Changed?.Invoke();
    }

    private static void Set(ResourceDictionary resources, string key, string color)
    {
        var brush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(color));
        brush.Freeze();
        resources[key] = brush;
    }
}
