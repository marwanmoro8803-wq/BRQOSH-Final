# CEEF-123 — هجرة Tier P0 (المسارات المالية) إلى الواجهة

| الحقل | القيمة |
|-------|--------|
| **النطاق** | E1 من CEEF-120 — Phase 2b (Tier P0) |
| **التاريخ** | 2026-07-10 |
| **الحكم** | **✅ مغلق (كود + اختبار)** |

## الملفات المُهاجَرة (11 ملفاً جديداً + 4 من CEEF-109 = 15)

| ملف | الخدمة | العملية |
|-----|--------|---------|
| `CreditNoteDialog` | CreditNotePostingService | ترحيل إشعار دائن |
| `CreditNotesPage` | CreditNoteCancellationService | إلغاء |
| `DebitNoteDialog` | DebitNotePostingService | ترحيل إشعار مدين |
| `DebitNotesPage` | DebitNoteCancellationService | إلغاء |
| `ReceiptVoucherDialog` | ReceiptVoucherPosting + PaymentAllocation | سند قبض + تخصيصات |
| `ReceiptVouchersPage` | VoucherReversalService | عكس قبض |
| `PaymentVoucherDialog` | PaymentAllocation (القسم المتبقي) | تخصيصات صرف |
| `PaymentVouchersPage` | VoucherReversalService | عكس صرف |
| `SalesInvoicesPage` | SalesInvoiceCancellationService | إلغاء فاتورة بيع |
| `PurchaseInvoicesPage` | PurchaseInvoiceCancellationService | إلغاء فاتورة شراء |
| `FiscalPeriodsPage` | YearEndClose + FiscalPeriod | إقفال سنة · إعادة فتح فترة |
| `FxRevaluationDialog` | FxRevaluationService | إعادة تقييم عملة |

**النمط الموحّد:**

```csharp
using var facade = new DesktopAccountingFacade();
await facade.GetService<XxxService>().DoAsync(...);
```

## بوابة الانحدار الجديدة

`tests/Brqosh.Desktop.Tests/DesktopDiGuardTests.cs`:
- **Theory على 15 ملفاً** — يفشل البناء المعرفي إذا عاد أي ملف مُهاجَر لنمط الإنشاء اليدوي
- **فحص الواجهة نفسها** — `GetRequiredService` إلزامي

## الدليل

| المستوى | الحالة | الدليل |
|---------|--------|--------|
| ✅ كود | تم | 11 ملف مُهاجَر + guard test |
| ✅ اختبار | **Desktop 96/96 PASS** (80 + 16 guard) · الحزمة الكاملة 433 | `dist/evidence/CEEF-120-audit/dotnet-test-after-p0.log` |
| ✅ حي | — | تشغيل Desktop يدوي عند أول جلسة مستخدم |
| ⏭ خارجي | — | — |

## المتبقي (CEEF-124 — Tier P1/P2/P3)

~40 ملفاً غير مالي مباشر (`inventory` في `dist/evidence/CEEF-120-audit/desktop-di-inventory.csv`): POS · Cheques · Stock · Payroll · FixedAssets · Construction · Users/Login — تدريجياً بنفس النمط مع توسيع `MigratedTierP0Files`.
