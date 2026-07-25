# BRQOSH — تحليل فجوات الإطلاق السوقي والمنافسة (مجلس مستقل)

**التاريخ:** 2026-07-09 · **آخر تحديث:** 2026-07-10 — **CEEF-136** (plan reconciled)  
**المنهج:** فحص كل الوثائق + الكود الفعلي + نتائج الاختبارات  
**الحكم:** صريح بلا تنميق

> **تنبيه:** G-06/G-07 مغلقان CEEF-120 · G-08/G-09/G-69 مغلقان CEEF-129/130.  
> **الواقع الحالي (2026-07-10): 458/458 PASS** — دليل: `dist/evidence/CEEF-131-135/dotnet-test.log`.

---

## 0. ملخص تنفيذي — أين BRQOSH فعلاً اليوم؟

| السوق | جاهزية الكود | جاهزية الإنتاج الحي | ملاحظة (محدّثة CEEF-120) |
|-------|-------------|---------------------|--------|
| **تجربة سعودية (Pilot SMB)** | **~75%** | **~40%** | لا OTP · **458/458 PASS** · CRM MVP · OpenAPI ✅ |
| **سوق سعودي واسع** | **~62%** | **~30%** | CRM MVP ✅ · عروض/أوامر UI ✅ · لا POS مرخّص |
| **خليجي (UAE/Qatar/Bahrain/Kuwait/Oman)** | **~18%** | **~2%** | VAT matrix CEEF-133 · **لا e-invoicing حي** |
| **عالمي (SaaS)** | **~5%** | **~0%** | stubs فقط · لا billing · لا HA |

### الحالة التشغيلية الحقيقية (مُحدَّثة 2026-07-10 · CEEF-120)

```
Tests:     Domain 64/64 · Application 258/258 · Api 27/27 · Desktop 109/109
Total:     458/458 PASS — 0 FAIL
Evidence:  dist/evidence/CEEF-131-135/dotnet-test.log
Coverage:  Application ≥ 80.64% (بوابة ≥75%)
```

**G-06 (9 اختبارات فاشلة) أُغلق** ضمن V12-FIX (خطة V12 بند `v12-fix-9-failures` = completed) — CEEF-104/115/116.

---

## 1. تحقق V12 (بند ببند)

| البند | الادعاء في الخطة | الواقع في الكود | الاختبارات | الحكم |
|-------|-----------------|-----------------|-----------|-------|
| **W0 Governance** | `completed` | CEEF-101 موجود · OPEN_ITEMS محدّث | — | ✅ مغلق |
| **W1-A PostingOrchestrator** | `completed` | `PostingOrchestrator.cs` + `SaudiPostingPolicy.cs` | **PASS** (PostingPolicyTests) | ✅ مغلق CEEF-102 |
| **W1-B ImmutableAudit** | `completed` | `AuditLogImmutabilityInterceptor.cs` | PASS | ✅ CEEF-103 |
| **W1-C GoldenTests** | `completed` | `GoldenAccountingIntegrityTests.cs` | **PASS** 8/8 | ✅ CEEF-104 |
| **W2-A Workflow** | `completed` | `WorkflowEngine.cs` | PASS | ✅ CEEF-105 |
| **W2-B MDM** | `completed` | `MasterDataGovernanceService.cs` | PASS | ✅ CEEF-106 |
| **W2-C Treasury** | `completed` | `CashFlowForecastService` | PASS | ✅ CEEF-107 |
| **W3 ConfigEngine** | `completed` | `ConfigurationTemplateLoader.cs` | PASS | ✅ CEEF-108 |
| **W4 CleanArch+API** | `completed` | `DesktopAccountingFacade` · **0 new Service** | DesktopDiGuard PASS | ✅ CEEF-109/122/124 |
| **W5 LIFECYCLE** | `completed` | MFA · Quotation · BankTransfer | PASS | ✅ CEEF-113+ |

**الحكم (محدّث CEEF-136):** V12 **مغلق** — **458/458 PASS** · Desktop DI **كامل** · CEEF-120…136 in-repo.

> **تحديث CEEF-124 (2026-07-10):** W4 **مغلق** — كل Desktop يستخدم `DesktopAccountingFacade` + DI. بوابة `DesktopDiGuardTests.EntireDesktop_HasZeroDirectServiceConstruction` PASS.

---

## 2. جدول الفجوات الشامل

