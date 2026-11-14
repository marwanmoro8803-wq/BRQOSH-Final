# تدقيقُ جاهزيّة الإنتاج — BRQOSH · 2026-11-14

**النطاق:** قائمةُ مفاهيم جاهزيّة الإنتاج كاملةً، كلُّ مفهومٍ مُسقَطٌ على ما يقابله فعلاً في هذا المستودع:
تطبيقُ سطح مكتب WPF (`src/Brqosh.Desktop`) على قاعدة PostgreSQL محلّيّة أو مدمجة، **وواجهةُ HTTP اختياريّة** (`src/Brqosh.Api`) تعمل في وضع SaaS.
فمفاهيمُ الويب تُقاس على الواجهة حيث وُجدت، وعلى سطح المكتب بمعادلها الحقيقيّ، وما لا معادلَ له يُعلَّم ➖ بسببٍ من سطر.

**الأساس المقيس:** `main` عند `582404d7` (الإصدار 1.1.5). **الفرع:** `claude/project-thread-lxdwez`.

**الرموز:** ✅ منفَّذٌ وعليه دليل · ⚠️ منفَّذٌ جزئيّاً أو بحدٍّ معلن · ❌ غائب · ➖ لا ينطبق.
**🔧** = أُصلح في هذا التغيير (انظر §٤).

> **الحكمُ المختصر.** المستودعُ ناضجٌ أمنيّاً ومحاسبيّاً على غير المعتاد: المالُ `decimal` في كلّ موضع، والترقيمُ وICV وPIH تحت قفلٍ مع فهرسٍ فريد، وسجلُّ التدقيق مسلسَلٌ بالتجزئة وغيرُ قابلٍ للتعديل، وPBKDF2 بـ600 ألف دورة، وقفلُ دخولٍ دائم، وDPAPI لمفتاح الهيئة، وCI على ويندوز مع PostgreSQL حقيقيّ ومنعِ التخطّي. **والفجواتُ التي وُجدت في هذه الجولة خمسٌ وأُغلقت كلُّها:** النسخُ الاحتياطيّةُ غيرُ مشفّرة (🔴 — أُغلق بقرار المالك: تشفيرٌ مغلَّفٌ يفتحه مفتاحُ استعادة المدير)، وسجلُّ الأعطال يكتب نصَّ الاستثناء بلا حجبٍ للأسرار، والواجهةُ بلا ترويسة أمانٍ واحدة ولا مُلتقِطٍ عامّ، وإعادةُ محاولة الهيئة حتميّةٌ بلا تشتيت، وبوّابتان تسقطان على `main` (ذيلُ UTF-16 في README، ووسمٌ داخليٌّ في نصّ المساعد). **ولا يبقى بندٌ 🔴 مفتوح**؛ ما بقي في §٣ مهمٌّ أو تحسين.

---

## ١. خطُّ الأساس والتحقّق

| المرحلة | البناء (`dotnet build Brqosh.sln -c Release`) | الاختبارات (`dotnet test Brqosh.sln -c Release`) |
|---|---|---|
| الأساس `582404d7` | 0 أخطاء · 47 تحذيراً | 4106 ناجحاً · 6 فاشلاً · 56 متخطًّى من 4168 (تفصيلٌ في §٥) |
| بعد التغيير | 0 أخطاء | 4205 ناجحاً · 4 فاشلاً · 0 متخطًّى من 4209 على PostgreSQL حقيقيّ (تفصيلٌ في §٥) |

---

## ٢. المفاهيم

### ٢.١ الهويّة والجلسات والصلاحيات

| المفهوم | الحالة | الدليل / الملاحظة |
|---|---|---|
| Authentication | ✅ | `src/Brqosh.Infrastructure/Security/UserAuthService.cs:62` (`AuthenticateAsync`) — اسمٌ مُطبَّع، قفلٌ قبل البحث، TOTP اختياريّ. |
| Hashing vs Encryption | ✅ | كلماتُ المرور **تجزئة** (PBKDF2) لا تشفير؛ الأسرارُ التي يلزم استرجاعُها (مفتاح الهيئة، رموز) **تشفير** DPAPI (`LocalSecretProtector.cs`، `Zatca/ZatcaKeyFile.cs`). |
| Salting | ✅ | ملحٌ عشوائيّ 16 بايت لكلّ كلمة مرور — `UserAuthService.cs:25`. |
| Password hashing strength | ✅ | PBKDF2-HMAC-SHA256 بـ600,000 دورة (توصية OWASP) — `UserAuthService.cs:13`؛ مقارنةٌ بزمنٍ ثابت `FixedTimeEquals`؛ ترقيةٌ تلقائيّة للتجزئات الأضعف (`NeedsRehash`). 🔧 تعليقُ `AppUser` كان يقول «SHA-256» فصُحّح. |
| Brute Force Protection / Account Lockout | ✅ | 5 محاولات ⟵ 15 دقيقة، **محفوظٌ في القاعدة** فلا تمحوه إعادةُ التشغيل — `Security/PersistentLoginLockout.cs:24-25`. |
| Password policy | ✅ | 12 محرفاً حدّاً أدنى — `Security/PasswordPolicy.cs:8`. |
| Password Reset Flows | ✅ | لا بريدَ ولا رابط: مفتاحُ استعادة المدير `Security/AdminRecoveryKey.cs` + أداةُ `tools/PasswordReset` على الجهاز نفسِه. |
| Two-Factor Authentication | ✅ | TOTP (RFC 6238) — `Security/TotpMfaService.cs`، والرمزُ يُقبل مرّةً واحدة (`MfaLastAcceptedStep`)؛ OTP عبر واتساب `WhatsAppOtpService.cs`. |
| Magic Links | ➖ | تطبيقُ سطح مكتب بلا بريدٍ ولا متصفّح في مسار الدخول؛ رابطٌ سحريّ يضيف قناةً لا يملكها المستخدم. |
| Passkeys | ➖ | WebAuthn يحتاج متصفّحاً أو Windows Hello API؛ يُعاد النظر إن طُلب Windows Hello — تقدير ٣–٥ أيّام. |
| SSO | ➖ | لا مزوّدَ هويّةٍ مؤسّسيّ في جمهور المنتج (منشآتٌ صغيرةٌ ومتوسّطة على جهازٍ محلّيّ). |
| Session Management | ✅ | الجلسةُ مربوطةٌ بـ`SessionVersion` فيُلغيها تغييرُ كلمة المرور أو الدور فوراً — `Desktop/AppServices.cs:53` (`IsSessionRevokedAsync`). |
| Session idle timeout | ✅ | 30 دقيقة خمول ⟵ إعادةُ الدخول — `Desktop/AppServices.cs:44`، يُفحص في `MainWindow.xaml.cs:381`، والنشاطُ من حركة الفأرة ولوحة المفاتيح (`MainWindow.xaml.cs:87-100`). |
| Token Expiration and Refresh | ➖ | لا رموزَ حاملة في سطح المكتب؛ الواجهةُ تعمل بمفتاح API ثابتٍ لكلّ مستأجر (انظر API Keys). |
| Cookie Flags | ➖ | لا كوكيز: سطحُ المكتب بلا متصفّح، والواجهةُ بلا جلسة كوكي (مفتاحٌ في ترويسة). |
| Authorization / RBAC | ✅ | أربعة أدوار `Domain/Security/AppUser.cs:3`؛ الكتابةُ المحاسبيّة في الواجهة مقصورةٌ على المدير والمحاسب (`Api/Program.cs` `RejectAccountingWrite`)؛ اختبار `ApiRoleEscalationTests`. |
| Principle of Least Privilege | ✅ | دورُ قاعدةٍ بأقلّ صلاحية `Security/LeastPrivilegeDatabaseRole.cs` + `tools/DbRoles`؛ `GITHUB_TOKEN` للقراءة فقط (`.github/workflows/ci.yml` `permissions: contents: read`). |
| Row-Level Security / company isolation | ⚠️ | الواجهة: عزلُ المستأجر يفشل مغلقاً (`TenantIsolationFailsClosedTests`، `TenantGuard`). سطحُ المكتب: الشركةُ عمودٌ `CompanyId` يُرشَّح في الاستعلامات يدويّاً لا بمرشّحٍ عامّ، والتنصيبُ الواحد شركةٌ واحدةٌ في الواقع. RLS على مستوى PostgreSQL غيرُ مُفعَّل — يلزم فقط إن صار للقاعدة الواحدة مستأجرون متعدّدون عبر سطح المكتب؛ تقدير ٤–٦ أيّام. |
| IDOR | ✅ | الواجهة: كلُّ مسارٍ بـ`companyId` يمرّ بـ`RejectTenantCompanyAsync`؛ `TenantIsolationListingTests`. |
| API Keys | ✅ | مفتاحٌ عامّ أو مفاتيحُ مربوطةٌ بمستأجر `Security/TenantApiKeys.cs`، مقارنةٌ بزمنٍ ثابت `SecretComparer`، والإنتاجُ يرفض الإقلاع بلا مفتاح (`ProductionApiSecurityStartupTests`). |
| Audit Logs | ✅ | `Domain/Security/AuditLogEntry.cs:31` تجزئةٌ مسلسَلة؛ `Persistence/AuditLogImmutabilityInterceptor.cs` يرفض التعديلَ والحذف؛ `Security/AuditChainVerifier.cs` يتحقّق من السلسلة؛ سجلُّ دخولٍ مستقلّ `LoginAuditService.cs`. |

