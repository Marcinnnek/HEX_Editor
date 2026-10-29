namespace HexEditor.UI;

internal static class FirmwareImageDialog
{
    private const string Filter = "Firmware images (*.hex;*.ihx;*.s19;*.s28;*.s37;*.srec;*.mot)|*.hex;*.ihx;*.s19;*.s28;*.s37;*.srec;*.mot|All files (*.*)|*.*";

    public static Microsoft.Win32.OpenFileDialog CreateOpen(string title, string? initialDirectory = null)
    {
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Title = title,
            Filter = Filter,
            CheckFileExists = true
        };
        if (initialDirectory is not null)
            dialog.InitialDirectory = initialDirectory;
        return dialog;
    }
}
