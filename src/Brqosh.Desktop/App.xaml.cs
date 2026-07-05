using System.Windows;
using Brqosh.Application.Setup;
using Brqosh.Desktop.Views;
using Brqosh.Infrastructure.Setup;

namespace Brqosh.Desktop;

public partial class App : System.Windows.Application
{
    private readonly ISetupStateStore _setupStore = new JsonSetupStateStore();

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        var setup = _setupStore.Load();
        if (setup is null || !setup.IsCompleted)
        {
            var wizard = new SetupWizardWindow(_setupStore);
            var completed = wizard.ShowDialog() == true;
            if (!completed)
            {
                Shutdown();
                return;
            }
            setup = _setupStore.Load();
        }

        var main = new MainWindow(setup!);
        MainWindow = main;
        main.Closed += (_, _) => Shutdown();
        main.Show();
    }
}
