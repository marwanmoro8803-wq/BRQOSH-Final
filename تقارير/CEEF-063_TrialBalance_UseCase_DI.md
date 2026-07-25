# CEEF-063 — V4-4: Use Cases — موجة 3 (تقارير)

| الحقل | القيمة |
|-------|--------|
| **النطاق** | TrialBalancePage → ITrialBalanceQuery |
| **التاريخ** | 2026-07-09 |
| **الحكم** | **V4-4 ✅** |

## التغيير
- `TrialBalancePage.xaml.cs` يستخدم `AppServices.CreateInfrastructureScope()` + `ITrialBalanceQuery`
- PDF/CSV يحوّلان `TrialBalanceDto` عند الحاجة

## الاختبارات
293 PASS إجمالي (58 Domain · 179 Application · 26 Api · 30 Desktop)
