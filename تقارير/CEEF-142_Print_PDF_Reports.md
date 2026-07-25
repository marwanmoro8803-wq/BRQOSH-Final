# CEEF-142 — طباعة · PDF · تقارير

| الحقل | القيمة |
|-------|--------|
| **النطاق** | محور I — DocumentPrintService vs PRINT-DOCUMENT-SYSTEM |
| **التاريخ** | 2026-07-10 |
| **الحكم** | **B+** — registry ~19/19 implemented · spec gaps remain |

---

## 1. Registry alignment (PRINT-DOCUMENT-SYSTEM §1)

| Document type | Implemented | Ref |
|---------------|-------------|-----|
| Sales/Purchase invoice, CN/DN | ✅ | `DocumentPrintService.cs:18-138` |
| Receipt/Payment voucher | ✅ | `:140-182` |
| Journal entry | ✅ | `:184-223` |
| Financial reports (TB/IS/BS/GL/VAT) | ✅ | `DocumentPrintService.Reports.cs:12-73` |
| Consolidated reports | ✅ | `:103-141` |
| Progress billing + QR | ✅ | `:143-176` |
| ZATCA doc router | ✅ | `:178-191` |

**Test:** `FinancialReportPdfTests.cs` — consolidated + progress billing PDF bytes.

---

## 2. Spec gaps (honest)

| Gap | Spec | Code evidence |
|-----|------|---------------|
| Tier D operational PDFs (PO, GRN, quotation, payroll slip) | §122-124 | No Export* methods |
| Cheque print (CHQ) | §107 future | No generator |
| Unified IDocumentPrintService | §228-236 | Per-doc Export*Async only |
| Posted-only print guard | §200 | No status check on load |
| Voucher signature blocks | §187-198 | VoucherPdfGenerator — المستلم only |
| POS receipt via service | §44 | PosReceiptWindow screen-only |
| Email PDF attachment | §226 | Future |

---

## 3. Findings

| ID | Severity | Claim |
|----|----------|-------|
| AUDIT-PRT-001 | P1 | Posted-only guard missing — draft docs printable |
| AUDIT-PRT-002 | P2 | Tier D PDFs not implemented |
| AUDIT-PRT-003 | P2 | Voucher signature blocks incomplete vs spec |
| AUDIT-PRT-004 | P1 | G-40 bilingual PDF depth unverified |

---

## 4. Closure

| Level | Status |
|-------|--------|
| ✅ Code | Core registry implemented |
| ✅ Test | FinancialReportPdfTests PASS |
| ⏭ Live | Print on customer VM — O-03 |
