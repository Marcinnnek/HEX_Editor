using System.Windows;
using System.Windows.Threading;
using HexEditor.UI;

namespace HexEditor.Tests;

[TestClass]
[DoNotParallelize]
public sealed class ApplicationIconAcceptanceTests
{
    [TestMethod]
    [DataRow(AppearanceMode.Light)]
    [DataRow(AppearanceMode.Dark)]
    public void Main_window_and_application_dialog_render_with_the_supplied_icon(AppearanceMode mode)
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            MainWindow? mainWindow = null;
            SaveChangesDialog? dialog = null;
            try
            {
                mainWindow = new MainWindow();
                ApplicationAppearance.ApplyToWindow(mainWindow, mode);
                mainWindow.Show();
                mainWindow.UpdateLayout();
                Assert.IsNotNull(mainWindow.Icon, "The main window should expose the application icon after rendering.");
                Assert.AreEqual("pack://application:,,,/HexEditor.UI;component/HexEditor.ico", mainWindow.Icon!.ToString());

                dialog = new SaveChangesDialog(mainWindow, "firmware.hex")
                {
                    Left = -3000,
                    Top = -3000,
                    WindowStartupLocation = WindowStartupLocation.Manual,
                    ShowActivated = false
                };
                ApplicationAppearance.ApplyToWindow(dialog, mode);
                var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(30) };
                timer.Tick += (_, _) =>
                {
                    timer.Stop();
                    dialog.UpdateLayout();
                    Assert.IsNotNull(dialog.Icon, "The application dialog should expose the same icon after rendering.");
                    Assert.AreEqual(mainWindow.Icon.ToString(), dialog.Icon!.ToString());
                    dialog.Close();
                };
                timer.Start();
                dialog.ShowDialog();
            }
            catch (Exception exception) { failure = exception; }
            finally { dialog?.Close(); mainWindow?.Close(); }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
        if (failure is not null) System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(failure).Throw();
    }
}
