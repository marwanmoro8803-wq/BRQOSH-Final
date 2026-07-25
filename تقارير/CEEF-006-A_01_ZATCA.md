# CEEF-006-A — المراجعة العميقة (1) ZATCA / Fatoora

**الدرجة: 3.5 / 10** — توليد موجود؛ **غير جاهز للإنتاج أو للمرحلة الثانية**.

---

## ما فُهم من التصميم

```
ترحيل مستند → UnitOfWork Commit → ZatcaEInvoiceGenerator
  → ICV/PIH/UUID → UBL → QR (Phase 1) → Hash → TrySign → DB + ملف XML
  → ZatcaAutoSubmissionHelper → ZatcaSubmissionService
```

**الملفات المراجعة:** `Infrastructure/Zatca/*` · `Domain/Zatca/*` · `ZatcaInvoicesPage` · `ZatcaSettingsPage` · `ZatcaCsidWizardWindow` · اختبارات `Zatca*`.

---

## P0 — حرجة

### Z-001 — إلغاء فاتورة مبيعات: لا يُولَّد مستند ZATCA

| الحقل | القيمة |
|-------|--------|
| **الملف** | `SalesInvoiceCancellationService.cs:66` · `ZatcaEInvoiceGenerator.cs:137` |
| **الدليل** | `MarkCancelled()` ثم `TryGenerateCancellationForSalesInvoiceAsync` يشترط `Status == Posted` |
| **السيناريو** | إلغاء فاتورة مُبلَّغ عنها للهيئة |
| **التأثير** | مخالفة Fatoora؛ غرامات؛ سجل ضريبي غير متطابق |
| **Test Case** | Post + ZATCA → Cancel → assert مستند `CN-CANCEL-*` بـ ICV جديد |

### Z-002 — إلغاء إشعار دائن: تعارض فهرس فريد في DB

| الحقل | القيمة |
|-------|--------|
| **الملف** | `BrqoshDbContext.cs:328` · `ZatcaEInvoiceGenerator.cs:101-130` |
| **الدليل** | `HasIndex(CompanyId, DocumentType, SourceDocumentId).IsUnique()` — إشعار مرحّل + إلغاء يحاولان نفس المفتاح |
| **السيناريو** | ترحيل CN مع ZATCA → إلغاء CN |
| **التأثير** | `SaveChanges` فشل أو عدم إنشاء مستند إلغاء |
| **Test Case** | Post CN → ZATCA doc → Cancel CN → expect debit/cancel doc أو فشل مُثبت |

### Z-003 — لا دعم إشعار مدين (383) لعكس إشعار دائن

| الحقل | القيمة |
|-------|--------|
| **الملف** | `ZatcaUblInvoiceBuilder.cs:36-37` · `ZatcaEnums.cs` |
| **الدليل** | `InvoiceTypeCode` فقط `388` / `381` |
| **التأثير** | رفض الهيئة عند إلغاء CN |
| **Test Case** | Parse XML → assert type `383` لإلغاء CN |

---

## P1 — عالية

| ID | المشكلة | الملف | السطر |
|----|---------|-------|-------|
| Z-004 | UBL ناقص: BillingReference، عناوين، TaxSubtotal | `ZatcaUblInvoiceBuilder.cs` | 33-94 |
| Z-005 | QR Phase 1 فقط (tags 1-5) | `ZatcaQrGenerator.cs` | 9-35 |
| Z-006 | توقيع XML غير متوافق (RSA/ECDSA مختلط، XAdES مبسّط) | `ZatcaXmlSigner.cs` | 17-45 |
| Z-007 | Hash على XML كامل لا C14N ZATCA | `ZatcaEInvoiceGenerator.cs` | 190-191 |
| Z-008 | PIH أولي `ZeroHash` قد لا يطابق ثابت الهيئة | `ZatcaQrGenerator.cs` | 47 |
| Z-009 | ICV = Count+1؛ PIH بـ OrderBy Id | `ZatcaEInvoiceGenerator.cs` | 172-176 |
| Z-010 | إلغاء مستخلص بلا ZATCA | `ProgressBillingCancellationService.cs` | 13-36 |
| Z-011 | CSID بدون خطوة compliance invoices | `ZatcaCsidOnboardingService.cs` | كامل |
| Z-012 | CSR بدون SAN/serial ZATCA | `ZatcaCsidOnboardingService.cs` | 27-57 |
| Z-013 | رؤوس HTTP ناقصة للإرسال | `ZatcaSubmissionService.cs` | 89-94 |

---

## Current vs Best Practice vs Recommended

| البند | الحالي | أفضل ممارسة ZATCA | التوصية |
|-------|--------|-------------------|---------|
| ICV | `CountAsync+1` | `MAX(Icv)+1` ذري | Sequence أو قفل صف |
| PIH | `OrderByDescending(Id)` | آخر ICV | `OrderByDescending(Icv)` |
| إلغاء فاتورة | شرط Posted بعد Cancel | Credit Note مرتبط UUID أصلي | توليد قبل تغيير الحالة |
| التوقيع | TrySign اختياري | XAdES-EPES إلزامي Phase 2 | SDK/مكتبة معتمدة |
| الاختبارات | Simulation فقط | Golden XML + schematron | فصل suite إنتاج |

---

## P2 / P3 (عينة)

| ID | الوصف | الخطورة |
|----|--------|---------|
| Z-014 | Clearance/Reporting مبني على VAT العميل فقط | P2 |
| Z-015 | فشل إرسال يُبتلع في `ZatcaAutoSubmissionHelper` | P2 |
| Z-016 | Simulation يعلّم Reported دون تحقق | P2 |
| Z-017 | Progress Billing في UBL = فاتورة 388 عادية | P2 |
| Z-018 | UI يعرض PB كـ«إشعار دائن» | P2 |
| Z-019 | Token API plaintext في DB | P3 |
| Z-020 | `PosSaleService` استدعاء submit مزدوج | P3 |

---

## تغطية الاختبارات

| المنطقة | مغطى؟ | الملف |
|---------|-------|-------|
| QR TLV | جزئي | `ZatcaTests` |
| توليد فاتورة | نعم (simulation) | `ZatcaTests`, `SalesInvoicePostingTests` |
| POS B2C | نعم | `PosZatcaTests` |
| مستخلص + ZATCA | إنشاء فقط | `ProgressBillingVatTests` |
| UBL XSD | **لا** | — |
| ICV/PIH chain | **لا** | — |
| إلغاءات ZATCA | **لا** | — |
| CSID إنتاج | اختياري | `ZatcaLiveCsidTests` |

---

## Test Cases حرجة (مجموعة)

1. Parallel 10 invoices → ICV فريد + سلسلة PIH.
2. Cancel sales invoice → XML credit مع BillingReference لـ UUID الأصلي.
3. Cancel credit note → debit 383 بدون unique violation.
4. Cancel progress billing → مستند ZATCA عكسي.
5. First invoice → PIH = ثابت الهيئة الرسمي.
6. Signed XML → تحقق hash بقواعد C14N.
7. Production headers → integration mock يتحقق من `Accept-Version`.

---

## أولويات الإصلاح

1. Z-001، Z-002، Z-003  
2. Z-004 → Z-007 (UBL + hash + QR + sign)  
3. Z-008، Z-009  
4. Z-010، Z-011، Z-012  

---

**الخلاصة:** البنية التحتية لـ ZATCA **قابلة للتطوير** لكن الامتثال الحالي **شكلي** في Simulation ولا يصمد أمام تدقيق الهيئة أو المرحلة الثانية.
