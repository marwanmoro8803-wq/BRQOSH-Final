using System.Text.Json;
using Brqosh.Application.Setup;
using Brqosh.Domain.Modules;

namespace Brqosh.Infrastructure.Setup;

/// <summary>
/// ‏(م-٥ · ن-٥٠) <b>ملفٌّ لا يُفهَم ليس «أوّلَ تشغيل».</b>
///
/// <para><b>ما كان:</b> <c>catch (JsonException) { return null; }</c> بتعليقٍ يقول «ملف تالف
/// — نعامل الحالة كأول تشغيل بدل إسقاط التطبيق». <b>وذلك يُشغّل معالجَ التهيئة فوق دفاترَ
/// حيّة</b>، وقِيس أنّ أثرَه ثلاثةٌ لا واحد:</para>
///
/// <list type="number">
/// <item><b>القاعدةُ سليمة</b> — <c>DatabaseInitializer</c> يعود عند أوّل منشأةٍ قائمة،
/// و<c>CreateInitialAdminAsync</c> يعود عند أوّل مستخدم. فلا منشأةَ ثانيةٌ ولا دليلَ حسابات
/// مُعاد. <b>وهذا يُخفّض أسوأَ ما في البند.</b></item>
/// <item><b>والهويّةُ تُفقد</b> — المعالجُ يكتب <c>SetupState</c> بلا
/// <c>LicensedCompanyName</c>، و<c>App</c> يقرأ <c>LicensedCompanyName ?? CompanyName</c>،
/// <b>فيُقاس الترخيصُ على ما يكتبه المُشغّل الآن</b>. وهو بابُ ن-٣٥ نفسُه من جهةٍ لم تُغلق.</item>
/// <item><b>وحارسُ الهويّة يُطفأ</b> — <c>App.xaml.cs</c>: <c>isFreshInstall = adminUsername
/// is not null</c>، والمعالجُ يملؤه. <b>فـ<c>DescribeIdentityProblemAsync</c> لا يُستدعى في
/// الإقلاعة التي يكون فيها الملفُّ المحلّيُّ غيرَ جديرٍ بالثقة أصلاً</b>.</item>
/// </list>
///
/// <para><b>والرفضُ يُغلق الثلاثةَ بالبناء</b>: متى لم يُشغَّل المعالجُ على تركيبٍ قائم لم
/// تُفقد الهويّةُ ولم يُطفأ الحارس.</para>
///
/// <para><b>وغيابُ الملفّ يبقى أوّلَ تشغيل</b> — فالتمييزُ بين «لا شيءَ هنا» و«هنا شيءٌ لا
/// أفهمه» هو كلُّ الإصلاح.</para>
/// </summary>
public class JsonSetupStateStore : ISetupStateStore
{
    /// <summary>
    /// ‏(ن-٥٠) <b>صيغةُ الملفّ الحاليّة.</b> والملفُّ القديمُ بلا وسمٍ <b>هو الصيغةُ ١ نفسُها</b>
    /// شكلاً، فيُقرأ كما هو. <b>ووسمٌ أحدثُ ممّا نعرف يُرفَض ولا يُخمَّن</b>.
    ///
    /// <para>وقِيس أنّ إضافةَ هذا الحقل <b>لا تكسر النسخَ المشحونة</b>: القارئُ القديم يتجاهل
    /// الأعضاءَ المجهولة، فالملفُّ يبقى مقروءاً للبناءَين معاً.</para>
    /// </summary>
    public const int CurrentSchemaVersion = 1;

    private static readonly JsonSerializerOptions Options = new() { WriteIndented = true };
    private readonly string _filePath;

    public JsonSetupStateStore()
        : this(Path.Combine(
            Brqosh.Infrastructure.Persistence.BrqoshDataRoot.Path(), "setup.json"))
    {
    }

