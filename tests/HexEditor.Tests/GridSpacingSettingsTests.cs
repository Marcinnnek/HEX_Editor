using HexEditor.UI;

namespace HexEditor.Tests;

[TestClass]
public sealed class GridSpacingSettingsTests
{
    [TestMethod]
    public void NewProfileUsesTighterGridDefaults()
    {
        var settings = new GridSpacingSettings(Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".json"));

        Assert.AreEqual(2, settings.HorizontalByteSpacing);
        Assert.AreEqual(1, settings.VerticalRowSpacing);
    }

    [TestMethod]
    public void IndependentSpacingChoicesPersistAcrossInstances()
    {
        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".json");
        var settings = new GridSpacingSettings(path);

        settings.SetSpacing(horizontalByteSpacing: 7, verticalRowSpacing: 4);
        var restored = new GridSpacingSettings(path);

        Assert.AreEqual(7, restored.HorizontalByteSpacing);
        Assert.AreEqual(4, restored.VerticalRowSpacing);
    }

    [TestMethod]
    public void ByteGridLayoutLeavesSeparatorsOutsideHighlightBounds()
    {
        var layout = new ByteGridLayout(CharacterWidth: 10, LineHeight: 20, HorizontalSpacing: 5, VerticalSpacing: 3);

        Assert.AreEqual(new System.Windows.Rect(0, 0, 20, 23), layout.GetByteBounds(0, isAscii: false));
        Assert.AreEqual(new System.Windows.Rect(25, 0, 20, 23), layout.GetByteBounds(1, isAscii: false));
        Assert.AreEqual(new System.Windows.Rect(0, 0, 20, 20), layout.GetHighlightBounds(0));
    }
}