### ٢.٢ المدخلات والحقن والمحتوى

| المفهوم | الحالة | الدليل / الملاحظة |
|---|---|---|
| Input Validation / Data Validation on the Server | ✅ | القواعدُ في المجال والخدمات لا في الشاشة (`JournalEntry` يرفض غيرَ المتوازن `Domain/Accounting/JournalEntry.cs:300-302`؛ `StandardInvoiceGaps`، `VatExemptionGaps` في `ZatcaEInvoiceGenerator.cs`). الواجهةُ تمرّ بالخدمات نفسِها. |
| SQL Injection | ✅ | LINQ/EF في كلّ مكان؛ `ExecuteSqlRaw` محصورٌ في مُحدِّث المخطّط ومعرّفاتُه تمرّ بـ`SchemaSqlGuard` (`Persistence/BrqoshSchemaUpdater.Regulatory.cs:305`) وفي `BackupService` بنصٍّ ثابت. |
| Command Injection | ✅ | `pg_dump`/`pg_restore` بـ`ProcessStartInfo.ArgumentList` لا بسلسلةٍ مركَّبة — `Operations/BackupService.cs:639`؛ كلمةُ المرور عبر `PGPASSWORD` لا سطر الأوامر. |
| Path Traversal | ✅ | الملفّاتُ تُكتب تحت `BrqoshDataRoot` بأسماءٍ يولّدها البرنامج؛ الاستيرادُ يقرأ ملفّاً يختاره المستخدم بحوار نظام ولا يكتب بمسارٍ من محتواه. |
| File Upload Security | 🔧✅ | لا رفعَ عبر الشبكة في سطح المكتب. الاستيراد (CSV) يمرّ بمحلّلٍ مُقيَّد (`CsvAmountText` للمبالغ)، و🔧 **يُقاس الملفُّ قبل قراءته**: 50 ميجابايت للـCSV في ترحيل البيانات والكشف البنكيّ والتسوية، و10 للشعار، والرفضُ بالعربيّة باسم الملفّ — `Operations/ImportFileGuard.cs`. |
| XSS | ➖/✅ | لا HTML يُصيَّر من بيانات المستخدم في WPF. معاينةُ PDF عبر WebView2 تعرض ملفّاً يولّده البرنامج. الواجهةُ JSON فقط، و🔧 `Content-Security-Policy: default-src 'none'`. |
| CSRF | ➖ | الواجهةُ لا تستعمل كوكيز؛ المصادقةُ بترويسة `X-Api-Key` لا يرسلها المتصفّح من تلقاء نفسه. |
| Mass Assignment | ✅ | الواجهةُ تقرأ سجلّاتِ طلبٍ مخصّصة (records) وتُسند الحقولَ صراحةً في الخدمات، لا ربطَ لكيانٍ كامل. |
| SSRF | ✅ | عناوينُ webhooks تمرّ بـ`Webhooks/WebhookUrlValidator.cs` (يرفض العناوين الخاصّة والمحلّيّة)؛ `WebhookUrlValidatorTests`. عناوينُ الهيئة ثابتةٌ في الكود. |
| Open Redirects | ➖ | لا مسارَ تحويلٍ في الواجهة ولا متصفّحَ في سطح المكتب. |
| XML External Entities | ✅ | قراءةُ XML الهيئة بـ`XDocument.Load` على .NET 10 — `XmlResolver` غائبٌ افتراضيّاً و`DtdProcessing` محظور؛ لا `XmlDocument` بمحلّلٍ مخصّص. |
| Content Security Policy / Security Headers / Clickjacking | 🔧✅ | كانت غائبةً كلُّها. صارت على كلّ ردّ: `X-Content-Type-Options: nosniff` · `X-Frame-Options: DENY` · `CSP default-src 'none'; frame-ancestors 'none'` · `Referrer-Policy: no-referrer` · `Cross-Origin-Resource-Policy` · `Cache-Control: no-store`، ولا ترويسة `Server` — `src/Brqosh.Api/Middleware/ApiResponseHardeningMiddleware.cs`. سطحُ المكتب ➖ (لا إطار). |
| HTTP Status Codes | ✅ | 401 بلا مفتاح، 403 للمستأجر والدور والترخيص، 409/422 للأعمال، 429 للحدّ؛ 🔧 و500 صار JSON بمعرّفٍ لا فارغاً. |
| Rate Limiting | ✅ | لكلّ مفتاحٍ أو عنوان، مع تقليمٍ يمنع نموَّ الذاكرة — `Api/Middleware/ApiRateLimitMiddleware.cs`؛ `ApiRateLimitPruningTests`. |
| Character Encoding | ✅ | UTF-8 في XML الهيئة وسجلّ الأعطال (`new UTF8Encoding(true)`)؛ وسمُ TLV طولُه بالبايت؛ 🔧 أُزيل ذيلُ UTF-16 من `README.md` كان يُسقط `NoRawControlCharactersInTheTreeTests` على `main`. |