| # | الفجوة | الفئة | في مهم؟ | في V12؟ | في OPEN_ITEMS؟ | الدليل في الكود | الأولوية | الجهد |
|---|--------|-------|---------|---------|---------------|----------------|----------|-------|
| **G-01** | ZATCA OTP إنتاجي + 10 فواتير مقبولة | Saudi Launch | ✅ | ✅ EXT | ✅ O-01 | `ZatcaCsidWizardWindow` stub · لا evidence/ | **P0** | 1-2w خارجي |
| **G-02** | مراجعة SOCPA طرف ثالث | Saudi Launch | ✅ | ✅ EXT | ✅ O-02 | `O-02_SOCPA_REVIEWER_PACKAGE.md` exists | **P0** | خارجي |
| **G-03** | MSI على VM نظيفة + توقيع رقمي | Saudi Launch | ✅ | ✅ EXT | ✅ O-03 | `verify-msi-build.ps1` exists · لا code signing cert | **P0** | 1w |
| **G-04** | Load Test 1M قيد | Saudi Launch | ✅ | ✅ EXT | ✅ O-04 | `run-stress-benchmark.ps1` · tested 5k only | **P1** | 2d خارجي |
| **G-05** | OWASP ZAP pen test حي | Security | ✅ | ✅ EXT | ✅ O-05 | `run-zap-baseline.ps1` · لا staging URL | **P1** | 1w |
| **G-06** | ~~9 اختبارات فاشلة~~ **مغلق CEEF-120** — 458/458 PASS | Technical Debt | ❌ | ✅ V12-FIX | ❌ | `dist/evidence/CEEF-136-plan-reconcile/dotnet-test.log` | ~~P0~~ **✅** | مغلق |
| **G-07** | ~~V12 plan status outdated~~ **مغلق** — كل بنود V12 `completed` | Technical Debt | ❌ | ✅ | ❌ | `.cursor/plans/مهم_v12_*.plan.md` frontmatter | ~~P0~~ **✅** | مغلق |
| **G-08** | ~~عروض أسعار بدون UI~~ **مغلق CEEF-130** | UX/Product | ✅ مهم §4.31 | ✅ W5 | ✅ | `SalesQuotationsPage` + `SalesQuotationDialog` + nav | ~~P1~~ **✅** | مغلق |
| **G-09** | ~~أوامر بيع بدون كود~~ **مغلق CEEF-130** | UX/Product | ✅ مهم §4.32-33 | ✅ W5 | ✅ | `SalesOrdersPage` + `SalesOrderDialog` + فوترة | ~~P1~~ **✅** | مغلق |
| **G-10** | ~~CRM كامل بدون كود~~ **MVP مغلق CEEF-132** | UX/Product | ✅ مهم E-08 | ✅ MVP | ❌ | `CrmLeads/Opportunities/Activities` + `CrmService` | ~~P2~~ **🔶 MVP ✅** | حملات ⏭ |
| **G-11** | ~~UAE صفر كود~~ **مصفوفة VAT foundation CEEF-133** | Gulf Regional | ❌ | 🔶 stub | ❌ | `TaxJurisdiction` + AE 5% · **لا FTA حي** | **P1** | API ⏭ |
| **G-12** | Qatar VAT matrix stub | Gulf Regional | ❌ | 🔶 stub | ❌ | `VatJurisdictionProfiles` | **P2** | API ⏭ |
| **G-13** | Bahrain VAT matrix stub | Gulf Regional | ❌ | 🔶 stub | ❌ | BH 10% في المصفوفة | **P2** | API ⏭ |
| **G-14** | Kuwait VAT matrix stub | Gulf Regional | ❌ | 🔶 stub | ❌ | KW 0% في المصفوفة | **P2** | API ⏭ |
| **G-15** | Oman VAT matrix stub | Gulf Regional | ❌ | 🔶 stub | ❌ | OM 5% في المصفوفة | **P2** | API ⏭ |
| **G-16** | GOSI تكامل API حي | Saudi Launch | ✅ مهم C-04 | ❌ | ❌ | `SaudiChartOfAccounts` GOSI accounts only · لا API client | **P1** | 2-4w |
| **G-17** | Mudad تكامل API (نقل رواتب) | Saudi Launch | ❌ | ❌ | ❌ | **صفر** | **P1** | 2-3w |
| **G-18** | Qiwa تكامل API (عقود عمل) | Saudi Launch | ❌ | ❌ | ❌ | **صفر** | **P2** | 2-3w |
| **G-19** | WPS ملف حماية أجور | Saudi Launch | ❌ | ❌ | ❌ | **صفر** (deps.json mention only) | **P1** | 1-2w |
| **G-20** | Multi-tenant SaaS كامل (عزل/billing/provisioning) | Global | ✅ مهم §7 | ❌ | ✅ O-09 | `TenantId` stub + `TenantProvisioningService` — **no Stripe, no billing, no onboarding** | **P2** | 8-16w |
| **G-21** | Stripe/payment gateway تكامل | Global | ✅ مهم §7 | ❌ | ✅ O-09 | **صفر** | **P2** | 2-4w |
| **G-22** | HA/DR cloud (auto-scale, geo-redundancy) | Global | ✅ مهم §7 | ❌ | ✅ O-10 | `/health/live` + `/health/ready` only | **P2** | 6-12w |
| **G-23** | Mobile app (iOS/Android) | Global | ✅ مهم §7 | ❌ | ✅ O-13 | **صفر** | **P2** | 16-24w |
| **G-24** | Full Web Portal (Blazor/SPA) | Global | ✅ مهم §7 | ❌ | ✅ O-13 | **WPF monolith only** | **P2** | 16-24w |
| **G-25** | تكامل بنوك (كشوف آلية/دفع إلكتروني) | Saudi Launch | ✅ مهم §4.API | ❌ | ❌ | CSV import فقط · لا Open Banking | **P1** | 4-8w |
| **G-26** | تكامل Salla/Zid/Shopify | Ops/GTM | ❌ | ❌ | ❌ | **صفر** | **P2** | 2-4w لكل |
| **G-27** | ~~i18n بدون جرد~~ **foundation CEEF-134** — nav EN/AR + audit script | UX/Product | 🔶 مهم §1.7 | 🔶 nav | ❌ | `PageTitlesEn` + `i18n-coverage-audit.ps1` · XAML sweep ⏭ | **P1** | 2-3w sweep |
| **G-28** | SOC2 Type II | Compliance | ❌ | ❌ | ❌ | **صفر** | **P2** | 6-12m |
| **G-29** | ISO 27001 | Compliance | ❌ | ❌ | ❌ | **صفر** | **P2** | 6-12m |
| **G-30** | PDPL (حماية بيانات شخصية سعودي) | Compliance | 🔶 مهم C-05 | ❌ | ❌ | ذكر في ROADMAP فقط | **P1** | 4-8w |
| **G-31** | دعم فني عملاء (SLA/helpdesk/ticketing) | Ops/GTM | ❌ | ❌ | ❌ | **صفر** — لا بنية دعم | **P0** للإطلاق | 4-8w |
| **G-32** | خطة تسعير + باقات | Ops/GTM | ❌ | ❌ | ❌ | **صفر** | **P0** للإطلاق | 1-2w |
| **G-33** | مواد تدريب (فيديوهات/أدلة مستخدم) | Ops/GTM | ❌ | ❌ | ❌ | `installer/README.md` فقط | **P0** للإطلاق | 4-8w |
| **G-34** | خطة تنفيذ عملاء (onboarding flow) | Ops/GTM | ❌ | ❌ | ❌ | **صفر** | **P1** | 2-4w |
| **G-35** | Observability production-grade (APM/tracing/alerts) | Technical Debt | ✅ مهم §Obs | 🔶 | ✅ O-11 | Serilog exists · `/health` exists · **لا APM/tracing/alerting** | **P1** | 2-4w |
| **G-36** | Data Intelligence / BI dashboard حي | Technical Debt | ✅ مهم §5 | ❌ | ✅ O-11 | `RulesEngine-v1` stub · لا data warehouse | **P2** | 8-16w |
| **G-37** | AI CFO / LLM حي | Technical Debt | ✅ مهم §6 | ❌ | ✅ O-12 | `FinancialIntelligenceService` rules only · لا LLM | **P2** | 8-16w |
| **G-38** | Backup/DR runbooks للعملاء | Ops/GTM | ❌ | ❌ | ❌ | `backup-postgres.ps1` exists · **لا دليل عميل** | **P1** | 1-2w |
| **G-39** | أدوات ترحيل بيانات (من أنظمة أخرى) | UX/Product | ❌ | ❌ | ❌ | **صفر** — لا import wizard | **P1** | 4-8w |
| **G-40** | تقارير مطبوعة/PDF — تعريب كامل | UX/Product | ❌ | ❌ | ❌ | PDF generators exist (5 files) · **مستوى التعريب غير مُدقَّق** | **P1** | 1-2w |
| **G-41** | التقويم الهجري شامل (DatePicker + reports + periods) | UX/Product | ✅ مهم U-05 | 🔶 | ❌ | `HijriCalendarHelper.cs` exists · **لا DatePicker هجري في الحوارات** | **P1** | 1-2w |
| **G-42** | POS — شهادة ZATCA للأجهزة | Saudi Launch | ❌ | ❌ | ❌ | POS exists · **لا certification flow** | **P1** | 2-4w |
| **G-43** | IFRS 15 عمق (5 خطوات + متغير consideration) | Saudi Launch | ✅ مهم A-04 | 🔶 | ❌ | `ProgressBillingIfrs15` + `Ifrs15CostToCostTests` · **billing + cost-to-cost فقط** | **P1** | 2-4w |
| **G-44** | Manufacturing MRP (تخطيط إنتاج كامل) | UX/Product | ✅ مهم E-04 | ❌ | ❌ | BOM + WO basic only · لا MRP/ReorderPoint/SafetyStock | **P2** | 6-12w |
| **G-45** | Payroll WPS file export | Saudi Launch | ❌ | ❌ | ❌ | **صفر** | **P1** | 1-2w |
| **G-46** | E-invoicing B2C vs B2B differentiation production | Saudi Launch | 🔶 مهم §5 | ❌ | ❌ | `IsSimplified` flag exists · **لا production validation per type** | **P1** | 1-2w |
| **G-47** | API rate limits / throttling | Security | ❌ | ❌ | ❌ | **صفر** — `IdempotencyStore` exists but no rate limiter | **P1** | 1-2w |
| **G-48** | License enforcement in field (expiry/feature gates) | Saudi Launch | ❌ | ❌ | ❌ | `RsaLicenseValidator` + `LicenseStore` exist · **لا runtime feature gating** | **P1** | 1-2w |
| **G-49** | WCAG/Accessibility | UX/Product | ✅ مهم U-08 | ❌ | ❌ | `KeyboardNavigationHelper.cs` only · **لا AutomationPeer/WCAG compliance** | **P2** | 4-8w |
| **G-50** | Offline mode (disconnected operation) | UX/Product | ❌ | ❌ | ❌ | **صفر** | **P2** | 8-16w |
| **G-51** | قيود استحقاق / Accruals | Technical Debt | ✅ LIFECYCLE-73 | 🔜 | ❌ | **صفر كود** | **P2** | 1-2w |
| **G-52** | قيود متكررة / Recurring JE | Technical Debt | ✅ مهم A-10 | ❌ | ❌ | **صفر** | **P2** | 1-2w |
| **G-53** | ميزانيات + تحليل انحراف | Technical Debt | ✅ مهم A-09 | ❌ | ❌ | **صفر** | **P2** | 2-4w |
| **G-54** | قائمة تدفقات نقدية (مباشر/غير مباشر) | Technical Debt | ✅ مهم A-11 | ❌ | ❌ | `CashFlowForecastService` ≠ cash flow statement | **P1** | 2-4w |
| **G-55** | أرشفة مستندات وقيود (LIFECYCLE-99) | Technical Debt | ✅ LIFECYCLE-99 | 🔜 | ❌ | **صفر** — لا ArchivalService | **P2** | 1-2w |
| **G-56** | تجميد/تصفية شركة (LIFECYCLE-100) | Technical Debt | ✅ LIFECYCLE-100 | 🔶 | ❌ | `CompanyOperationalMode` exists · **لا freeze UI/archive flag** | **P2** | 1-2w |
| **G-57** | Serial/Lot tracking مخزون (LIFECYCLE-83) | UX/Product | ✅ LIFECYCLE-83 | 🔜 | ❌ | **صفر** | **P2** | 3-6w |
| **G-58** | Version History مستندات (LIFECYCLE-27) | UX/Product | ✅ LIFECYCLE-27 | 🔜 | ❌ | **صفر** | **P2** | 2-4w |
| **G-59** | مرفقات PDF/ملفات (LIFECYCLE-26) | UX/Product | ✅ LIFECYCLE-26 | 🔶 | ❌ | ZATCA XML ✅ · **لا attachment storage عام** | **P2** | 1-2w |
| **G-60** | ~~OpenAPI صفر~~ **مغلق CEEF-131** | Technical Debt | ❌ | ✅ | ❌ | `/swagger` + `OpenApiDocumentTests` | ~~P1~~ **✅** | مغلق |
| **G-61** | CI/CD pipeline كامل (build→test→SAST→sign→artifact) | Technical Debt | ✅ مهم §7.1 | 🔶 | ❌ | `ci.yml` exists · **لا MSI publish/sign step** | **P1** | 1-2w |
| **G-62** | Code signing certificate | Saudi Launch | ✅ مهم I-04 | ❌ | ❌ | **صفر** | **P1** | 1w |
| **G-63** | WPF monolith → multi-platform strategy | Technical Debt | ✅ مهم ADR | ❌ | ❌ | ADR says "keep WPF for now" · **لا migration path documented** | **P2** | strategic |
| **G-64** | Security retention policies (LIFECYCLE-9) | Security | ✅ LIFECYCLE-9 | 🔜 | ❌ | **صفر** | **P2** | 1-2w |
| **G-65** | Batch posting (LIFECYCLE-79) | UX/Product | ✅ LIFECYCLE-79 | 🔜 | ❌ | **صفر** | **P2** | 1-2w |
| **G-66** | عمولات مبيعات (LIFECYCLE-39) | UX/Product | ✅ LIFECYCLE-39 | 🔜 | ❌ | **صفر** | **P2** | 2-4w |
| **G-67** | سجل طلبات تسليم (LIFECYCLE-34) | UX/Product | ✅ LIFECYCLE-34 | 🔜 | ❌ | **صفر** | **P2** | 1-2w |
| **G-68** | Scrap/write-off مخزون (LIFECYCLE-89) | UX/Product | ✅ LIFECYCLE-89 | 🔜 | ❌ | **صفر** | **P2** | 1w |
| **G-69** | ~~منشآت متعددة القطاعات بدون UI~~ **مغلق CEEF-129** | UX/Product | ✅ LIFECYCLE-1/10 | ✅ W3 | ✅ | `OrganizationsPage` + `OrganizationProvisionerWindow` · 8 قطاعات · تبديل شركة | ~~P1~~ **✅** | مغلق |

