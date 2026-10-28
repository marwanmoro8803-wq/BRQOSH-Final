# ADR 0003 — ختم ZATCA مركزي لعدة محطات

الحالة: **مُعتمد — الخيار 1 (موقِّع مركزي على الخادم)، ومُنفَّذ؛ يبقى تسجيل CSID على الخادم بانتظار OTP من بوابة فاتورة.**

## ما نُفِّذ

- `ZatcaCentralSigner` (Brqosh.Infrastructure/Zatca/ZatcaCentralSigning.cs): يختم على الخادم بمفتاح CSID المسجَّل عليه،
  ولا يختم إلا مستنداً بائعُه المنشأةُ نفسُها.
- نقطة `POST /zatca-signer/sign` في `Brqosh.Api`، محمية بسرّ مشترك `BRQOSH_ZATCA_SIGNER_TOKEN` (مقارنة ثابتة الزمن،
  وبلا سرّ مُعَدّ تُغلق النقطة بـ503).
- المحطة: بضبط `BRQOSH_ZATCA_SIGNER_URL` و`BRQOSH_ZATCA_SIGNER_TOKEN` يطلب `ZatcaEInvoiceGenerator` الختمَ من الخادم
  بعد حجز ICV/PIH تحت `pg_advisory_xact_lock` في معاملة الترحيل نفسها؛ ورفضُ الخادم يُسقط الترحيلَ فلا يُستهلك ICV.
  وبدونهما يبقى الختم محلياً كما كان.
- اختبارات `StationsStampThroughTheCentralSignerTests` بمفتاح ذاتي التوقيع.

## الخطوة الأخيرة (غير منفَّذة حتى يصل OTP)

تشغيل معالج تسجيل CSID على جهاز الخادم بـ OTP من بوابة فاتورة (بيئة المحاكاة ثم الإنتاج)، ثم ضبط المتغيّرين على المحطات.

## ما هو قائم اليوم (فُحص في الكود)

- **التسلسل مشترك فعلاً على PostgreSQL**: `ZatcaEInvoiceGenerator.GenerateAsync` يأخذ
  `pg_advisory_xact_lock(companyId, 7301)` داخل معاملة الترحيل، ثم يقرأ `MAX(Icv)+1` وبصمة آخر مستند
  (PIH) من `zatca_einvoice_documents` — فكل المحطات على قاعدة واحدة تتسلسل على عدّاد واحد وسلسلة واحدة،
  والفهرس الفريد يلتقط أي تزاحم. اختبارات `IcvLockIsAliveTests` تثبت ذلك على PostgreSQL (تعمل على ويندوز
  فقط لأن مفتاح الختم محمي بـ DPAPI).
- **الختم نفسه على محطة واحدة**: مفتاح CSID مشفَّر بـ DPAPI على الجهاز الذي أجرى التسجيل
  (`ZatcaSettingsSecrets.RecordSigningMachine`)، وأي محطة أخرى تُرفض عند الإصدار
  («الفواتير الإلكترونية تُختم على الجهاز المسجَّل لدى الهيئة وحده»).

## لماذا لا يكفي طابورٌ في القاعدة

الفاتورة المبسطة تُطبع برمز QR موقَّع لحظة البيع، والختم يجري **داخل معاملة الترحيل** (فشله يُلغي الترحيل).
فالختم المؤجَّل عبر طابور يكسر نقطة البيع، والختم عبر خدمة أخرى لا يشارك معاملة المحطة.

## الخيارات

1. **موقِّع مركزي على الخادم** (`Brqosh.Api` كخدمة ويندوز على جهاز PostgreSQL): تسجيل CSID يُجرى على الخادم،
   والمحطة تطلب الختم متزامناً بعد حجز ICV في معاملتها؛ يلزم بروتوكول «حجز ثم ختم ثم تثبيت» وتعويض عند الفشل.