### ٢.٣ سلامةُ البيانات المحاسبيّة والهيئة

| المفهوم | الحالة | الدليل / الملاحظة |
|---|---|---|
| Money as decimal | ✅ | بحثٌ عن `double`/`float` في حقول المبالغ والضرائب والتكاليف يُعيد صفراً؛ 303 موضعِ تقريبٍ صريح `MidpointRounding.AwayFromZero`. |
| Journal entry balanced | ✅ | `JournalEntry.IsBalanced` `Domain/Accounting/JournalEntry.cs:71`، ورفضٌ بالعربيّة عند الترحيل `:300-302`. |
| Posted documents immutable | ✅ | `Security/LedgerImmutability.cs`، `Persistence/ReadOnlyBooksInterceptor.cs`، `ClosedPeriodPostingInterceptor.cs`؛ التصحيحُ بإشعارٍ دائنٍ/مدين (`isCreditNote/isDebitNote` في `ZatcaEInvoiceGenerator.cs`). |
| Invoice numbering / ICV / PIH gap-free under concurrency | ✅ | `Persistence/DocumentSequenceLock.cs` و`DocumentNumberGenerator.cs` و`JournalEntryNumberGenerator.cs` (قفلٌ استشاريّ في المعاملة)؛ ICV: `AcquireIcvLockAsync` ثمّ `MAX+1` `Zatca/ZatcaEInvoiceGenerator.cs:663` مع فهرسٍ فريد `(CompanyId, Icv)` `BrqoshDbContext.PayrollTreasuryZatca.cs:208`؛ PIH يُقرأ تحت القفل نفسِه. مقيسٌ على PostgreSQL في CI (`BRQOSH_REQUIRE_PG=1`). |
| Idempotency | ✅ | الواجهة: `Api/Idempotency/IdempotencyStore.cs` (المفتاحُ لصاحبه — `IdempotencyIsTheCallersOwnTests`). الهيئة: مستندٌ معتمَدٌ لا يُعاد إرسالُه، والعطلُ العابرُ يُصنَّف معلَّقاً لا مرفوضاً (`ClassifyFailure`، `ResubmitPendingAsync`). |
| ZATCA private key / CSID protection | ✅ | DPAPI — `Zatca/ZatcaKeyFile.cs`، `Security/LocalSecretProtector.cs`؛ لا مفتاحَ في المستودع (`scripts/security-static-scan.ps1` في CI). ⚠️ معلَّقٌ قديمٌ معروف: منحنى مفتاح الختم مقابل المواصفة — في `docs/OPEN_ITEMS.md`. |
| QR TLV and UBL XML with Arabic seller name | ✅ | `Zatca/ZatcaQrGenerator.cs`، `ZatcaUblInvoiceBuilder`؛ اسمُ البائع `company.NameAr`؛ لحظةُ الإصدار واحدةٌ للمستند ورمزه (`ZatcaEInvoiceGenerator.cs:675-690`). |
| Soft Deletes | ✅ | `Domain/ISoftDeletable.cs` على البيانات الرئيسيّة مع مرشّحاتٍ عامّة `HasQueryFilter(... DeletedAtUtc == null)` (10 كيانات). |
| Database Normalization | ✅ | كياناتٌ ذات مفاتيح أجنبيّة؛ اللقطاتُ المتعمَّدة (وصفةُ الإنتاج المجمَّدة، بيانات المستند وقت الإصدار) تكرارٌ مقصودٌ لتثبيت التاريخ لا تسرّب. |
| Database Indexing | ✅ | فهارسُ فريدة على الأرقام وICV وفهارسُ مركّبة على `CompanyId`؛ `ScalePerfProbe` قاس ثمنَ القيد ثابتاً حتى 200 ألف. |
| Database Migrations | ✅ | 91 هجرة EF في `Persistence/Migrations`؛ نسخةٌ مُتحقَّقٌ منها **قبل** `MigrateAsync` وفشلُها يمنع الترقية في الإنتاج؛ `SchemaCoverageProbe` في CI. |
| Race Conditions / Concurrency | ✅ | أقفالٌ استشاريّة للترقيم والسنة (`LedgerYearLock.cs`) والجرد (`StockCountConcurrencyTests`)؛ `WriteSafeNpgsqlExecutionStrategy` لا يُعيد الكتابة تلقائيّاً فلا يُرحَّل قيدٌ مرّتين. |
| Time Zones | ✅ | التخزينُ UTC (`DateTime.UtcNow` 44 موضعاً في البنية والتطبيق مقابل 8 لـ`DateTime.Now` كلُّها أسماءُ ملفّاتٍ وطوابعُ عرض)؛ الرياض +03:00 ثابتة `RiyadhOffset`. |
| Data Privacy (Saudi PDPL) | ✅/⚠️ | البياناتُ على جهاز المنشأة لا على خادمنا؛ `Application/Privacy` (طلبات الإطّلاع والمحو)؛ 🔧 النسخُ الاحتياطيّة ورفيقُها من مستندات الهيئة (أسماءُ المشترين وأرقامُهم) مشفّرة؛ 🔧 حجبُ الأسرار من سجلّ الأعطال قبل إرساله للدعم. الحدّ: سجلُّ العطل يحمل اسمَ مستخدم ويندوز واسمَ الجهاز لأغراض الدعم — يُذكر في سياسة الخصوصيّة. |
| Encryption at Rest | ✅/⚠️ | الأسرارُ مشفّرة DPAPI ✅؛ 🔧 **النسخُ الاحتياطيّة مشفّرة** AES-256-GCM (§٤ · H)؛ ملفّاتُ القاعدة الحيّة غيرُ مشفّرة على مستوى التطبيق — التوصية: BitLocker على الجهاز (تشفيرُ القاعدة الحيّة من داخل التطبيق يحتاج TDE لا يوفّره PostgreSQL المجتمعيّ). |
| Encryption in Transit / HTTPS/TLS | ✅ | كلُّ الاتّصالات الخارجيّة HTTPS (الهيئة، أسعار الصرف، التحديث، واتساب). معالجُ الإعداد وشاشةُ الاتّصال **يشترطان TLS افتراضيّاً** لخادم PostgreSQL (`Views/Controls/DatabaseConnectionPanel.xaml.cs:80`، خانة `RequireTlsBox`) ولا يُزال إلّا لخادمٍ على الجهاز نفسِه — قِيس في التشغيل التجريبيّ. الواجهةُ لا تفرض HTTPS بنفسها — تُنشر خلف وكيلٍ عكسيّ يُنهي TLS. |
| Secret Rotation | ⚠️ | CSID يُجدَّد بإعادة الإعداد مع الهيئة (`ZatcaCsidOnboardingService`)، ومفاتيحُ API تُبدَّل بمتغيّر البيئة وإعادة التشغيل. لا تدويرَ مجدولٌ آليّ — مناسبٌ لحجم المنتج؛ 🟢. |
| Secrets Management / Environment Variables | ✅ | `BRQOSH_API_KEY`، `BRQOSH_TENANT_API_KEYS`، `ConnectionStrings:Brqosh` من البيئة؛ الإنتاجُ يرفض الإقلاعَ بلا مفتاح أو بقاعدةٍ افتراضيّة (`ProductionRefusesTheDefaultDatabaseTests`)؛ ملفُّ اتّصال سطح المكتب في جذر البيانات لا المستودع. |
| Penetration Testing | ⚠️ | فحصٌ ذاتيٌّ في §٦؛ لا اختبارَ اختراقٍ من طرفٍ ثالث — يلزم قبل بيع وضع SaaS؛ تقدير ٥–١٠ أيّام عمل خارجيّة. |

