using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Brqosh.Desktop.Automation;
using Brqosh.Domain.Sales;
using Microsoft.EntityFrameworkCore;
using Brqosh.Desktop.Security;
using Brqosh.Desktop.Ui;
using Brqosh.Desktop.ViewModels;

namespace Brqosh.Desktop.Views.Pages;

public partial class CustomersPage : UserControl
{
    public record CustomerRow(long Id, string NameAr, string VatRegistrationNumber,
        string Phone, string Address, string InvoiceKind, decimal Balance);

    private readonly CustomersViewModel _vm = new();
    private readonly SearchDebouncer _searchDebouncer;

    public CustomersPage()
    {
        InitializeComponent();
        _searchDebouncer = new SearchDebouncer(TimeSpan.FromMilliseconds(300), ReloadAsync);
        Loaded += async (_, _) =>
        {
            PermissionService.ApplyActionVisibility(AppServices.CurrentUser,
                (AddCustomerBtn, PermissionService.CanPost),
                (DeactivateBtn, PermissionService.CanPostAccounting));
            await UiFeedback.RunSafeAsync(ReloadAsync);
        };
    }

    private async Task ReloadAsync()
    {
        // (م2-٢٣) الموقوف يظهر عند طلبه.
        await _vm.LoadAsync(SearchBox.Text.Trim(), ShowInactiveCheck.IsChecked == true);
        CustomersGrid.ItemsSource = _vm.Rows;
        UiTestAutomation.PublishGridCount(CustomersGrid, _vm.Rows.Count);
        CustomerSummaryText.Text =
            $"عملاء نشطون: {_vm.TotalCount:N0} · معروض: {_vm.Rows.Count:N0} · إجمالي AR: {_vm.TotalBalance:N2} {CurrencyLabel.Short}";
    }

    private void OnSearchChanged(object sender, TextChangedEventArgs e) => _searchDebouncer.Trigger();

    private async void OnAddCustomer(object sender, RoutedEventArgs e)
    {
        if (new CustomerDialog { Owner = Window.GetWindow(this) }.ShowDialog() == true)
            await ReloadAsync();
    }

    private async void OnEdit(object sender, MouseButtonEventArgs e)
    {
        if (CustomersGrid.SelectedItem is not CustomerRow row) return;
        var customer = _vm.Find(row.Id);
        if (customer is null) return;
        if (new CustomerDialog(customer) { Owner = Window.GetWindow(this) }.ShowDialog() == true)
            await ReloadAsync();
    }

    private async void OnDeactivate(object sender, RoutedEventArgs e)
    {
        if (CustomersGrid.SelectedItem is not CustomerRow row) return;
        if (UiDialog.Show($"إيقاف العميل {row.NameAr}؟", "BRQOSH",
                MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
            return;

        try
        {
            await _vm.DeactivateAsync(row.Id);
            await ReloadAsync();
        }
        catch (Exception ex)
        {
            UiFeedback.ShowWarning(ex.Message);
        }
    }

    /// <summary>(م2-٢٣) تبديل إظهار الموقوف.</summary>
    private async void OnToggleInactive(object sender, RoutedEventArgs e)
    {
        if (!IsLoaded) return;
        // ‏(ن-١٨) **والدورُ شرطٌ هنا لا في `Loaded` وحدَه.** كان هذا السطرُ يُعيد الزرَّ
        // ‏مرئيّاً لكلِّ دورٍ عند أوّل ضغطةٍ على «إظهار الموقوف» — **وهي الضغطةُ التي لا
        // ‏يُبلَغ الزرُّ بدونها**. فحارسٌ يُوضَع في `ApplyActionVisibility` وحدَه **يُدهَس
        // ‏قبل أن يُستعمَل**، ويبقى مظهرُه في المُراجعة صحيحاً.
        ReactivateBtn.Visibility = ShowInactiveCheck.IsChecked == true
                                   && PermissionService.CanPostAccounting(AppServices.CurrentUser)
            ? Visibility.Visible
            : Visibility.Collapsed;
        await ReloadAsync();
    }

    /// <summary>(م2-٢٣) إعادة التفعيل — الطريق الذي لم يكن موجوداً.</summary>
    private async void OnReactivate(object sender, RoutedEventArgs e)
    {
        if (CustomersGrid.SelectedItem is not CustomerRow row) return;

        // ‏(ن-١٨) **الحارسُ عند نقطة الكتابة — وهو الذي يصمد.** الإخفاءُ ليس تخويلاً:
        // ‏زرُّ الإيقاف مَحروسٌ منذ م2-٢٣ وزرُّ العودة لم يكن، **فكان الصرّافُ يُعيد تفعيل
        // ‏ما لا يملك إيقافَه** — سقّاطةٌ في الاتجاه الخطأ. و`IsActive` هنا يُغذّي قائمةَ
        // ‏سند القبض (`CollectableCustomersQuery.cs:44`)، والأثرُ في `ProductsPage` أوسع.
        if (!PermissionService.CanPostAccounting(AppServices.CurrentUser))
        {
            UiFeedback.ShowWarning(PermissionService.DeniedActionMessage("إعادة التفعيل", "reactivate"));
            return;
        }

        try
        {
            await _vm.ReactivateAsync(row.Id);
            await ReloadAsync();
        }
        catch (Exception ex)
        {
            UiFeedback.ShowWarning(ex.Message);
        }
    }
}
