# CEEF-108 — W3 Configuration Engine SA (v1)

| الحقل | القيمة |
|-------|--------|
| **النطاق** | V12-3 — قوالب دولة/قطاع · تهيئة الشركة |
| **التاريخ** | 2026-07-09 |
| **الحكم** | **W3 v1 ✅ داخل المستودع** |

## الملفات
| المكوّن | المسار |
|---------|--------|
| قالب السعودية | `config/templates/countries/saudi-arabia.json` |
| قالب المقاولات | `config/templates/industries/construction.json` |
| العقد | `IConfigurationTemplateLoader` · `Setup/IConfigurationTemplateLoader.cs` |
| التنفيذ | `Infrastructure/Setup/ConfigurationTemplateLoader.cs` |
| التهيئة | `DatabaseInitializer` — دمج قالب دولة + قطاع |
| الواجهة | `SetupWizardWindow` · `CompanySettingsPage` |
| الاختبارات | `ConfigurationTemplateTests.cs` |

## الدليل
- `dotnet test tests/Brqosh.Application.Tests --filter ConfigurationTemplate`
- السعودية افتراضية (`saudi-arabia`) · `BusinessProfile` يطابق JSON القطاع
- القطاعات الأخرى (General/Manufacturing/Retail) — بدائل مدمجة حتى إضافة JSON لاحقاً

## ⏭ خارج النطاق (v1)
- قوالب دول إضافية · تغيير القطاع بعد الإعداد · retention % في كيان الشركة
