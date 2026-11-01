using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace HexEditor.UI;

/// <summary>Shared presentation and input rules for 32-bit address fields.</summary>
public static class HexAddressInput
{
    public const double FieldWidth = 116;
    public const double FieldFontSize = 15;

    public static readonly DependencyProperty ApplyProperty = DependencyProperty.RegisterAttached(
        "Apply", typeof(bool), typeof(HexAddressInput), new PropertyMetadata(false, OnApplyChanged));

    public static bool GetApply(DependencyObject element) => (bool)element.GetValue(ApplyProperty);
    public static void SetApply(DependencyObject element, bool value) => element.SetValue(ApplyProperty, value);
    public static void Configure(TextBox box) => SetApply(box, true);

    private static void OnApplyChanged(DependencyObject element, DependencyPropertyChangedEventArgs e)
    {
        if (element is not TextBox box) return;
        if ((bool)e.OldValue)
        {
            box.PreviewTextInput -= PreviewTextInput;
            box.TextChanged -= TextChanged;
            DataObject.RemovePastingHandler(box, Pasting);
        }
        if (!(bool)e.NewValue) return;

        box.Width = FieldWidth;
        box.FontSize = FieldFontSize;
        box.VerticalContentAlignment = VerticalAlignment.Center;
        box.MaxLength = 8;
        box.CharacterCasing = CharacterCasing.Upper;
        box.PreviewTextInput += PreviewTextInput;
        box.TextChanged += TextChanged;
        DataObject.AddPastingHandler(box, Pasting);
        UppercaseText(box);
    }

    private static void PreviewTextInput(object sender, TextCompositionEventArgs e)
    {
        if (sender is not TextBox box) return;
        var proposed = box.Text.Remove(box.SelectionStart, box.SelectionLength)
            .Insert(box.SelectionStart, e.Text);
        e.Handled = !HexAddressParser.IsValidPartialInput(proposed);
    }

    private static void Pasting(object sender, DataObjectPastingEventArgs e)
    {
        if (sender is not TextBox box ||
            !e.SourceDataObject.GetDataPresent(DataFormats.UnicodeText, true) ||
            e.SourceDataObject.GetData(DataFormats.UnicodeText, true) is not string pasted)
        {
            e.CancelCommand();
            return;
        }

        var proposed = box.Text.Remove(box.SelectionStart, box.SelectionLength)
            .Insert(box.SelectionStart, pasted);
        if (!HexAddressParser.IsValidPartialInput(proposed)) e.CancelCommand();
    }

    private static void TextChanged(object sender, TextChangedEventArgs e)
    {
        if (sender is TextBox box) UppercaseText(box);
    }

    private static void UppercaseText(TextBox box)
    {
        var uppercase = box.Text.ToUpperInvariant();
        if (box.Text == uppercase) return;

        var selectionStart = box.SelectionStart;
        var selectionLength = box.SelectionLength;
        box.Text = uppercase;
        box.Select(selectionStart, selectionLength);
    }
}
