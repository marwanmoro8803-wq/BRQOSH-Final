using System.IO;
using System.Windows;
using Brqosh.Application.Setup;
using Brqosh.Desktop.Views;
using Brqosh.Domain.Security;
using Brqosh.Infrastructure.Licensing;
using Brqosh.Infrastructure.Persistence;
using Brqosh.Infrastructure.Setup;
using Microsoft.EntityFrameworkCore;

namespace Brqosh.Desktop;

public partial class App : System.Windows.Application
{
    private readonly ISetupStateStore _setupStore = new JsonSetupStateStore();

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        Ui.UiLocalizationHost.Register();

        DispatcherUnhandledException += (_, args) =>
        {
            var detail = args.Exception.InnerException is null
                ? args.Exception.Message
                : $"{args.Exception.Message}\n\n{args.Exception.InnerException.Message}";
            MessageBox.Show(
                $"خطأ غير متوقع:\n{detail}",
                "BRQOSH — خطأ", MessageBoxButton.OK, MessageBoxImage.Error);
            args.Handled = true;
        };

        AppDomain.CurrentDomain.UnhandledException += (_, args) =>
        {
            if (args.ExceptionObject is Exception ex)
            {
                MessageBox.Show(
                    $"خطأ حرج:\n{ex.Message}",
                    "BRQOSH — خطأ", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        };

        var setup = _setupStore.Load();
        string? adminUsername = null;
        string? adminPassword = null;

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
            adminUsername = wizard.AdminUsername;
            adminPassword = wizard.AdminPassword;
        }

        try
        {
            AppServices.LoadDatabaseConfig();
            await AppServices.EnsureDatabaseReachableAsync();
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                $"تعذر تهيئة قاعدة البيانات:\n{ex.Message}",
                "BRQOSH — خطأ في التشغيل", MessageBoxButton.OK, MessageBoxImage.Error);
            Shutdown();
            return;
        }

        try
        {
            // CEEF-200: التمهيد عبر DI scope (المصنع كان ينشئ الخدمة يدوياً — يكسر حارس DI،
            // ويشغّل على سياق بلا حارس تدقيق). المزوّد مُهيّأ في LoadDatabaseConfig أعلاه.
            using var facade = new DesktopAccountingFacade();
            var result = await facade.GetService<DatabaseBootstrapService>()
                .InitializeAsync(setup!.CompanyName, setup.VatRegistrationNumber, setup.Profile,
                    adminUsername, adminPassword, setup.CountryTemplateId);
            AppServices.CompanyId = result.CompanyId;
        }
        catch (Exception ex)
        {
            var detail = ex.InnerException is null
                ? ex.Message
                : $"{ex.Message}\n—\n{ex.InnerException.Message}";
            MessageBox.Show(
                $"تعذر تهيئة قاعدة البيانات ({AppServices.ActiveProviderLabel}):\n{detail}",
                "BRQOSH — خطأ في التشغيل", MessageBoxButton.OK, MessageBoxImage.Error);
            Shutdown();
            return;
        }

        if (!LicenseStore.ValidateForCompany(setup!.CompanyName, setup.CompletedAtUtc))
        {
            var license = new LicenseActivationWindow(setup.CompanyName);
            if (license.ShowDialog() != true)
            {
                Shutdown();
                return;
            }
        }

        if (await TryEnterWithUiTestAuthAsync(setup))
            return;

        var login = new LoginWindow();
        if (login.ShowDialog() != true || login.AuthenticatedUser is null)
        {
            Shutdown();
            return;
        }

        if (login.AuthenticatedUser.MustChangePasswordOnNextLogin)
        {
            var change = new PasswordChangeWindow(login.AuthenticatedUser);
            if (change.ShowDialog() != true || change.UpdatedUser is null)
            {
                Shutdown();
                return;
            }
            login.AuthenticatedUser = change.UpdatedUser;
        }

        AppServices.CurrentUser = login.AuthenticatedUser;

        var main = new MainWindow(setup!);
        MainWindow = main;
        main.Closed += (_, _) => Shutdown();
        main.Show();
        main.Activate();
        main.Focus();
    }

    /// <summary>دخول آلي للتدقيق FlaUI فقط — BRQOSH_UITEST=1 (لا يُفعَّل في التشغيل العادي).</summary>
    private async Task<bool> TryEnterWithUiTestAuthAsync(SetupState setup)
    {
        if (!string.Equals(Environment.GetEnvironmentVariable("BRQOSH_UITEST"), "1", StringComparison.Ordinal))
            return false;

        WriteUiTestDiag("START · UITEST bypass engaged");

        try
        {
            var companyId = AppServices.CompanyId;
            await using var db = AppServices.CreateDb();

            var testUsername = Environment.GetEnvironmentVariable("BRQOSH_TEST_USERNAME")?.Trim();
            AppUser? user = null;
            if (!string.IsNullOrEmpty(testUsername))
            {
                user = await db.AppUsers.FirstOrDefaultAsync(u =>
                    u.CompanyId == companyId && u.Username == testUsername && u.IsActive);
                user ??= await db.AppUsers.FirstOrDefaultAsync(u =>
                    u.Username == testUsername && u.IsActive);
            }

            user ??= await db.AppUsers
                .Where(u => u.CompanyId == companyId && u.IsActive)
                .OrderByDescending(u => u.Role == UserRole.Administrator)
                .ThenBy(u => u.Id)
                .FirstOrDefaultAsync();

            user ??= await db.AppUsers
                .Where(u => u.IsActive && u.Role == UserRole.Administrator)
                .OrderBy(u => u.Id)
                .FirstOrDefaultAsync();

            if (user is null)
            {
                WriteUiTestDiag(
                    $"no active users · provider={AppServices.ActiveProviderLabel} · companyId={companyId} · testUser={testUsername ?? "(none)"}");
                return false;
            }

            if (user.MustChangePasswordOnNextLogin)
            {
                user.ClearMustChangePassword();
                await db.SaveChangesAsync();
            }

            AppServices.CompanyId = user.CompanyId;
            AppServices.CurrentUser = user;

            var main = new MainWindow(setup);
            MainWindow = main;
            main.Closed += (_, _) => Shutdown();
            main.Show();
            main.Activate();
            main.Focus();
            WriteUiTestDiag($"OK · user={user.Username} · companyId={user.CompanyId} · provider={AppServices.ActiveProviderLabel}");
            return true;
        }
        catch (Exception ex)
        {
            WriteUiTestDiag(ex.ToString());
            return false;
        }
    }

    private static void WriteUiTestDiag(string message)
    {
        try
        {
            var path = Path.Combine(Path.GetTempPath(), "brqosh-uitest-last.txt");
            File.WriteAllText(path, $"{DateTime.UtcNow:u}\n{message}");
        }
        catch
        {
            /* ignore */
        }
    }
}