2. **مفتاح مشترك مشفَّر بسرّ لا يعرفه دور التطبيق** (مثلاً DPAPI على مستوى المجال أو ملف PFX بكلمة مرور
   يدخلها المسؤول على كل محطة مرة): كل محطة تختم محلياً، والتسلسل يبقى كما هو اليوم.
3. **الإبقاء على محطة ختم واحدة** وتحويل بقية المحطات إليها للبيع المبسَّط.

## المطلوب من مروان

أي خيار؟ وفي الخيار 1: أي جهاز يكون الخادم، وهل نُجري تسجيل CSID جديداً عليه (يحتاج OTP من بوابة فاتورة)؟


## تسجيلُ الخادم — جاهزٌ بالإعداد (Phase 7)

`ZatcaServerOnboarding` يُجري التسلسلَ كلَّه بلا تدخّلٍ في الكود: مفتاحٌ وCSR (OU = الفرع، CN = اسم وحدة الإصدار، القالبُ بحسب البيئة)
← شهادةُ امتثال بـOTP ← فحوصُ الامتثال (`/compliance/invoices`) لكلّ نوعٍ أعلنه `InvoiceType` (1100 = ستّةُ مستندات، 0100 = ثلاثة)
← شهادةُ إنتاج ← حفظُها في `ZatcaSettings` التي يقرؤها `ZatcaCentralSigner`. ويُشغَّل بـ`POST /zatca-signer/onboard` (بسرّ الموقِّع).

| المفتاح (appsettings / متغيّر بيئة) | المعنى |
| --- | --- |
| `Zatca:Onboarding:Environment` / `BRQOSH_ZATCA_ENVIRONMENT` | `sandbox` (developer-portal، OTP ‏123345 المنشور) · `simulation` · `production` |
| `…:Otp` / `BRQOSH_ZATCA_OTP` | OTP من بوّابة فاتورة — **الخطوةُ الوحيدةُ الباقية: «أدخِل OTP الحقيقيّ عند التسجيل»** |
| `…:Vat`, `…:Cr`, `…:OrganizationName`, `…:Branch`, `…:Address`, `…:BusinessCategory` | حقولُ الـCSR والبائع |
| `…:InvoiceType` (1100), `…:SolutionName` (BRQOSH-ERP), `…:CompanyId` (1), `…:ApiBaseUrl` (اختياريّ) | |

بلا OTP في simulation/production يردّ `AwaitingOtp` ولا يُرسل شيئاً — لا يتوقّف عليه شيءٌ آخر. واختُبر التسلسلُ كاملاً أمام هيئةٍ مُحاكاة
(`TheCentralSignerOnboardsEndToEndFromConfigurationTests`)؛ والبيئةُ التجريبيّةُ الحيّة تُجرَّب بالأمر نفسِه على جهازٍ يبلغ `gw-fatoora.zatca.gov.sa`.

## Simulation preflight (Phase 7) — everything before the OTP, offline

`ZatcaSimulationPreflight.Run(options)` takes the same onboarding settings as `POST /zatca-signer/onboard`
and, without any network call or key on disk, builds the CSR and every compliance sample, stamps the samples
with a throw-away self-signed secp256k1 certificate, then checks them with an independent reader:
CSR signature, named curve `secp256k1` (OID 1.3.132.0.10), subject C/O/OU/CN, certificate template per
environment (TSTZATCA / PREZATCA / ZATCA), the five SAN attributes; per sample: re-computed invoice hash,
signature against the certificate key, QR tags 1–9, ICV sequence and PIH chain.

Run it with `scripts/zatca-simulation-preflight.ps1` (or `dotnet run --project tools/ZatcaPreflight`); it
writes `request.csr`, the signed sample XMLs and `preflight-report.txt`. If the ZATCA SDK CLI (`fatoora`) is
on PATH the script also validates the samples with it. The only remaining step is the real OTP
("enter real OTP on registration").

The preflight found and fixed one defect: the public key in the CSR (and QR tag 8) was encoded with explicit
curve parameters instead of the named curve OID that `openssl ecparam -name secp256k1` produces.