### ٢.٤ الاتّصالات الخارجيّة والمرونة

| المفهوم | الحالة | الدليل / الملاحظة |
|---|---|---|
| Timeouts | ✅ | لكلّ `HttpClient` مهلة: الهيئة 60ث (`ZatcaSubmissionService.cs:108`)، الإعداد 90ث، أسعار الصرف وواتساب 30ث، التحديث 20ث للبيان و30د للتنزيل. |
| Exponential Backoff | 🔧✅ | `Zatca/ZatcaRetryBackoff.cs`: 5⟵10⟵20⟵40⟵60 دقيقة، **وأُضيف تشتيتٌ حتى خُمس المهلة بعدها** فلا تعود الأجهزةُ كلُّها في الدقيقة نفسِها بعد انقطاعٍ للهيئة. القاعدة: `WriteSafeNpgsqlExecutionStrategy` (3 محاولات، 5ث سقفاً) للقراءة فقط. |
| Circuit Breakers | ✅ (معادل) | الباكوف نفسُه يعمل قاطعاً: فشلٌ متتالٍ يُباعد حتى ساعة، و401 لا يملأ السجلّ. لا حاجة لمكتبة Polly لعميلٍ واحدٍ في الجهاز. |
| Message Queues / offline queue | ✅ (معادل) | المستندُ المتعثّر يبقى «معلَّقاً» في جدول `ZatcaEInvoiceDocuments` ويُعاد إرسالُه في الخلفيّة كلَّ دقيقةٍ يحين فيها الموعد (`MainWindow.xaml.cs:1283`)؛ صندوقٌ صادرٌ للأحداث والـwebhooks (`AppServices.WireDomainEventPersistence`). لا وسيطَ رسائل — لا يلزم. |
| Webhooks | ✅ | `Infrastructure/Webhooks` تسجيلٌ بصلاحية (`WebhookSubscriptionAuthorizationTests`) وتحقّقٌ من العنوان. |
| Background Jobs / Cron Jobs | ✅ | مؤقّتاتٌ داخل التطبيق: النسخُ كلَّ 30 دقيقة (`MainWindow.xaml.cs:113`)، إعادةُ إرسال الهيئة، فحصُ التحديث؛ لا تُشغَّل فوق نفسها (`_backupRunning`، `_zatcaRunning`). ولا جدولةَ نظام تشغيل تلزم. |
| Async/Await / UI freezes | ✅ | العملُ الشبكيّ والقاعديّ `async` من المعالجات؛ `TaskScheduler.UnobservedTaskException` مُلتقَطٌ ومسجَّل (`App.xaml.cs:139`). |
| Graceful Shutdown | ✅ | `OnExit` يوقف الخادمَ المدمج (`App.xaml.cs:100`)؛ الكتابةُ في معاملاتٍ فلا قيدَ نصفُه مكتوب؛ حدٌّ معلن: `pg_dump` ابنٌ قد يبقى ملفَّه المرحليّ ولا يُحسب نسخة (موثَّق في `App.xaml.cs`). |
| Connection Pooling | ✅ | تجمُّعُ Npgsql الافتراضيّ؛ والقفلُ الجلسيّ يُحرَّر في `finally` لأنّ الإغلاقَ لا يحرّره مع التجمّع (ح-١٥٦). |

### ٢.٥ الأخطاء والسجلّات والمراقبة