**المجموع: 69 فجوة** — منها **11 P0** (بما فيها 4 O-gates + 6 إصلاحات فشل + governance) و **28 P1** و **30 P2**.

---

## 3. فجوات إضافية اكتُشفت (لم تُذكر في التقييم السابق)

| # | الفجوة | ملاحظة |
|---|--------|--------|
| G-39 | أدوات ترحيل بيانات | حرج لأي عميل ينتقل من نظام آخر |
| G-40 | تقارير PDF تعريب | مطبوعات ثنائية اللغة للعملاء |
| G-42 | POS certification | ZATCA تفرض اعتماد أجهزة POS |
| G-45 | WPS payroll export | مطلوب لحماية الأجور |
| G-48 | License enforcement runtime | بدونه لا يمكن تحصيل رسوم |
| G-51-59 | LIFECYCLE 🔜 items | 15+ خطوة مخطّطة لم تُنفَّذ |
| G-60 | OpenAPI docs | Developer Portal مفقود |
| G-62 | Code signing | SmartScreen يحظر MSI بدونه |
| G-64-68 | عمولات/تسليم/أرشفة/batch | حلقات أعمال ناقصة |
| **G-69** | ~~منشآت متعددة القطاعات~~ **مغلق CEEF-129** | `OrganizationsPage` + 8 profiles + Provisioner |

