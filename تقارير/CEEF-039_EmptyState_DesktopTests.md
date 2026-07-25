# CEEF-039 — Empty State + Desktop.Tests + Keyboard Nav (إغلاق UI/O-06 جزئي)

**التاريخ:** 2026-07-09  
**النطاق:** إكمال ما تبقى من CEEF-038 + O-06 Desktop.Tests  
**الحالة:** ✅ مغلق داخل المستودع

---

## 1. Empty State (كل صفحات القائمة)

| المكون | المسار |
|--------|--------|
| Catalog | `src/Brqosh.Desktop/Ui/EmptyStateCatalog.cs` |
| Helper | `src/Brqosh.Desktop/Ui/EmptyStateHelper.cs` |
| Hook | `UiTestAutomation.PublishGridCount` → `EmptyStateHelper.Sync` |
| Script | `scripts/apply-empty-state-grid-count.py` (27 صفحة) |
| Override | `design-system/pages/list-crud.md` |

---

## 2. Keyboard Navigation (a11y)

| البند | التطبيق |
|-------|---------|
| Shell header TabIndex | `KeyboardNavigationHelper.ApplyShellHeader` في MainWindow |
| UserControl default | `KeyboardNavigation.TabNavigation=Local` في BrqoshTheme |
| Toolbar helper | `KeyboardNavigationHelper.ApplyToolbarTabOrder` (جاهز للصفحات) |

---

## 3. Brqosh.Desktop.Tests (O-06)

| الاختبار | العدد |
|----------|-------|
| NavIconGlyphsTests | 5 |
| EmptyStateCatalogTests | 2 |
| NavigationCatalogTests | 2 |
| **المجموع Desktop** | **9** |

---

## 4. إصلاحات اختبار

- `ManufacturingTests`: عزل `SecurityContext` (منع تسريب Viewer من اختبارات parallel)

---

## 5. CI

- `ui-ux-pro-max-audit.ps1` في `.github/workflows/ci.yml`
- `Brqosh.Desktop.Tests` في الحل

---

## 6. دليل تشغيل

```powershell
cd C:\Users\qc\Projects\BRQOSH
.\scripts\ui-ux-pro-max-audit.ps1
.\scripts\apply-empty-state-grid-count.py   # عند إضافة صفحات جديدة
$env:BRQOSH_SKIP_STRESS="1"; $env:BRQOSH_TEST="1"
dotnet test Brqosh.sln -c Release
dotnet build src\Brqosh.Desktop\Brqosh.Desktop.csproj -c Release
```

---

## 7. نتائج التحقق

| الفحص | النتيجة |
|-------|---------|
| ui-ux-pro-max-audit | PASS |
| dotnet build Release | PASS |
| dotnet test | **254/254 PASS** (Domain 58 · Application 164 · Api 23 · Desktop 9) |

---

## 8. ⏭ ما يبقى (لا يُغلق بالكود)

| البند | السبب |
|-------|--------|
| O-01…O-05 | ZATCA/SOCPA/MSI/1M/ZAP حي |
| O-06 coverlet Desktop WPF LOC ≥75% | UI غير قابل للcoverlet بمعنى LOC؛ Desktop.Tests يغطي UI static |
| O-08 SAMA حي | feed رسمي |
| O-09…O-13 | SaaS/Cloud/BI/LLM/Mobile |

---

## 9. CEEF-038 + CEEF-039 = UI UX Pro Max A→Z داخل المستودع ✅