| المفهوم | الحالة | الدليل / الملاحظة |
|---|---|---|
| Error Handling (central) | ✅ | المعالجات الثلاثة في `App.xaml.cs:119-145` ومسارُ إبلاغٍ واحد `ReportCrash`؛ 🔧 الواجهة: مُلتقِطٌ عامّ يُسجّل ويُردّ `{"error":"internal_error","correlationId":…}` بلا نصّ الاستثناء. |
| Correlation id | 🔧✅ | الواجهة: `X-Correlation-Id` على كلّ ردٍّ ويُعاد ما أرسله العميلُ إن سلم شكلُه. سطحُ المكتب: اسمُ ملفّ العطل الفريد يُعرض في الحوار ويعمل معرّفاً للدعم. |
| No raw exception text to the user | ⚠️ بقرار | رسائلُ المجال عربيّةٌ مكتوبةٌ للمستخدم (`AccountingException`) وتُعرض عمداً لأنّها تقول ما يُصلَح. 🔧 أمّا الأسرارُ فيها فتُحجب قبل العرض بالحاجب نفسِه. إخفاءُ كلّ نصٍّ يُفقد المستخدمَ سببَ الرفض — لذلك لم يُفعل. |
| Logging / Structured Logging | ✅/⚠️ | الواجهة: Serilog (`UseSerilogRequestLogging`). سطحُ المكتب: سجلُّ أعطالٍ نصّيٌّ منظَّمُ الحقول (`Operations/CrashLog.cs`) لا JSON — مناسبٌ لقراءة الدعم؛ 🟢 تحويلُه JSON إن صار له مُجمِّع. |
| Log rotation | ✅ | 5 لكلّ سياقٍ و30 إجمالاً — `CrashLog.cs` `PerContextLimit`/`TotalLimit`. |
| Redaction of secrets/PII in logs | 🔧✅ | **كان غائباً.** `src/Brqosh.Infrastructure/Operations/LogRedactor.cs`: كلماتُ المرور في سلاسل الاتّصال، `api_key`/`token`/`secret`/`binarySecurityToken` بصيغتي `key=value` و JSON، `Bearer`/`Basic`، وكتلُ PEM الخاصّة؛ يبقى اسمُ المفتاح ويُحجب الأصلُ كلُّه إن استنفد النصُّ ميزانيةَ التعبير. |
| Monitoring / Health Checks | ✅ | `/health` و`/health/ready` (`Api/Program.cs:352`)، ونصُّ عطل القاعدة لا يُكشف في الإنتاج (`Middleware/ReadinessProbe.cs`)؛ عدّاداتُ `AppMetrics`؛ صفحةُ التشخيص في سطح المكتب. |
| Error Tracking | ⚠️ | محلّيٌّ فقط (سجلّاتُ الأعطال تُرسل يدويّاً عبر صفحة الدعم). خدمةُ تتبّعٍ سحابيّة (Sentry…) تعني إرسالَ بياناتٍ ماليّةٍ خارج الجهاز — قرارُ خصوصيّة للمالك؛ 🟢. |
| Alerting / Uptime Monitoring | ➖/⚠️ | سطحُ المكتب ➖ (لا خدمة تعمل بلا مستخدم). الواجهة في SaaS: `/health` جاهزٌ لأيّ مراقب خارجيّ، والمراقبُ نفسُه بنيةٌ تحتيّة يُختار عند النشر. |
| Distributed Tracing | ➖ | عمليّةٌ واحدة وقاعدةٌ واحدة؛ معرّفُ الترابط يكفي. |
| Debugging / Profiling | ✅ | أدواتٌ في `tools/` (`ScalePerfProbe`، `DbStartupProbe`، `MigrationProbe`)، وسجلُّ المخطّط `schema-*.log`. |
| Memory Leaks | ✅ | المؤقّتاتُ تُوقَف عند `Closed`؛ القواميسُ المتنامية في الواجهة تُقلَّم (`ApiRateLimitPruningTests`). لا قياسَ ذاكرةٍ طويلٌ آليّ — 🟢. |
| Performance Budgets / Load Testing | ✅ | `JournalStressLoadTests` بميزانية 100ms، `ScalePerfProbe` على 200 ألف قيد، و«وضعُ المليون» على PostgreSQL. |
| Cost Monitoring | ➖ | لا بنيةَ سحابيّةً مدفوعة يشغّلها المنتج؛ CI على GitHub Actions المجّانيّ للمستودع. |

### ٢.٦ النسخ والتعافي

| المفهوم | الحالة | الدليل / الملاحظة |
|---|---|---|
| Backups (scheduled) | ✅ | كلَّ 30 دقيقة في التطبيق (`MainWindow.xaml.cs:113`) + `scripts/backup-postgres.ps1`. |
| Verified restore | ✅ | كلُّ نسخةٍ تُفحص بعد كتابتها (`BackupService.VerifyAsync` `:366`: `pg_restore --list` أو فتحُ ملفّ SQLite)؛ الاستعادةُ تأخذ نسخةَ أمانٍ قبلها (`backups/pre-restore`). |
| Retention policy | ✅ | تقليمٌ بعد كلّ نسخة مع عدّ ما تعذّر حذفُه — `BackupScheduleService.cs:46-53`. |
| Backup encryption | 🔧✅ | **كان غائباً (🔴) وأُغلق بقرار المالك 2026-11-14.** `Operations/BackupCipher.cs` و`BackupKeyring.cs` — التفصيلُ في §٤ · H. |
| Off-site copy | ✅ | `Operations/OffsiteBackupService.cs` ينسخ نسخةً مُتحقَّقاً منها إلى وجهاتٍ عدّة، ويُعلن الوجهةَ التي على القرص نفسِه بأنّها ليست خارج الجهاز؛ 🔧 والنسخُ المنقولةُ صارت مشفّرة. |
| Disaster Recovery | ✅ | الإجراءُ موثَّق في `docs/ops` و`scripts/restore-postgres.ps1`؛ 🔧 النسخةُ المشفّرة تُستعاد على **جهازٍ جديد** بمفتاح استعادة المدير وحدَه (لا DPAPI ولا ملفّ آخر)، وشاشةُ الاستعادة تسأل عنه. |
| Read Replicas / Horizontal vs Vertical Scaling / Load Balancing | ➖ | قاعدةٌ لكلّ منشأة على جهازها؛ التوسّعُ رأسيّ (جهازٌ أقوى). الواجهة SaaS عديمةُ الحالة عدا مخزن التكرار في الذاكرة (انظر Stateless). |
| Stateless Services | ⚠️ | الواجهةُ عديمةُ الحالة **عدا** حدّ المعدّل ومخزن مفاتيح التكرار في الذاكرة — نسختان خلف موزّعٍ لا تتشاركانهما. يلزم مخزنٌ مشترك (PostgreSQL) قبل تشغيل أكثر من نسخة؛ تقدير يومان. |
| Caching / Cache Invalidation / CDNs | ➖ | لا طبقةَ تخزينٍ مؤقّت: الاستعلامُ مباشرٌ على قاعدةٍ محلّيّة، و`no-store` على ردود الواجهة عمداً. لا محتوى ثابتاً يُوزَّع. |
| Reverse Proxies / API Gateways | ➖ | الواجهةُ تُنشر خلف وكيل المستضيف (IIS/nginx) الذي يُنهي TLS؛ لا بوّابةَ API لخدمةٍ واحدة. |

### ٢.٧ الهندسة والتسليم