---

## 4. حالة LIFECYCLE-100 (ملخص)

| المجال | إجمالي الخطوات | ✅ | 🔶 | 🔜 | ⏭ | نسبة إنجاز |
|--------|---------------|----|----|----|----|-----------|
| البنية (1-10) | 10 | 3 | 6 | 1 | 0 | 30% |
| المحاسبة (11-20) | 10 | 6 | 4 | 0 | 0 | 60% |
| المستند (21-30) | 10 | 4 | 4 | 2 | 0 | 40% |
| المبيعات (31-40) | 10 | 3 | 2 | 5 | 0 | 30% |
| ZATCA (41-50) | 10 | 5 | 4 | 0 | 1 | 50% |
| المشتريات (51-60) | 10 | 5 | 4 | 0 | 1 | 50% |
| النقد (61-70) | 10 | 3 | 4 | 1 | 0 | 30% |
| القيود (71-80) | 10 | 4 | 2 | 4 | 0 | 40% |
| المخزون (81-90) | 10 | 4 | 4 | 2 | 0 | 40% |
| الإقفال (91-100) | 10 | 4 | 3 | 2 | 1 | 40% |
| **المجموع** | **100** | **41** | **37** | **17** | **3** | **41%** |

---

## 5. المراجع

- `docs/مهم` v4.0
- `.cursor/plans/مهم_v12_إغلاق_شامل_8c9973cd.plan.md`
- `docs/OPEN_ITEMS.md`
- `docs/EXECUTION_ROADMAP_MASTER.md`
- `design-system/LIFECYCLE-ROADMAP-100.md`
- `docs/DOMAIN_MAP.md`
- `docs/ROADMAP.md`
- `dotnet test` output 2026-07-09
