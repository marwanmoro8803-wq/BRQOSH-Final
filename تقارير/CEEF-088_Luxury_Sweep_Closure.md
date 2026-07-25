# CEEF-088 — V9-4: Luxury sweep — إغلاق الصفحات الرئيسية

| الحقل | القيمة |
|-------|--------|
| **النطاق** | جميع صفحات القائمة الرئيسية في Desktop |
| **التاريخ** | 2026-07-09 |
| **الحكم** | **V9-4 ✅** |

## التحقق
كل صفحة قائمة رئيسية (`*Page.xaml` في NavigationCatalog) تحتوي `PageTitleText` أو `GlassPanel`.

صفحات كانت بدون luxury وأُغلقت في V8–V9:
Users · Branches · Contracts · Boq · Subcontractors · Consolidated×3 · CompanySettings · CompanyGroups · ExchangeRates · Bom · WorkOrders · LandedCost · PosShifts · PosSession

## الاختبارات
**329 PASS** (58 Domain · 179 Application · 26 Api · **66 Desktop**)
