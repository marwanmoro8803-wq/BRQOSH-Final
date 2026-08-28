using System.Windows;
using Brqosh.Domain.Construction;
using Brqosh.Infrastructure.Construction;
using Microsoft.EntityFrameworkCore;
using Brqosh.Desktop.Ui;

namespace Brqosh.Desktop.Views.Pages;

public partial class ProjectDialog : Window
{
    private record CustomerOption(long Id, string Name);
    private record StatusOption(ConstructionProjectStatus Status, string Label)
    {
        public override string ToString() => Label;
    }

    private readonly long? _projectId;

    public ProjectDialog()
    {
        InitializeComponent();
        _projectId = null;
        Loaded += OnLoaded;
    }

    public ProjectDialog(ConstructionProject project) : this()
    {
        _projectId = project.Id;
        Title = "تعديل مشروع";
        CodeBox.Text = project.Code;
        CodeBox.IsReadOnly = true;
        NameBox.Text = project.NameAr;
        ValueBox.Text = project.ContractValue.ToString("N2");
        EstimatedCostBox.Text = project.EstimatedTotalCost.ToString("N2");
        RetentionBox.Text = project.RetentionPercent.ToString("N1");
        LocationBox.Text = project.Location;
        if (project.StartDate is DateOnly sd)
            StartDateBox.SelectedDate = sd.ToDateTime(TimeOnly.MinValue);
        if (project.EndDate is DateOnly ed)
            EndDateBox.SelectedDate = ed.ToDateTime(TimeOnly.MinValue);
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        StatusBox.Items.Clear();
        foreach (ConstructionProjectStatus s in Enum.GetValues(typeof(ConstructionProjectStatus)))
            StatusBox.Items.Add(new StatusOption(s, ConstructionProject.StatusLabel(s)));

        await using var db = AppServices.CreateDb();
        var customers = await db.Customers
            .Where(c => c.CompanyId == AppServices.CompanyId && c.IsActive)
            .OrderBy(c => c.NameAr)
            .Select(c => new CustomerOption(c.Id, c.NameAr))
            .ToListAsync();
        customers.Insert(0, new CustomerOption(0, "— بدون عميل —"));
        CustomerBox.ItemsSource = customers;
        CustomerBox.SelectedIndex = 0;

        if (_projectId is long id)
        {
            var project = await db.ConstructionProjects.SingleAsync(p => p.Id == id);
            if (project.CustomerId is long cid)
            {
                var idx = customers.FindIndex(c => c.Id == cid);
                if (idx >= 0) CustomerBox.SelectedIndex = idx;
            }
            var statusIdx = StatusBox.Items.Cast<StatusOption>().ToList()
                .FindIndex(o => o.Status == project.Status);
            if (statusIdx >= 0) StatusBox.SelectedIndex = statusIdx;
        }
        else
        {
            StatusBox.SelectedIndex = 1; // Active
        }
    }

    private async void OnSave(object sender, RoutedEventArgs e)
    {
        try
        {
            if (!NumericInput.TryParse(ValueBox.Text, out var value))
                throw new InvalidOperationException("قيمة العقد غير صالحة.");
            if (!NumericInput.TryParse(EstimatedCostBox.Text, out var estimatedCost))
                throw new InvalidOperationException("التكلفة المقدّرة غير صالحة.");
            if (!NumericInput.TryParse(RetentionBox.Text, out var retention))
                throw new InvalidOperationException("نسبة المحتجز غير صالحة.");

            long? customerId = null;
            if (CustomerBox.SelectedItem is CustomerOption opt && opt.Id > 0)
                customerId = opt.Id;

            DateOnly? start = StartDateBox.SelectedDate is DateTime sd
                ? DateOnly.FromDateTime(sd) : null;
            DateOnly? end = EndDateBox.SelectedDate is DateTime ed
                ? DateOnly.FromDateTime(ed) : null;

            var status = StatusBox.SelectedItem is StatusOption so
                ? so.Status : ConstructionProjectStatus.Planning;

            using var facade = new DesktopAccountingFacade();
            var db = facade.Db;
            var service = facade.GetService<ConstructionProjectService>();

            if (_projectId is long id)
            {
                await service.UpdateAsync(AppServices.CompanyId, id, NameBox.Text, customerId,
                    start, end, value, retention, LocationBox.Text, status, estimatedCost);
            }
            else
            {
                var project = ConstructionProject.Create(
                    AppServices.CompanyId, CodeBox.Text, NameBox.Text,
                    customerId, start, end, value, retention, LocationBox.Text, estimatedCost);
                await service.CreateAsync(project);
                project.Activate();
                await db.SaveChangesAsync();
            }

            DialogResult = true;
            Close();
        }
        catch (Exception ex)
        {
            ErrorText.Text = ex.Message;
            ErrorText.Visibility = Visibility.Visible;
        }
    }

    private void OnCancel(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
