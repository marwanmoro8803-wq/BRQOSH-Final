# سجل البنود غير المغلقة — BRQOSH

**آخر تحديث:** 2026-07-25 (CEEF-200…211 · حملة تدقيق وإغلاق)  
**الغرض:** ذاكرة مشروع دائمة — لا تُحذف عند انتقال المرحلة  
**ذاكرة التطوير السريعة:** `docs/PROJECT-MEMORY.yaml` · `skill-lessons.yaml` · `LEARNING-INDEX.yaml`  
**قاعدة:** كل بند هنا **متعمد مؤجّل** بسبب خارجي أو استراتيجي، وليس «نسيان كود».

---

## 0. تعريف «مغلق» (4 مستويات — لا استثناء)

| المستوى | الرمز | المعنى | دليل مطلوب |
|---------|-------|--------|------------|
| كود | ✅ كود | منطق منفّذ في `src/` | مراجعة + build |
| اختبار | ✅ اختبار | اختبار/سيناريو يغطي السلوك | `dotnet test` PASS |
| تشغيل حي | ✅ حي | OTP · مراجع · VM · pen test | مجلد `dist/evidence/O-XX/` |
| خارجي | ⏭ | يعتمد طرف ثالث/بنية خارج المستودع | runbook + سبب موثّق |

**لا يُكتب «مغلق» أو «جاهز» إلا عند استيفاء المستوى المعلَن صراحة.**

---

## 1. من المراحل 1–6 + تدقيق تشغيلي

| # | البند | المرحلة الأصل | سبب عدم الإغلاق داخل المستودع | حالة السكلليت إن وُجد |
|---|--------|---------------|-------------------------------|------------------------|
| O-01 | ZATCA Production OTP حي + 10 فواتير مقبولة | 3 / Pilot | اعتماد هيئة + OTP | **`docs/ops/O-01_*`** · `run-external-gates.ps1` |
| O-02 | مراجعة SOCPA طرف ثالث | 3 | مراجع خارجي | **`docs/ops/O-02_*`** |
| O-03 | إثبات MSI على VM نظيفة + توقيع ميداني | 3 | تشغيل تشغيلي | **`verify-msi-build.ps1`** · `O-03_MSI_VM_RUNBOOK` |
| O-04 | Load test 1M قيد | M12 / 5 | أجهزة/زمن؛ أثبتنا 2k–5k | **`run-stress-benchmark.ps1`** |
| O-05 | OWASP ZAP / pen test حي | M12 | ماسح + شبكة | **`run-zap-baseline.ps1`** |
| O-06 | Coverlet ≥75% **LOC** على Desktop WPF | M12 | UI WPF لا يُقاس LOC بشكل ذي معنى — **قرار مقبول** | **Desktop.Tests 110** · بوابة مجمّعة ≥75% · **CEEF-037/039/116** |
| O-08 | FX translation + SAMA **حي** | 5أ / 7 | feed رسمي SAMA | **CEEF-026→034** chain جاهز |
| O-09 | Multi-tenant SaaS · Stripe · billing | 5ب / 7 | Stripe · multi-company/tenant | **CEEF-035 stub** |
| O-10 | Cloud HA / DR geo | 5ب / 7 | بنية سحابة | `/health/live` · `/health/ready` |
| O-11 | BI platform / Power BI | 5ب | مستودع تحليلي | `CEEF-021` |
| O-12 | AI CFO / LLM حي | 6 | مزوّد نموذج | `CEEF-022` |
| O-13 | Mobile / Blazor | 5ب / 7 | منتج منفصل | API v1 قراءة |

### أُغلق بالكود (لا يُعاد فتحه)

| # | البند | الدليل |
|---|--------|--------|
| ~~O-07~~ | BranchId شامل + منتقي جلسة | CEEF-024 · CEEF-025 |
| ~~O-14~~ | خمول + MustChangePassword | CEEF-024 |
| ~~O-15~~ | Visibility CRUD | CEEF-024 |
| ~~UI UX Pro Max~~ | Design System + KPI + Nav monograms | **CEEF-038** |
| ~~Empty State CRUD~~ | EmptyStateHelper + كل القوائم | **CEEF-039** |

---

## 1ب. حملة CEEF-200…211 (2026-07-25)

**التقرير النافذ:** [`تقارير/CEEF-211_تقرير_الإغلاق_النهائي_2026-07-25.md`](../تقارير/CEEF-211_تقرير_الإغلاق_النهائي_2026-07-25.md)
(CEEF-200 وCEEF-202 صارا تاريخيَّين.)

**أُغلق بالكود والاختبار (561/561، بناء 0/0):**