| المفهوم | الحالة | الدليل / الملاحظة |
|---|---|---|
| Automated Testing / Unit / Integration / E2E | ✅ | أربعة مشاريع اختبار (المجال، التطبيق، سطح المكتب، الواجهة)؛ تكاملٌ على PostgreSQL؛ رحلاتٌ كاملة (`FullScenarioAudit`، `desktop-qa-army`)؛ قيادةُ الواجهة الحقيقيّة بـUIA. |
| CI/CD | ✅ | `.github/workflows/ci.yml` على `windows-latest`، PostgreSQL 17 حقيقيّ، منعُ التخطّي، بوّابةُ تغطية، فحصٌ أمنيّ ساكن، إجراءاتٌ مثبّتةٌ ببصمة. ⚠️ يُشغَّل على `main` و`wip/**` لا على `claude/**` — طلباتُ الدمج إلى `main` تُقاس. |
| Dependency Vulnerabilities | ✅ | `dotnet list package --vulnerable --include-transitive` على الحلّ كلّه في هذه الجولة: **لا حزمةَ مصابة** في المشاريع التسعة. `.github/dependabot.yml` يغطّي nuget وgithub-actions. |
| Docker | ➖ | تطبيقُ سطح مكتب ويندوز (WPF + DPAPI) لا يُحوى. الواجهة يمكن حويُها — لا طلبَ قائماً؛ تقدير نصف يوم. |
| Infrastructure as Code | ➖ | لا بنيةَ سحابيّة؛ المُنصِّبُ (WiX، `installer/`) هو «بنيةُ» المنتج وهو كودٌ مُودَع. |
| Staging Environments | ✅ (معادل) | بيئةُ محاكاة الهيئة (`ZatcaSimulationPreflight`، `docs/ZATCA_LIVE_CSID.md`) وآلةُ اختبارٍ نظيفة (ر-ك) للتنصيب. |
| Feature Flags | ✅ (معادل) | قدراتُ الترخيص (`LicenseGate.Allows`، `LicensedApiRoutes`) تفتح الوحدات وتغلقها؛ لا منصّةَ أعلام تلزم. |
| Code Review / Branching Strategies | ✅ | فروعُ `claude/*` و`wip/*` وطلباتُ دمجٍ إلى `main`؛ الدمجُ قرارُ المالك. |
| Semantic Versioning | ✅ | `version.txt` + `-BuildNumber` للـMSI (`1.1.5`). |
| API Versioning | ✅ | المساراتُ تحت `/api/v1`؛ `Accept-Version` لطلبات الشهادة مع الهيئة. |
| API Documentation | ✅ | Swagger/OpenAPI في التطوير (`Api/Program.cs:40`)؛ `OpenApiDocumentTests`. |
| Technical Debt | ⚠️ | مسجَّلٌ بندياً في `docs/OPEN_ITEMS.md` و`docs/release-readiness/REMAINING-MAJORS.md`؛ ملفّاتٌ كبيرة (`MainWindow.xaml.cs`، `Api/Program.cs` 913 سطراً) — 🟢 تقسيمٌ تدريجيّ. |
| Accessibility | ⚠️ | 104 `AutomationProperties.Name` و32 `AutomationId` في XAML؛ الواجهةُ تُقاد آليّاً بـUIA (دليلٌ على تسمية العناصر الحرجة)؛ الاتّجاهُ من `LocaleManager.RegisterWindows` لكلّ نافذة. الحدّ: لم يُجرَ فحصُ قارئ شاشةٍ يدويّ لكلّ الشاشات؛ 🟢. |
| Mobile Responsiveness | ➖ | تطبيقُ سطح مكتب؛ والنوافذُ تُحصر في مساحة العمل وتُمرَّر إن لم تتّسع (`Ui.WindowFit`). |

---

## ٣. الخطّة المرتّبة — ما بقي

### 🔴 حرج
لا بندَ حرجٌ مفتوح. (تشفيرُ النسخ أُغلق في هذا التغيير؛ والنسخةُ خارج الجهاز كانت قائمةً أصلاً — `OffsiteBackupService` — وذكرُها ناقصةً في المسوّدة الأولى من هذا التدقيق كان خطأً صُحّح.)

### 🟠 مهمّ
1. **تنصيباتٌ قائمةٌ نسخُها صريحة حتى يُعاد إصدارُ مفتاح الاستعادة.** حلقةُ مفاتيح النسخ تولد مع إصدار المفتاح (الإعداد، «إعادة إصدار المفتاح» في نافذة تغيير كلمة المرور، أو الاستعادة به)، لأنّ المفتاحَ يُخزَّن تجزئةً فقط ولا يمكن اشتقاقُ شيءٍ منه بعد. شاشةُ النسخ تقول «غيرُ مشفّرة — أعِد إصدار المفتاح». **قرارٌ للمالك:** هل يُطلب الإصدارُ مرّةً عند أوّل تشغيلٍ بعد الترقية؟ تقدير نصف يوم.
2. **مديرون متعدّدون:** آخرُ مفتاح استعادةٍ صدر لأيّ مدير هو الذي يفتح النسخ (ويُعاد تغليفُ القائمة له). لعدّة مفاتيح معاً يلزم رأسٌ بأكثر من مقطع استعادة — يوم.
3. **حالةُ الواجهة المشتركة** قبل تشغيل أكثر من نسخة: حدّ المعدّل ومفاتيح التكرار في PostgreSQL. يومان.
4. **اختبارُ اختراقٍ خارجيّ** للواجهة قبل بيع وضع SaaS. ٥–١٠ أيّام خارجيّة.
5. **RLS في PostgreSQL** إن صارت القاعدةُ الواحدة لعدّة مستأجرين عبر سطح المكتب. ٤–٦ أيّام.
6. **TLS لاتّصالاتٍ قديمة:** الشاشةُ تشترط TLS للتنصيبات الجديدة، لكنّ ملفّ اتّصالٍ قديماً لخادمٍ على الشبكة بلا TLS يبقى يعمل. **لم يُفرض عمداً**: منشآتٌ قائمةٌ بخادمٍ على شبكة المكتب بلا شهادة ستتوقّف عند الترقية. **قرارٌ للمالك:** تحذيرٌ عند الإقلاع أم رفض. نصف يوم.
7. **بوّابةُ «رقم الإصدار يتحرّك مع كلّ تغيير»** (`TheVersionMovesWithEveryChangeTests`) تسقط على هذا الفرع لأنّه يغيّر `src/` ولا يرفع `version.txt` — **عمداً**: الرقمُ يملكه فرعُ `claude/trial-hardening`، ويُرفع عند الدمج.
8. **ثلاثةُ اختباراتٍ للمساعد تسقط على `main` قبل هذا التغيير** (`ProgramAssistantTests`): الإيداعُ `a5acf666` غيّر نصَّ دليل الصلاحيات («دورك الحالي…» بدل «الصلاحيات الحالية») ولم يُحدّث الاختبار، ودليلُ لوحة التحكم الإنجليزيّ يحوي كلمةَ `dashboard` وهي مفتاحُ الشاشة. قرارُ نصٍّ للمنتج — لم يُمسّ.

### 🟢 تحسين
9. سجلُّ أعطالٍ JSON إن وُجد مُجمِّع. نصف يوم.
10. فحصُ قارئ شاشة (NVDA) للشاشات العشر الأكثر استعمالاً. يومان.
11. تقسيمُ `Api/Program.cs` إلى ملفّات مسارات. يوم.

---

## ٤. ما أُصلح في هذا التغيير (ملاحظاتُ الإصدار)

