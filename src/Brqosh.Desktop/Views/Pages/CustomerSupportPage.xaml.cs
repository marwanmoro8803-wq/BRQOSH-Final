using System.Diagnostics;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using Brqosh.Desktop.Automation;
using Brqosh.Desktop.Support;
using Brqosh.Desktop.Ui;

namespace Brqosh.Desktop.Views.Pages;

/// <summary>
/// الدعمُ والشكاوى — <b>القنواتُ ومستوياتُ الخدمة كما اعتمدها المورّد في <c>config/support.json</c></b> وبدونه «لم تُعتمد»
/// (<c>commercial-readiness#7</c>)، <b>وطلباتٌ حقيقيةٌ تُسجَّل من الشاشة</b> (سؤال · مشكلة · شكوى · اقتراح) في سجلٍّ على هذا
/// الجهاز مع بريدٍ جاهز إلى عنوان الدعم المعتمد. كان جدولُ التذاكر فارغاً دائماً بنصّ «لا تُدار من داخل البرنامج».
/// </summary>
public partial class CustomerSupportPage : UserControl
{
    private SupportContact _support = SupportContact.NotConfigured();
    private bool _loading;

    public CustomerSupportPage()
    {
        InitializeComponent();
        Loaded += (_, _) => Bind();
        LocaleManager.Changed += OnLocaleChanged;
        Unloaded += (_, _) => LocaleManager.Changed -= OnLocaleChanged;
    }

    private void OnLocaleChanged()
    {
        LocaleManager.ApplyTo(this);
        if (IsLoaded)
            Bind();
    }

    private void Bind()
    {
        _loading = true;
        try
        {
            TitleText.Text = LocaleManager.T("الدعم والشكاوى", "Support and complaints");
            SubtitleText.Text = LocaleManager.T(
                "قنوات التواصل · مستويات الخدمة · ارفع سؤالاً أو مشكلة أو شكوى أو اقتراحاً",
                "Contact channels · service levels · raise a question, problem, complaint or suggestion");

            _support = SupportContact.LoadBeside(AppContext.BaseDirectory);

            SlaTitle.Text = LocaleManager.T("مستويات الخدمة (SLA)", "Service levels (SLA)");
            SlaPlanCol.Header = LocaleManager.T("الباقة", "Plan");
            SlaResponseCol.Header = LocaleManager.T("زمن الاستجابة", "Response time");
            SlaHoursCol.Header = LocaleManager.T("ساعات العمل", "Working hours");
            SlaGrid.ItemsSource = _support.Plans.Select(p => new SlaRow(p.Plan, p.Response, p.Hours)).ToList();
            SlaGrid.Visibility = _support.Plans.Count > 0 ? Visibility.Visible : Visibility.Collapsed;

            EmailLabel.Text = LocaleManager.T("البريد:", "Email:");
            EmailText.Text = _support.Email ?? "";
            EmailRow.Visibility = _support.Email is null ? Visibility.Collapsed : Visibility.Visible;
            PhoneLabel.Text = LocaleManager.T("الهاتف:", "Phone:");
            PhoneText.Text = _support.Phone ?? "";
            PhoneRow.Visibility = _support.Phone is null ? Visibility.Collapsed : Visibility.Visible;

            if (_support.Problem is not null)
                SupportStateText.Text = _support.Problem;
            else if (!_support.IsConfigured)
                SupportStateText.Text = LocaleManager.T(
                    "لم تعتمد قنوات الدعم ولا مستويات الخدمة لهذه النسخة بعد.",
                    "Support channels and service levels have not been approved for this copy yet.");
            SupportStateText.Visibility = _support.IsConfigured ? Visibility.Collapsed : Visibility.Visible;

            NewTitle.Text = LocaleManager.T("طلب جديد", "New request");
            KindLabel.Text = LocaleManager.T("النوع", "Type");
            var selectedKind = (KindBox.SelectedItem as KindChoice)?.Kind ?? SupportRequestKind.Problem;
            KindBox.ItemsSource = Enum.GetValues<SupportRequestKind>().Select(k => new KindChoice(k, KindLabelOf(k))).ToList();
            KindBox.DisplayMemberPath = nameof(KindChoice.Label);
            KindBox.SelectedItem = ((IEnumerable<KindChoice>)KindBox.ItemsSource).First(k => k.Kind == selectedKind);
            SubjectLabel.Text = LocaleManager.T("الموضوع *", "Subject *");
            DetailsLabel.Text = LocaleManager.T("التفاصيل * (ماذا فعلت؟ وماذا ظهر؟)", "Details * (what did you do, and what happened?)");
            RegisterBtn.Content = LocaleManager.T("تسجيل الطلب", "Register request");
            RegisterMailBtn.Content = LocaleManager.T("تسجيل وفتح بريد إلى الدعم", "Register and open an email to support");
            RegisterMailBtn.Visibility = _support.Email is null ? Visibility.Collapsed : Visibility.Visible;
            HonestyText.Text = _support.Email is null
                ? LocaleManager.T(
                    "يُسجَّل الطلب على هذا الجهاز فقط ولا يُرسل شيئاً من تلقاء البرنامج. لا بريد دعم معتمداً في هذه النسخة بعد — انسخ نص الطلب وأرسله بالقناة التي يزوّدك بها المورّد.",
                    "The request is recorded on this device only; the program sends nothing by itself. No approved support email is set for this copy yet — copy the request text and send it through the channel your vendor gives you.")
                : LocaleManager.T(
                    "يُسجَّل الطلب على هذا الجهاز، ويفتح «تسجيل وفتح بريد» رسالةً جاهزةً في برنامج بريدك لتراجعها وترسلها أنت — لا يُرسل البرنامج شيئاً من تلقاء نفسه.",
                    "The request is recorded on this device, and 'Register and open an email' opens a ready message in your mail program for you to review and send — the program sends nothing by itself.");

            RequestsTitle.Text = LocaleManager.T("طلباتي المسجَّلة على هذا الجهاز", "My requests recorded on this device");
            IdCol.Header = LocaleManager.T("الرقم", "No.");
            KindCol.Header = LocaleManager.T("النوع", "Type");
            SubjectCol.Header = LocaleManager.T("الموضوع", "Subject");
            DateCol.Header = LocaleManager.T("التاريخ", "Date");
            StatusCol.Header = LocaleManager.T("الحالة", "Status");
            CopyBtn.Content = LocaleManager.T("نسخ نص الطلب", "Copy request text");
            MailBtn.Content = LocaleManager.T("فتح بريد للطلب", "Open an email for it");
            CloseBtn.Content = LocaleManager.T("إغلاق الطلب", "Close request");
            MailBtn.Visibility = _support.Email is null ? Visibility.Collapsed : Visibility.Visible;

            RefreshRequests();
        }
        finally
        {
            _loading = false;
        }
    }