| # | البند | مستوى الإغلاق |
|---|--------|----------------|
| ح-1 | رمز OTP يُعرض على الشاشة → استيلاء على أي حساب | ✅ كود · ✅ اختبار |
| ح-2 | **تثبيت PostgreSQL نظيف كان مستحيلاً** (هجرات بأنواع SQLite) | ✅ كود · ✅ **حيّ** (`tools/MigrationProbe`) |
| ح-3 | حارس ثبات سجل التدقيق لا يعمل على مسار Desktop | ✅ كود · ✅ اختبار |
| ح-4 | 🔴 **الترحيل المحاسبي مكسور كلياً على PostgreSQL** (ابتلاع CancellationToken + توقيع pg_advisory_xact_lock خاطئ) | ✅ كود · ✅ **حيّ** |
| ع-R1 | الميزانية تختل عبر حدود السنة | ✅ كود · ✅ اختبار |
| ع-R2 | قائمة دخل سنة مقفلة تعود صفراً | ✅ كود · ✅ اختبار |
| ع-D1 | ضياع تحديثات أرصدة المخزون (رموز التزامن) | ✅ كود · ✅ **حيّ** (6 كتّاب متزامنين) |
| ع-D2 | حدث «شبح» عن مستند مرتدّ (نمط Outbox) | ✅ كود · ✅ اختبار |
| ع-D3 | الفولباك الصامت PostgreSQL→SQLite | ✅ كود · ✅ اختبار |
| ع-A1 | تسريب كل الشركات في وضع SaaS | ✅ كود · ✅ اختبار |
| ع-A4 | كاش Idempotency عابر للمستأجرين | ✅ كود · ✅ اختبار |
| ع-A5 | حدّ المعدل بعد المصادقة (تخمين بلا حدّ) | ✅ كود |
| ع-Z2 | `cac:TaxSubtotal` الإلزامي غائب | ✅ كود · ✅ اختبار · ⏭ **لم يُختبر ضد Sandbox** |
| — | الفئات الضريبية (S/Z/E/O) + وصلها بالواجهة | ✅ كود · ✅ اختبار · ✅ **حيّ** |
| — | تلويث بيانات المستخدم من الاختبارات (`BrqoshDataRoot`) | ✅ كود · ✅ **حيّ** |
| — | الدفع المختلط في POS يُقيَّد كله على الصندوق | ✅ كود · ✅ اختبار |
| — | أداء الإقلاع ودفتر الأستاذ | ✅ كود · ✅ اختبار |

**بنود جديدة مفتوحة (تحلّ محلّ ما أُغلق):**

| # | البند | سبب عدم الإغلاق |
|---|--------|------------------|
| O-16 | اعتماد ZATCA حيّ: توقيع ع-Z1، وسوم QR 8/9، عنوان البائع و CRN | ⏭ بيئة الهيئة بشهادات |
| O-17 | مفاتيح API لكل مستأجر + الدور الافتراضي Administrator | ⏭ قرار عقد — تكاملات قائمة واختبار يؤكد السلوك |
| O-18 | تشفير `MfaSecret` | ⏭ يحتاج مفتاح منشأة لا DPAPI محلي ([السبب](../تقارير/CEEF-211_تقرير_الإغلاق_النهائي_2026-07-25.md)) |
| ~~O-19~~ | ~~سلسلة تجزئة لسجل التدقيق~~ | ✅ **أُغلق** (CEEF-212): كود · اختبار — العبث عبر SQL خام صار مكشوفاً |
| O-20 | `tools/UiAudit` لا يكتمل (عائق FlaUI/UIA3 COM) | ⏭ بيئيّ — البديل `scripts/ops/verify-ui-live.ps1` يعمل |
| ~~O-21~~ | ~~الإقرار الضريبي يعتمد الحالة الجارية للمستند~~ | ✅ **أُغلق** (CEEF-219): كود · اختبار · **حيّ** (`MigrationProbe`) — الإقرار يعكس وقائع الفترة |
| ~~O-22~~ | ~~نافذة «An item with the same key has already been added» على لوحة التحكم~~ | ✅ **أُغلق** (CEEF-220): كود · اختبار — `ConditionalWeakTable.Add` على مفتاح موجود في `XamlUiLocalizer` |
| ~~O-23~~ | ~~صفحة إعدادات المنشأة لا تفتح (`BorderSubtleBrush` غير معرَّف)~~ | ✅ **أُغلق** (CEEF-221): كود · اختبار (`ResourceKeyResolutionTests`) · **حيّ** (مسح 60 صفحة) |
| ~~O-24~~ | ~~نصّ الجداول غير مقروء في الثيم الداكن — تباين 1.05~~ | ✅ **أُغلق** (CEEF-222): كود · اختبار (`DataGridContrastTests`) · **حيّ** (17.13 مقيساً على الشاشة) |
| ~~O-25~~ | ~~الوضع الإنجليزيّ نصف عامل~~ | ✅ **أُغلق** (CEEF-223): كود · اختبار (`NavigationEnglishCoverageTests` + `BilingualRegistryQualityTests`) · **حيّ** — `BuildNavigation()` لم يكن يُستدعى عند التبديل، و188/799 مدخلاً مُشوَّهاً. القياس: 116–134 نصّاً عربياً للصفحة ← **22** |
| O-26 | لوحة برقوش AI تُولّد رؤاها ومقترحاتها **بالعربية دائماً** — 60/60 صفحة في الوضع الإنجليزيّ | 🔶 **مرصود بالقياس** (CEEF-223) — يحتاج نصوصاً ثنائية في مولّد الرؤى لا في الواجهة |
| O-27 | التاريخ في الشريط العلويّ يُنسَّق بثقافة عربية في الوضع الإنجليزيّ | 🔶 **مرصود بالقياس** (CEEF-223) |