| المحور | الإصلاح | الملفّات | الاختبار |
|---|---|---|---|
| C/F — الواجهة | ترويساتُ أمان على كلّ ردّ، `X-Correlation-Id` (يُقبل من العميل بشكلٍ محدود يمنع حقنَ السجلّ)، مُلتقِطٌ عامّ يُسجّل ويُردّ 500 بنوعٍ عامٍّ ومعرّف، حذفُ ترويسة `Server`. | `src/Brqosh.Api/Middleware/ApiResponseHardeningMiddleware.cs`، `src/Brqosh.Api/Program.cs` | `tests/Brqosh.Api.Tests/ApiResponseHardeningTests.cs` |
| F — السجلّات | حاجبُ أسرار لسجلّ الأعطال ولنصّ حوار الخطأ. | `src/Brqosh.Infrastructure/Operations/LogRedactor.cs`، `CrashLog.cs`، `src/Brqosh.Desktop/App.xaml.cs` | `tests/Brqosh.Application.Tests/Operations/LogRedactorTests.cs` |
| E — الهيئة | تشتيتٌ حتى 20٪ يُضاف **بعد** المهلة الأُسّيّة (لا تُبكَّر المحاولةُ أبداً)، ومصدرُ العشوائيّة قابلٌ للحقن للاختبار. | `src/Brqosh.Infrastructure/Zatca/ZatcaRetryBackoff.cs` | `tests/Brqosh.Application.Tests/Zatca/ZatcaRetryBackoffTests.cs` |
| H — النسخ | **تشفيرٌ مغلَّف بقرار المالك.** لكلّ نسخةٍ مفتاحُ بياناتٍ عشوائيّ 256 بت يشفّر المحتوى AES-256-GCM مقطَّعاً (1 ميبي بايت، وسمٌ لكلّ قطعة، ورقمُها وعَلَمُ «الأخيرة» في البيانات المرافقة فلا تبديلَ ولا بتر). مفتاحُ البيانات مغلَّفٌ بـECDH P-256 للمفتاح العامّ الرئيسيّ — فالنسخُ المجدولة تُشفَّر بلا سرّ. المفتاحُ الخاصّ الرئيسيّ مغلَّفٌ بمفتاحٍ مشتقٍّ من مفتاح استعادة المدير (PBKDF2-HMAC-SHA256، 600,000 دورة، ملحٌ عشوائيّ؛ لا Argon2 لأنّه ليس اعتماديّةً قائمة) **في رأس كلّ ملفّ**، فالاستعادةُ على جهازٍ جديدٍ تحتاج المفتاحَ وحدَه. **لا DPAPI في مسار الفتح**؛ نسخةٌ محميّةٌ بـDPAPI من المفتاح الخاصّ تبقى على الجهاز نفسِه لفحص النسخة بعد أخذها والاستعادة المحلّيّة وإعادة التغليف. تدويرُ المفتاح يكتب مقطعَ الاستعادة (80 بايتاً ثابتة) في مكانه في كلّ نسخةٍ قائمة دون إعادة تشفير المحتوى. المفتاحُ الخاطئ والبايتُ المعدَّل والبترُ تُرفض برسالةٍ عربيّة ولا يبقى نصٌّ مفكوك. النسخُ الصريحةُ القديمة تُستعاد كما كانت. رفيقُ مستندات الهيئة مشفّرٌ كذلك. شاشةُ الاستعادة تسأل عن المفتاح حين يلزم، وشاشةُ النسخ تقول إن كانت النسخةُ مشفّرة. | `src/Brqosh.Infrastructure/Operations/BackupCipher.cs`، `BackupKeyring.cs`، `BackupService.cs`، `Security/AdminRecoveryKey.cs`، `Security/UserAuthService.cs`، `src/Brqosh.Desktop/Views/Pages/BackupPage.xaml.cs`، `assets/i18n/*` + `UiBilingualRegistry.g.cs` | `tests/Brqosh.Application.Tests/Operations/BackupEncryptionTests.cs` (ذهابٌ وإيابٌ عند حدود القطع، مفتاحٌ خاطئ، بايتٌ معدَّلٌ في أربعة مواضع، بتر، تدوير، جهازٌ آخر، نسخةٌ صريحةٌ قديمة)؛ وثلاثةُ اختباراتٍ تقرأ الصيغةَ الصريحةَ صارت تعزل جذرَ الحلقة |
| الترميز والمساعد | إزالةُ ذيل UTF-16 (`# BRQOSH-Final`) من `README.md` أُلحق في إيداع «first commit» وأسقط بوّابةَ محارف التحكّم على `main`؛ و«workflow» في نصّ المساعد الإنجليزيّ صار «procedure» (وسمٌ داخليٌّ يمنعه حارس). | `README.md`، `src/Brqosh.Application/Intelligence/ProgramKnowledge.cs` | `NoRawControlCharactersInTheTreeTests`، `NoGuideSpeaksInInternalTags` |
| B — توثيق | تعليقُ `AppUser` كان يصف تجزئة SHA-256 القديمة. | `src/Brqosh.Domain/Security/AppUser.cs` | — |
| C — الاستيراد | سقفُ حجمٍ يُقاس قبل القراءة لملفّات CSV المستورَدة (ثلاث شاشات) ولصورة الشعار. | `src/Brqosh.Infrastructure/Operations/ImportFileGuard.cs`، أربعُ شاشاتٍ في `Views/Pages` | `tests/Brqosh.Application.Tests/Operations/ImportFileGuardTests.cs` |
| قواعد | `CLAUDE.md` في الجذر بالقواعد الهندسيّة الدائمة. | `CLAUDE.md` | — |

لم يُرفع رقمُ الإصدار ولم يُعدَّل `CHANGELOG.md` (يملكهما فرعُ `claude/trial-hardening`).

---

## ٥. نتائجُ التحقّق

كلُّ رقمٍ هنا من مخرجات `dotnet test -c Release` الفعليّة على هذا الجهاز (Windows 11، .NET SDK 10.0.402).

| المشروع | الأساس `582404d7` (بلا PostgreSQL محلّيّاً) | بعد التغيير |
|---|---|---|
| Domain.Tests | 297 / 298 — يسقط `NoRawControlCharactersInTheTreeTests` (ذيلُ UTF-16 في README) | **298 / 298** |
| Api.Tests | 127 / 127 | **133 / 133** (+6 اختباراتِ ترويساتٍ ومُلتقِط) |
| Desktop.Tests | 914 / 916 — يسقط `NoGuideSpeaksInInternalTags` (قائمٌ على main)، و`NoSilentCatch_AnywhereInSource` (التقط ملفّي الجديدَ أثناء التشغيل وأُصلح قبل الإيداع) | **916 / 916** |
| Application.Tests | 2768 ناجحاً · 3 فاشلاً · 56 متخطًّى (اختباراتُ PostgreSQL بلا خادم) | **2858 ناجحاً · 4 فاشلاً · 0 متخطًّى** — شُغّلت على خادم PostgreSQL 17 مؤقّتٍ بـ`BRQOSH_REQUIRE_PG=1`، فجرت اختباراتُ التزاحم والترقيم وICV والنسخ والاستعادة كلُّها. وبعد إضافة سقف الاستيراد (+4 اختبارات) أُعيد بلا خادم: 2806 ناجحاً · 4 فاشلاً · 56 متخطًّى من 2866 — الأربعةُ أنفسُها |