    private sealed record SlaRow(string Plan, string Response, string Hours);
    private sealed record KindChoice(SupportRequestKind Kind, string Label);
    private sealed record RequestRow(string Id, string KindLabel, string Subject, string Date, string StatusLabel, SupportRequest Source);

    private static string KindLabelOf(SupportRequestKind kind) => kind switch
    {
        SupportRequestKind.Question => LocaleManager.T("سؤال", "Question"),
        SupportRequestKind.Problem => LocaleManager.T("مشكلة", "Problem"),
        SupportRequestKind.Complaint => LocaleManager.T("شكوى", "Complaint"),
        _ => LocaleManager.T("اقتراح", "Suggestion"),
    };

    private static string StatusLabelOf(SupportRequestStatus status) => status == SupportRequestStatus.Open
        ? LocaleManager.T("مفتوح", "Open")
        : LocaleManager.T("مغلق", "Closed");

    private void RefreshRequests()
    {
        var all = SupportRequestStore.Load();
        TicketsGrid.ItemsSource = all
            .Select(r => new RequestRow(r.Id, KindLabelOf(r.Kind), r.Subject,
                r.CreatedUtc.ToLocalTime().ToString("yyyy-MM-dd"), StatusLabelOf(r.Status), r))
            .ToList();
        UiTestAutomation.PublishGridCount(TicketsGrid, all.Count);

        var open = all.Count(r => r.Status == SupportRequestStatus.Open);
        TicketsSummaryText.Text = all.Count == 0
            ? LocaleManager.T("لا طلبات مسجَّلة بعد.", "No requests recorded yet.")
            : LocaleManager.T($"{all.Count} طلبٍ · {open} مفتوح", $"{all.Count} requests · {open} open");
    }

    private static string AppVersion()
    {
        var entry = typeof(CustomerSupportPage).Assembly;
        var informational = entry.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
        return string.IsNullOrWhiteSpace(informational) ? entry.GetName().Version?.ToString(3) ?? "—" : informational;
    }

