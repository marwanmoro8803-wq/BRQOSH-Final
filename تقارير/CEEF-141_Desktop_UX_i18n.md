# CEEF-141 — Desktop · UX · i18n · design-system

| الحقل | القيمة |
|-------|--------|
| **النطاق** | محور H |
| **التاريخ** | 2026-07-10 |
| **الحكم** | **ERP كثيف RTL ✅** · **i18n كامل ⏭** · **spec coverage ~30%** |

## قياس حي (i18n audit)

| المقياس | القيمة | المصدر |
|---------|--------|--------|
| XAML أسطر عربية | **1381** | `i18n-audit.log` |
| CS أسطر عربية | **1040** | نفس السكربت |
| LocaleManager.T | **81** | نفس السكربت |
| PageTitlesEn | **PRESENT** | NavigationCatalog |

**الفجوة:** G-27 foundation فقط — sweep كامل لم يُنفَّذ.

## design-system vs Desktop

| المقياس | القيمة |
|---------|--------|
| صفحات Desktop `*Page.xaml` | **67** |
| specs `design-system/pages/*.md` | **20** |
| تغطية spec | **~30%** |

**صفحات بلا spec مخصص:** CRM (3) · consolidated reports · intercompany · data quality · platform events · organizations · وغيرها.

## UX نقاط قوة

- `BrqoshTheme.xaml` + `EmptyStateCatalog` bilingual
- `GoldenPathUiMarkupTests` — 67+ markup assertions
- `ui-ux-pro-max-audit.ps1` PASS في الجلسة

## UX فجوات

| ID | Finding | P |
|----|---------|---|
| AUDIT-UX-001 | 1381 سطر XAML عربي ثابت | P1 |
| AUDIT-UX-002 | `expenses.md` spec بلا `ExpensesPage` | P2 |
| AUDIT-UX-003 | Bank transfer service بلا صفحة UI | P1 |
| AUDIT-UX-004 | `sales_returns` → CreditNotesPage (تسمية مربكة) | P2 |

```534:536:src/Brqosh.Desktop/MainWindow.xaml.cs
case "sales_returns":
    PageHost.Content = new Views.Pages.CreditNotesPage();
```

## درجة المحور: **B** (Desktop) · **C+** (i18n)