**الإخفاقاتُ الأربعة الباقية ليست من هذا التغيير:** ثلاثةٌ في `ProgramAssistantTests` تسقط على `main` قبله (§٣ البند ٨)، وواحدٌ `TheVersionMovesWithEveryChangeTests` يسقط لأنّ هذا الفرع لا يرفع `version.txt` بتعليمات المهمّة (§٣ البند ٧).

**التشغيلُ التجريبيّ للتطبيق الحقيقيّ** (`Brqosh.Desktop.exe` من `bin\Release`، جذرُ بياناتٍ معزولٌ `BRQOSH_DATA_ROOT` في `%TEMP%`، وخادمُ PostgreSQL 17 مؤقّتٌ على `localhost:55431` أُنشئ لهذا الغرض فقط، والقيادةُ بـUI Automation):

| الخطوة | النتيجة |
|---|---|
| الإقلاع ← اتّفاقيّةُ الترخيص | ✅ قُبلت |
| معالجُ الإعداد: المنشأة والمدير والقاعدة | ✅ «الاتصال ناجح — PostgreSQL 17.10»؛ خانةُ TLS مفعَّلةٌ افتراضيّاً وأُزيلت لأنّ الخادمَ على الجهاز نفسِه |
| شاشةُ مفتاح الاستعادة | ✅ ظهرت وأُقرّت — **وأنشأت `security/backup-keyring.json`** (المفتاحُ الخاصّ محميٌّ `BRQOSH1:`، والنقطةُ العامّة 65 بايتاً) |
| الدخول | ⛔ لم يُبلغ: ظهرت شاشةُ **تفعيل الترخيص** («انتهت صلاحية الترخيص») — حالةُ التجربة مربوطةٌ بالجهاز لا بجذر البيانات، والترخيصُ مجالُ فرع `claude/trial-hardening`؛ ولم يُصدَر مفتاحُ ترخيص |
| إنشاءُ فاتورة | ✅ عبر `tools/FullScenarioAudit` على القاعدة المعزولة نفسِها (بإذنٍ بالاسم `localhost/brqsmoke_books`): فاتورةُ مبيعاتٍ مرحَّلة 4,312.50 = 3,750.00 + 15٪، وميزانُ المراجعة 19,312.50 = 19,312.50، والمركزُ الماليّ متوازن، وسجلُّ التدقيق 4 إدخالات |

---

## ٦. فحصٌ اختراقيٌّ ذاتيّ

| الهجوم | الطريقة | النتيجة |
|---|---|---|
| تصعيدُ صلاحيةٍ بين الأدوار (الواجهة) | دورُ أمين صندوق/مشاهد يطلب مسارَ كتابةٍ محاسبيّة | 403 `accounting_write_forbidden` — مقيسٌ في `AccountingWriteRoutesRequireARoleTests` و`ApiRoleEscalationTests`. |
| تصعيدُ صلاحيةٍ (سطح المكتب) | مستخدمٌ غُيّر دورُه أثناء الجلسة | الجلسةُ تُلغى بتغيّر `SessionVersion` (`AppServices.IsSessionRevokedAsync`). |
| تجاوزُ المستأجر | مفتاحُ مستأجرٍ أ مع `X-Tenant-Id` لمستأجر ب | 403 `tenant_mismatch` — `TenantBoundApiKeysTests`. |
| تخمينُ كلمة المرور | محاولاتٌ متتالية ثمّ إعادةُ تشغيل | القفلُ يبقى (محفوظٌ في القاعدة) — `PersistentLoginLockout`. |
| العبثُ بملفّ القاعدة | تعديلُ صفٍّ في سجلّ التدقيق مباشرةً بـSQL | `AuditChainVerifier` يكشف انقطاعَ السلسلة؛ والتعديلُ عبر EF يُرفض بالمعترِض. **الحدّ:** من يملك صلاحيةَ مالك القاعدة يستطيع إعادةَ حساب السلسلة كلّها — الدفاعُ هنا دورُ القاعدة الأقلُّ صلاحيةً للتطبيق وحمايةُ الجهاز، لا التشفير. |
| العبثُ بفاتورةٍ مرحَّلة | تعديلُ مبلغ فاتورةٍ معتمَدة | `LedgerImmutability`/`ReadOnlyBooksInterceptor` يرفضان؛ وتجزئةُ المستند في سلسلة PIH تجعل أيَّ تعديلٍ يكسر ما بعده عند الهيئة. |
| تسريبُ السرّ عبر الأخطاء | استثناءٌ يحمل سلسلةَ اتّصالٍ بكلمة مرور | قبل التغيير: يُكتب في السجلّ ويُعرض. **بعده: يُحجب** — `LogRedactorTests.TheComposedCrashLog_CarriesNoSecretFromAnyInnerException`. |
| تسريبُ البنية عبر 500 | مسارٌ يرمي في الواجهة | قبل: Kestrel يُردّ 500 فارغاً بلا معرّف (وفي التطوير صفحةُ المطوّر). **بعده:** JSON بنوعٍ عامٍّ ومعرّف، والنصُّ في السجلّ وحدَه. |
| حقنُ السجلّ | `X-Correlation-Id` بأسطرٍ جديدة أو نصٍّ طويل | يُرفض ويُستبدل بـ`TraceIdentifier` (`^[A-Za-z0-9._:-]{1,64}$`). |
| سرقةُ نسخةٍ احتياطيّة | نسخُ ملفّ النسخة إلى جهازٍ آخر وفتحُه | قبل: قاعدةٌ صريحةٌ تُفتح بأيّ أداة. **بعده:** لا يُفتح إلّا بمفتاح استعادة المدير؛ مفتاحٌ خاطئٌ يُرفض — `BackupEncryptionTests.AWrongRecoveryKey_IsRefused_InWords`. |
| العبثُ بنسخةٍ احتياطيّة | تعديلُ بايتٍ في المحتوى أو الرأس، أو بترُ آخرها، ثمّ استعادتُها | تُرفض قبل أن تُلمس القاعدةُ الحيّة، ولا يبقى نصٌّ مفكوك — `ATamperedByte_IsRefused_AndLeavesNoPlaintext`، `DroppingTheLastChunk_IsRefused`. |
| SSRF عبر webhook | عنوانُ `http://127.0.0.1` أو `169.254.169.254` | مرفوض — `WebhookUrlValidatorTests`. |
