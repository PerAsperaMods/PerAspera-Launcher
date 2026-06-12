using System.Windows;
using PerAsperaLauncher.ViewModels;

namespace PerAsperaLauncher;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        var vm = new MainViewModel();
        var window = new MainWindow { DataContext = vm };
        window.Show();
        _ = vm.LoadAsync();
    }
}