    /// <summary>يسجّل الطلب من الحقول. <c>null</c> مع رسالةٍ ظاهرةٍ إن نقص حقل أو تعذّرت الكتابة.</summary>
    private SupportRequest? RegisterFromForm()
    {
        FormError.Visibility = Visibility.Collapsed;
        try
        {
            var kind = (KindBox.SelectedItem as KindChoice)?.Kind ?? SupportRequestKind.Problem;
            var request = SupportRequestStore.Add(
                kind, SubjectBox.Text, DetailsBox.Text,
                AppVersion(), CompanyLabel(), AppServices.CurrentUser?.Username ?? "");
            SubjectBox.Clear();
            DetailsBox.Clear();
            RefreshRequests();
            return request;
        }
        catch (ArgumentException)
        {
            ShowFormError(LocaleManager.T("اكتب الموضوع والتفاصيل أولاً.", "Write the subject and details first."));
        }
        catch (Exception ex) when (ex is System.IO.IOException or UnauthorizedAccessException)
        {
            ShowFormError(LocaleManager.T(
                $"تعذّر حفظ الطلب على هذا الجهاز: {ex.Message}",
                $"Could not save the request on this device: {ex.Message}"));
        }

        return null;
    }

    private static string CompanyLabel()
    {
        try
        {
            using var db = AppServices.CreateDb();
            var id = AppServices.CompanyId;
            return db.Companies.Where(c => c.Id == id).Select(c => c.NameAr).FirstOrDefault() ?? "";
        }
        catch (Exception)
        {
            return "";
        }
    }

    private void ShowFormError(string message)
    {
        FormError.Text = message;
        FormError.Visibility = Visibility.Visible;
    }

    private void OnRegister(object sender, RoutedEventArgs e)
    {
        var request = RegisterFromForm();
        if (request is not null)
            UiFeedback.ShowInfo(LocaleManager.T($"سُجّل الطلب {request.Id}.", $"Request {request.Id} recorded."));
    }

    private void OnRegisterAndMail(object sender, RoutedEventArgs e)
    {
        var request = RegisterFromForm();
        if (request is null)
            return;
        UiFeedback.ShowInfo(LocaleManager.T($"سُجّل الطلب {request.Id}.", $"Request {request.Id} recorded."));
        OpenMail(request);
    }

    private SupportRequest? SelectedRequest() => (TicketsGrid.SelectedItem as RequestRow)?.Source;

    private void OnCopyRequest(object sender, RoutedEventArgs e)
    {
        var request = SelectedRequest();
        if (request is null)
        {
            UiFeedback.ShowWarning(LocaleManager.T("اختر طلباً من القائمة أولاً.", "Select a request from the list first."));
            return;
        }

        try
        {
            Clipboard.SetText(SupportRequestStore.ToText(request, KindLabelOf(request.Kind)));
            UiFeedback.ShowInfo(LocaleManager.T("نُسخ نص الطلب.", "Request text copied."));
        }
        catch (Exception ex)
        {
            UiFeedback.ShowWarning(ex.Message);
        }
    }

    private void OnMailRequest(object sender, RoutedEventArgs e)
    {
        var request = SelectedRequest();
        if (request is null)
        {
            UiFeedback.ShowWarning(LocaleManager.T("اختر طلباً من القائمة أولاً.", "Select a request from the list first."));
            return;
        }

        OpenMail(request);
    }

    private void OpenMail(SupportRequest request)
    {
        if (_support.Email is null)
            return;
        try
        {
            var uri = SupportRequestStore.MailtoUri(_support.Email, request, KindLabelOf(request.Kind));
            Process.Start(new ProcessStartInfo(uri) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            UiFeedback.ShowWarning(LocaleManager.T(
                $"تعذّر فتح برنامج البريد: {ex.Message} — انسخ نص الطلب وأرسله يدوياً.",
                $"Could not open the mail program: {ex.Message} — copy the request text and send it by hand."));
        }
    }

    private void OnCloseRequest(object sender, RoutedEventArgs e)
    {
        var request = SelectedRequest();
        if (request is null)
        {
            UiFeedback.ShowWarning(LocaleManager.T("اختر طلباً من القائمة أولاً.", "Select a request from the list first."));
            return;
        }

        if (_loading)
            return;
        SupportRequestStore.Close(request.Id);
        RefreshRequests();
    }
}
