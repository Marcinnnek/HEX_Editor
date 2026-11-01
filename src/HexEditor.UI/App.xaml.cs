using System.Configuration;
using System.Data;
using System.Windows;

namespace HexEditor.UI;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        ApplicationAppearance.Apply(AppearanceMode.Light);
        base.OnStartup(e);
        var window = new MainWindow();
        window.SizeForDefaultRowWidth();
        MainWindow = window;
        window.Show();
        if (e.Args.Length > 0)
            window.Dispatcher.BeginInvoke(new Action(async () => await window.OpenPathsAsync(e.Args)));
    }
}

