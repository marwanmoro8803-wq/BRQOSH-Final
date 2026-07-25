# CEEF-038 — UI UX Pro Max (تصميم A→Z داخل المستودع)

**التاريخ:** 2026-07-09  
**النطاق:** تطبيق منهجية UI UX Pro Max v2.0 على BRQOSH Desktop (WPF)  
**الحالة:** ✅ مغلق داخل المستودع (بدون CLI خارجي — توليد Design System يدوي/موثّق)

---

## 1. الهدف

تنفيذ طبقة تصميم احترافية لـ ERP مالي سعودي وفق anti-patterns المالية في UI UX Pro Max:

- بدون emoji كأيقونات
- KPI Bento data-dense
- Focus ring للوحة المفاتيح
- Design System MASTER + page overrides

---

## 2. المخرجات

| Artifact | المسار |
|----------|--------|
| Design System MASTER | `design-system/MASTER.md` |
| Dashboard override | `design-system/pages/dashboard.md` |
| Shell override | `design-system/pages/shell.md` |
| Login override | `design-system/pages/login.md` |
| Nav monograms | `src/Brqosh.Desktop/Navigation/NavIconFactory.cs` |
| KPI builder | `src/Brqosh.Desktop/Ui/KpiMetricCardBuilder.cs` |
| Theme tokens | `src/Brqosh.Desktop/Themes/BrqoshTheme.xaml` |
| Audit script | `scripts/ui-ux-pro-max-audit.ps1` |
| دليل v2.0 | `docs/UI_UX_DESIGN_SYSTEM.md` |

---

## 3. تغييرات UI رئيسية

### 3.1 التنقل (MainWindow)

- `NavigationCatalog`: إزالة حقل `Icon` emoji — `(Key, TitleAr)` فقط
- أزرار التنقل: `NavIconFactory.BuildContent` — Monogram badge 28×28
- زر المظهر: «داكن» / «فاتح» بدل 🌙/☀️

### 3.2 لوحة التحكم

- `UniformGrid` → `WrapPanel` (Bento responsive)
- 10 بطاقات KPI عبر `KpiMetricCardBuilder`
- Monogram + شريط accent + لاحقة «ر.س» للعملات
- `LoadingStatePanel` أثناء التحميل

### 3.3 Theme

- `AppFocusVisual` — حلقة Primary 2px
- أنماط: `KpiMetricCard`, `KpiMonogramBadge`, `NavMonogramBadge`, `LoadingStatePanel`, `EmptyStatePanel`
- `MotionDurationFast/Normal` (150/200ms — موثّق)

---

## 4. دليل تشغيل

```powershell
cd C:\Users\qc\Projects\BRQOSH

# فحص UI UX Pro Max
.\scripts\ui-ux-pro-max-audit.ps1

# بناء Desktop (أغلق Brqosh.Desktop.exe إن كان يعمل)
dotnet build src\Brqosh.Desktop\Brqosh.Desktop.csproj -c Release

# اختبارات الحل
$env:BRQOSH_SKIP_STRESS="1"; $env:BRQOSH_TEST="1"
dotnet test Brqosh.sln -c Release
```

---

## 5. نتائج التحقق

| الفحص | النتيجة |
|-------|---------|
| `ui-ux-pro-max-audit.ps1` | **PASS** (16/16 checks) |
| `dotnet build` Desktop Release | **PASS** (0 warnings) |
| `dotnet test` (`BRQOSH_SKIP_STRESS=1`) | **245/245 PASS** (Domain 58 · Application 164 · Api 23) |

---

## 6. ⏭ خارج النطاق

- تثبيت `ui-ux-pro-max-cli` من npm (محظور/غير مطلوب — MASTER مولَّد يدوياً بنفس القواعد)
- مراجعة بصرية VM/MSI (O-03)
- Tab order audit لكل صفحة (~80 صفحة) — مستمر

---

## 7. الخطوة التالية

- CEEF-039: EmptyStatePanel تطبيق على صفحات CRUD فارغة (اختياري)
- أو Global Platform / O-01…O-05 حسب `docs/مهم`
