# CEEF-009 — المرحلة 2: امتثال ZATCA (بداية التنفيذ)

| الحقل | القيمة |
|-------|--------|
| **المرجع** | `CEEF-007-FINAL` — المرحلة 2 (أسابيع 5–8) |
| **التاريخ** | 2026-07-08 |
| **السياق** | بدأت ضمن CEEF-008 — يستكمل هنا |

---

## 1. ما أُنجز في هذه الدفعة

| # | المهمة CEEF-007 | الحالة | الدليل |
|---|-----------------|--------|--------|
| 5 | XAdES-EPES + C14N | 🟡 | `TrySign` موجود — يحتاج SDK معتمد |
| 6 | ICV/PIH ذري | ✅ | `MAX(Icv)+1` · PIH بـ `OrderByDescending(Icv)` |
| 7 | CSID Production OTP | ❌ | يتطلب بيئة + OTP فعلي |
| 8 | إلغاء مستخلص ZATCA | ✅ | `TryGenerateCancellationForProgressBillingAsync` |

### إلغاءات ZATCA — مصفوفة كاملة

| المستند | إلغاء | نوع UBL | الحالة |
|---------|-------|---------|--------|
| فاتورة مبيعات | CN-CANCEL-* | 381 | ✅ |
| إشعار دائن | DN-CANCEL-* | 383 | ✅ |
| مستخلص أعمال | CN-CANCEL-PB-* | 381 | ✅ **جديد** |

---

## 2. اختبارات ZATCA

| الاختبار | الملف |
|----------|-------|
| QR TLV | `ZatcaTests` |
| إلغاء فاتورة | `CancelPostedInvoice_GeneratesZatcaCancellationCreditNote` |
| إلغاء CN → DebitNote | `CancelPostedCreditNote_GeneratesZatcaDebitNote` |
| ICV تسلسلي | `ZatcaGoldenTests.SecondInvoice_IcvIncrementsSequentially` |
| ProfileID + نوع 388 | `ZatcaGoldenTests.FirstInvoice_IcvIsOne_AndXmlContainsProfileId` |

**المجموع:** 7+ سيناريوهات ZATCA (هدف 10 — قريب)

---

## 3. المتبقي للمرحلة 2 (P1 ZATCA)

| ID | الوصف | الأولوية |
|----|--------|----------|
| Z-004 | BillingReference في UBL | P1 |
| Z-006 | توقيع XAdES كامل | P0 إنتاج |
| Z-007 | Hash C14N ZATCA | P0 إنتاج |
| Z-011 | CSID compliance invoices | P1 |
| Z-013 | رؤوس HTTP إرسال | P1 |

---

## 4. قرار المجلس

المرحلة 2 **بدأت** — ICV/PIH وإلغاء المستخلصات **مُغلقة**.  
**لا إطلاق إنتاج ZATCA** حتى XAdES + CSID حي + schematron golden files.

**الانتقال للمرحلة 3:** MSI + PostgreSQL hardening + دليل تركيب — [`CEEF-010`](CEEF-010_المرحلة3_جاهزية_السوق.md)

---

## 5. التقييم المحدّث

| المجال | CEEF-007-FINAL | CEEF-009 |
|--------|----------------|----------|
| ZATCA | 4.5/10 | **5.5/10** |
| المقاولات | 6.0/10 | **6.5/10** |
| الأمان | 5.5/10 | **6.0/10** |
| **الإجمالي** | ~63/100 | **~66/100** |

---

*نهاية CEEF-009 — استكمال المرحلة 2 عند توفر CSID إنتاجي*
