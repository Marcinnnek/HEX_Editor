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

    [TestMethod]
    public void ByteGridLayoutMeasuresRowsAndHitTestsUsingTheSameByteBounds()
    {
        var layout = new ByteGridLayout(CharacterWidth: 10, LineHeight: 20, HorizontalSpacing: 5, VerticalSpacing: 3);

        Assert.AreEqual(new System.Windows.Size(45, 23), layout.GetRowSize(2, isAscii: false));
        Assert.AreEqual(new System.Windows.Size(20, 23), layout.GetRowSize(2, isAscii: true));
        Assert.IsTrue(layout.TryGetByteIndexAt(new System.Windows.Point(44.9, 22.9), 2, isAscii: false, out var second));
        Assert.AreEqual(1, second);
        Assert.IsFalse(layout.TryGetByteIndexAt(new System.Windows.Point(21, 10), 2, isAscii: false, out _));
        Assert.IsFalse(layout.TryGetByteIndexAt(new System.Windows.Point(10, 23), 2, isAscii: false, out _));
        Assert.IsTrue(layout.TryGetByteIndexAt(new System.Windows.Point(19, 22.9), 2, isAscii: true, out var asciiSecond));
        Assert.AreEqual(1, asciiSecond);
    }
}