    /// <summary>‏مسارٌ صريح — للاختبارات، فلا تُكتب حالةُ اختبارٍ في جذر بيانات الجهاز.</summary>
    public JsonSetupStateStore(string filePath)
    {
        _filePath = filePath;
        var directory = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrEmpty(directory))
            Directory.CreateDirectory(directory);
    }

    public SetupState? Load()
    {
        // ‏**الغيابُ وحدَه أوّلُ تشغيل.**
        if (!File.Exists(_filePath))
            return null;

        SetupState? state;
        try
        {
            state = JsonSerializer.Deserialize<SetupState>(File.ReadAllText(_filePath));
        }
        catch (JsonException ex)
        {
            throw Unreadable(ex.Message, ex);
        }

        // ‏‏**و«null» نصّاً لا يرمي** — يُعيد المُفكِّكُ عدماً بلا استثناء، فكان يمرّ
        // ‏من تحت الالتقاط إلى معالج التهيئة. مقيس.
        if (state is null)
            throw Unreadable("‏محتواه «null» لا حالةَ إعداد", inner: null);

        if (state.SchemaVersion > CurrentSchemaVersion)
            throw Unreadable(
                $"‏صيغتُه {state.SchemaVersion} وهذا الإصدارُ يعرف {CurrentSchemaVersion} — "
                + "‏الأرجحُ أنّه كُتب بإصدارٍ أحدث. حدِّثِ البرنامجَ أو أعِدْ تسميةَ الملفّ",
                inner: null);

        // ‏**ورقمٌ خارج التعداد لا يرمي كذلك.** مقيس: `"Profile": 99` يُفكَّك إلى
        // ‏`(BusinessProfile)99` بلا شكوى، **ثمّ يموت بعيداً عن سببه**:
        // ‏`ModuleCatalog.ResolveModules` يفهرس قاموساً فيرمي `KeyNotFoundException`
        // ‏داخل بناء القائمة. **فيُمسَك هنا حيث يُقرأ، لا هناك حيث يُستعمَل.**
        if (!Enum.IsDefined(state.Profile))
            throw Unreadable($"‏نظامُ العمل فيه رقمٌ مجهول ({(int)state.Profile})", inner: null);

        return state;
    }

    /// <summary>
    /// ‏(ن-٥٠) <b>تُكتب جانباً ثمّ تُنقل باسمها</b> — على سابقة <c>BackupService</c>.
    ///
    /// <para>كانت <c>File.WriteAllText</c> مباشرةً: انقطاعُ كهرباءٍ في منتصفها يترك ملفّاً
    /// مقطوعاً، <b>وهو بعينُه المُدخَلُ الذي كان يصير «أوّلَ تشغيل»</b>. فالعطلان يتغذّيان.</para>
    ///
    /// <para><b>ولا التقاطَ لتنظيف المؤقّت</b>: التقاطٌ صامتٌ يُسقط بوّابةَ
    /// <c>SilentFailureTests</c> في المستودع، <b>وملفٌّ مؤقّتٌ باقٍ لا يضرّ</b> — الحفظُ
    /// التالي يقطعه.</para>
    /// </summary>
    public void Save(SetupState state)
    {
        var temp = _filePath + ".tmp";
        File.WriteAllText(temp, JsonSerializer.Serialize(state, Options));
        File.Move(temp, _filePath, overwrite: true);
    }

    private InvalidOperationException Unreadable(string why, Exception? inner) =>
        new($"‏ملفُّ الإعداد لا يُقرأ: {_filePath}\n{why}.\n\n"
            + "‏ولم يُغيَّر شيءٌ في الدفاتر — **وهي في قاعدة البيانات لا في هذا الملفّ**. "
            + "‏ولو عُومل الملفُّ «أوّلَ تشغيل» لعُرض معالجُ التهيئة فوق منشأةٍ قائمة، "
            + "‏فتُفقد هويّةُ الترخيص ويُطفأ حارسُ «لمن هذه الدفاتر؟».\n"
            + "‏أعِدْ تسميةَ الملفّ ليبدأ الإعدادُ من جديد، أو أصلِحْ محتواه.", inner);
}
