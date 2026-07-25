# BRQOSH ERP

نظام محاسبة ERP سعودي احترافي — إعادة بناء كاملة على .NET.

## نظرة عامة

برنامج Desktop لنظام Windows يبدأ باختيار **نظام العمل** (مقاولات / إنتاج / تسويق) ثم يفعّل الوحدات المناسبة، مع نواة محاسبية مشتركة وامتثال كامل لمتطلبات ZATCA (الفوترة الإلكترونية — المرحلة الثانية).

## المكدس التقني

| الطبقة | التقنية |
|---|---|
| اللغة | C# / .NET 9 |
| الواجهة | WPF + Fluent Design (RTL عربي كامل) |
| قاعدة البيانات | PostgreSQL (الإنتاج) · SQLite (تطوير/اختبار فقط) |
| الوصول للبيانات | EF Core (التقارير الثقيلة تُجمَّع في SQL عبر LINQ) |
| التقارير | QuestPDF |
| الفوترة الإلكترونية | ZATCA Phase 2 (UBL XML + توقيع EC P-256) |

## المعمارية

Clean Architecture مع وحدات (Modules) قابلة للتفعيل حسب نظام العمل:

```
src/
  Brqosh.Domain/          الكيانات وقواعد الأعمال (بدون تبعيات)
  Brqosh.Application/     حالات الاستخدام والخدمات
  Brqosh.Infrastructure/  قاعدة البيانات، ZATCA، التقارير
  Brqosh.Desktop/         واجهة WPF (MVVM)
  Modules/                 (أصداف مشاريع — الوحدات مطبَّقة فعلياً كأعلام ميزات
                            عبر Domain/Modules/ModuleCatalog، وكودها داخل Infrastructure)
tests/
  Brqosh.Domain.Tests/
  Brqosh.Application.Tests/
  Brqosh.Api.Tests/
  Brqosh.Desktop.Tests/
docs/
  architecture/           قرارات المعمارية (ADRs)
```

## المبادئ غير القابلة للتفاوض

1. **النواة المحاسبية (GL) لا تُعطَّل أبداً** — كل الوحدات تعتمد عليها.
2. **لا حذف فعلي للقيود** — Soft delete + Audit trail على كل عملية.
3. **الأداء هدف تصميمي**: إقلاع < 3 ثوانٍ، بحث في 5 مليون سجل < 1 ثانية.
4. **عربي RTL أولاً** — الإنجليزية لغة ثانية.
5. **الهجرات مسار الإنتاج حصراً** — تُولَّد على Npgsql؛ وSQLite يُهيَّأ من النموذج مباشرة.

## المتطلبات

- Windows 10/11 x64
- .NET 9 SDK
- PostgreSQL 17

## البناء والتشغيل

```powershell
dotnet build Brqosh.sln
dotnet run --project src/Brqosh.Desktop
```

## التحقق

```powershell
dotnet build Brqosh.sln
dotnet test Brqosh.sln                       # 561 اختباراً
dotnet run --project tools/MigrationProbe    # حارس PostgreSQL: هجرات + ترحيل + تزامن + أرصدة
pwsh -File scripts/ops/verify-ui-live.ps1 -Nav "فواتير المبيعات" -Dialog "فاتورة مبيعات جديدة"
```

آخر تدقيق شامل: [`تقارير/CEEF-211_تقرير_الإغلاق_النهائي_2026-07-25.md`](تقارير/CEEF-211_تقرير_الإغلاق_النهائي_2026-07-25.md) · البنود المفتوحة: [`docs/OPEN_ITEMS.md`](docs/OPEN_ITEMS.md)