---

## 2. المرحلة الحالية

1. **V12 + V13 + CEEF-120…138 in-repo مغلق** — تدقيق A-to-Z مستقل CEEF-138
2. **تنفيذ حي O-01…O-05** · **تصليب API P0** — `docs/ops/WAKE_UP_CHECKLIST.md`

## 2ب. ما بعد CEEF-138 — الإطلاق السوقي

| الأولوية | البند | الحالة |
|----------|-------|--------|
| P0 | API auth + tenant default-deny | 🔶 CEEF-138 finding |
| P0 | O-01…O-05 حي | ⏭ خارجي |
| P1 | G-27 XAML sweep كامل | ✅ CEEF-145 · `XamlUiLocalizer` + registry 100% |
| P1 | G-16 GOSI · G-19 WPS | ⏭ |
| P2 | V13-C SaaS · V13-E compliance | ⏭ |

- **تفصيل:** [`docs/MARKET_LAUNCH_GAP_ANALYSIS.md`](MARKET_LAUNCH_GAP_ANALYSIS.md)

---

## 3. آخر قياس تشغيلي

| المقياس | القيمة |
|---------|--------|
| اختبارات | **470/470 PASS** (App 267 · Domain 64 · Api 27 · Desktop 112) |
| Golden Integrity | **8/8** |
| FullScenario | **13/13** |
| Coverlet مجمّع | **80.64%** ≥ 75% |
| أمن ثابت | **PASS** |
| External gates harness | **PASS** (`run-external-gates.ps1`) |

---

## 4. مراجع الإغلاق

- `تقارير/CEEF-015` … `CEEF-099`
- `design-system/MASTER.md`
- `docs/ops/MASTER_EXTERNAL_GATES_RUNBOOK.md`
- `docs/ROADMAP.md`
- `docs/MARKET_LAUNCH_GAP_ANALYSIS.md` — فجوات الإطلاق السوقي V13+
- `تقارير/CEEF-116_V12_Plan_Formal_Closure.md`
- `تقارير/CEEF-119_O01_O05_Execution_Wave.md`
- `تقارير/CEEF-120_Supreme_Audit_Closure.md` — تدقيق شامل + توحيد وثائقي (2026-07-10)
- `تقارير/CEEF-121_Skills_v16.md` · `CEEF-122_Desktop_DI_Registration.md` · `CEEF-123_Desktop_Tier_P0_Migration.md`
- `تقارير/CEEF-124_Desktop_Tier_P2_P3_Complete.md` — Desktop DI كامل (0 new Service)
- `تقارير/CEEF-125_Schema_PG_Security_Gate.md` — PG gate + BRQOSH_TEST production guard
- `تقارير/CEEF-129_Organization_Provisioning_UI.md` — G-69 منشآت + 8 قطاعات
- `تقارير/CEEF-130_Sales_Lifecycle_UI.md` — G-08 عروض أسعار + G-09 أوامر بيع
- `تقارير/CEEF-131_OpenAPI_Swagger.md` — G-60 Swagger
- `تقارير/CEEF-132_CRM_G10_MVP.md` — G-10 CRM
- `تقارير/CEEF-133_Gulf_VAT_Foundation.md` — G-11…G-15 matrix
- `تقارير/CEEF-134_i18n_Nav_Foundation.md` — G-27 nav bilingual
- `تقارير/CEEF-135_O_External_Prep_Complete.md` — O-05 runbook + prep
- `تقارير/CEEF-136_Plan_Reconciliation.md` — تسوية الخطة الشاملة
- `تقارير/CEEF-138_Supreme_AZ_Audit_Master.md` — تدقيق A-to-Z مستقل (2026-07-10)
- `تقارير/CEEF-139_Security_RBAC_API.md` … `CEEF-144_Market_Lifecycle_Gaps.md`
