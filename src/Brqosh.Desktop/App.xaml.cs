using System.Windows;
using Brqosh.Application.Setup;
using Brqosh.Desktop.Views;
using Brqosh.Infrastructure.Persistence;
using Brqosh.Infrastructure.Setup;

namespace Brqosh.Desktop;

public partial class App : System.Windows.Application
{
    private readonly ISetupStateStore _setupStore = new JsonSetupStateStore();

    protected override async void OnStartup(StartupEventArgs e)
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

        AppServices.LoadDatabaseConfig();
        try
        {
            await using var db = AppServices.CreateDb();
            var company = await new DatabaseInitializer(db)
                .InitializeAsync(setup!.CompanyName, setup.VatRegistrationNumber, setup.Profile);
            AppServices.CompanyId = company.Id;
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                $"تعذر تهيئة قاعدة البيانات:\n{ex.Message}",
                "BRQOSH — خطأ في التشغيل", MessageBoxButton.OK, MessageBoxImage.Error);
            Shutdown();
            return;
        }

        var main = new MainWindow(setup!);
        MainWindow = main;
        main.Closed += (_, _) => Shutdown();
        main.Show();
    }
}
