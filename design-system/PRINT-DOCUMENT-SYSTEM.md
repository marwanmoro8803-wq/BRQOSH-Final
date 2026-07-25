# BRQOSH Print & PDF Document System (v2.1 Implemented)

> **Authority:** Binding spec for all printable outputs — invoices, vouchers, journals, reports, ZATCA artifacts  
> **Engine:** QuestPDF (`Brqosh.Infrastructure/Reporting/`) · QRCoder (ZATCA QR PNG)  
> **Service:** `DocumentPrintService` · Desktop delivery: `DocumentPrintHelper` + WebView2 preview  
> **Agent skill:** `.cursor/skills/brqosh-ui-design-system/print-document-system.md`  
> **Compliance:** ZATCA · SOCPA audit trail · Saudi VAT 15%

---

## 1. Current workflow (implemented)

### Architecture

```
UI Page (OnPrintPdf / OnExportPdf)
    → DocumentPrintHelper.ExportAndDeliverAsync(...)
        → DocumentPrintService (load DB · generate PDF · AuditService PrintPdf)
    → DocumentPdfPreviewWindow (WebView2 embedded PDF)
        → طباعة: CoreWebView2.ShowPrintUI (system print dialog)
        → حفظ باسم / فتح خارجياً (fallback)
```

**Facts:**
- Central `DocumentPrintService` registered in DI
- Print audit via `AuditService` action `PrintPdf`
- **WebView2** embedded preview on all print/export flows (default)
- **System print dialog** from preview (`طباعة` button)
- Company branding: logo · address · print font from **إعدادات المنشأة**
- Templates remain inline QuestPDF C# — no external RDLC/HTML

### Document registry

| Document | PDF generator | UI trigger | Status |
|----------|---------------|------------|--------|
| Sales invoice | `SalesInvoicePdfGenerator` + QR image | `SalesInvoicesPage` | ✅ ZATCA QR |
| Purchase invoice | `SalesInvoicePdfGenerator` (Purchase) | `PurchaseInvoicesPage` | ✅ |
| Credit note | `SalesInvoicePdfGenerator` (CreditNote) + QR | `CreditNotesPage` | ✅ |
| Debit note | `SalesInvoicePdfGenerator` (PurchaseDebitNote) | `DebitNotesPage` | ✅ |
| Progress billing | `ProgressBillingPdfGenerator` + QR | `ProgressBillingsPage` | ✅ |
| Receipt voucher | `VoucherPdfGenerator` | `ReceiptVouchersPage` | ✅ |
| Payment voucher | `VoucherPdfGenerator` | `PaymentVouchersPage` | ✅ |
| Journal entry | `JournalEntryPdfGenerator` | `JournalEntryViewDialog` | ✅ |
| POS receipt | `PosReceiptWindow` (screen) + PDF via service | `PosSessionPage` | ✅ |
| ZATCA e-invoice | routes to source doc PDF | `ZatcaInvoicesPage` | ✅ |
| Trial balance | `TrialBalancePdfGenerator` | `TrialBalancePage` | ✅ |
| Income statement | `FinancialReportPdfGenerator` | `IncomeStatementPage` | ✅ |
| Balance sheet | `FinancialReportPdfGenerator` | `BalanceSheetPage` | ✅ |
| General ledger | `FinancialReportPdfGenerator` | `GeneralLedgerPage` | ✅ |
| VAT return | `FinancialReportPdfGenerator` | `VatReturnPage` | ✅ |
| Customer statement | `StatementPdfGenerator` | `CustomerStatementPage` | ✅ |
| Supplier statement | `StatementPdfGenerator` | `SupplierStatementPage` | ✅ |
| Cash flow | `FinancialReportPdfGenerator.CashFlowStatement` | `CashFlowStatementPage` | ✅ |
| Consolidated TB/IS/BS | `ConsolidatedReportPdfGenerator` | consolidated pages | ✅ |
| POS daily report | `PosReportPdfGenerator` | `PosReportsPage` | ✅ |

### Code map

```
src/Brqosh.Infrastructure/Reporting/
  DocumentPrintService.cs (+ DocumentPrintService.Reports.cs)
  SalesInvoicePdfGenerator.cs · ZatcaQrImageHelper.cs · PrintBranding.cs
  VoucherPdfGenerator.cs · JournalEntryPdfGenerator.cs
  ProgressBillingPdfGenerator.cs · ConsolidatedReportPdfGenerator.cs
  FinancialReportPdfGenerator.cs · TrialBalancePdfGenerator.cs · StatementPdfGenerator.cs
  PosReportPdfGenerator.cs

src/Brqosh.Desktop/Reporting/
  DocumentPrintHelper.cs · DocumentPdfPreviewWindow.xaml(.cs) · PdfWebViewHelper.cs
```

---

## 2. Remaining gaps (honest closure)

| ID | Gap | Status |
|----|-----|--------|
| G07 | Arabic print font from settings | ✅ `Company.PrintFontFamily` · Tahoma/Arial/Segoe UI/Traditional Arabic |
| G08 | Company logo from settings | ✅ `CompanySettingsPage` → all PDF headers |
| G09 | Print audit | ✅ `AuditService` |
| G10 | Mislabeled doc types | ✅ `PrintDocumentKind` |
| — | Embedded WebView2 PDF preview | ✅ `DocumentPdfPreviewWindow` |
| — | System print dialog on PDF | ✅ WebView2 `ShowPrintUI` · زر «طباعة» |
| — | Custom TTF font upload | ⏭ phase 2 (system fonts only) |
| O-01 | Live ZATCA OTP validation | ⏭ EXTERNAL · `dist/evidence/O-01-*/` |

---

## 3. Document taxonomy (target system)

### Tier A — Statutory / ZATCA (highest rigor)

| DocType ID | Arabic | Paper | QR | UBL link |
|------------|--------|-------|-----|----------|
| `INV-SMP` | فاتورة ضريبية مبسّطة | A5/A4 | **MUST** TLV QR image | required |
| `INV-STD` | فاتورة ضريبية | A4 | optional phase | required |
| `CN` | إشعار دائن | A4 | **MUST** if simplified | billing ref |
| `DN` | إشعار مدين | A4 | per ZATCA rules | billing ref |
| `PB` | مستخلص أعمال | A4 | per mode | progress billing |

### Tier B — Treasury vouchers

| DocType ID | Arabic | Paper | Signature |
|------------|--------|-------|-----------|
| `RV` | سند قبض | A5 | receiver + accountant |
| `PV` | سند صرف | A5 | payer + accountant |
| `CHQ` | شيك (future) | A5 | payee line |

### Tier C — Accounting records

| DocType ID | Arabic | Paper |
|------------|--------|-------|
| `JE` | قيد يومية | A4 landscape |
| `TB` | ميزان مراجعة | A4 |
| `GL` | دفتر أستاذ | A4 landscape |
| `IS` | قائمة دخل | A4 |
| `BS` | مركز مالي | A4 |
| `VAT` | إقرار ضريبة | A4 |
| `CUST-STMT` | كشف حساب عميل | A4 landscape |
| `SUPP-STMT` | كشف حساب مورد | A4 landscape |

### Tier D — Operational (future)

PO · GRN · quotation · payroll slip · fixed asset tag

---

## 4. Universal print layout (all documents)

Every PDF MUST share this skeleton:

```
┌─────────────────────────────────────────────────────────┐
│ ZONE A — Letterhead (fixed height)                       │
│   [Logo]  Company legal name AR (+ EN optional)          │
│   VAT 15-digit · CR · address · branch                   │
├─────────────────────────────────────────────────────────┤
│ ZONE B — Document identity band                          │
│   Doc type (AR primary) · number · date · time (ZATCA)   │
│   Status badge: مسودة | مرحّل | ملغى                      │
├─────────────────────────────────────────────────────────┤
│ ZONE C — Parties (when applicable)                       │
│   Seller/buyer · customer/supplier · cost center         │
├─────────────────────────────────────────────────────────┤
│ ZONE D — Body (table or narrative)                       │
│   lines · amounts tabular · RTL column order             │
├─────────────────────────────────────────────────────────┤
│ ZONE E — Totals band (financial docs)                    │
│   subtotal · VAT 15% · grand total · amount in words      │
├─────────────────────────────────────────────────────────┤
│ ZONE F — Compliance strip (ZATCA docs)                   │
│   QR 25–35mm · UUID · ICV · hash snippet · mode badge    │
├─────────────────────────────────────────────────────────┤
│ ZONE G — Footer (fixed)                                  │
│   printed at · printed by · page x of y · branch         │
│   "Generated by BRQOSH" · optional EN mirror line        │
└─────────────────────────────────────────────────────────┘
```

**MUST NOT:** marketing hero · gradients · emoji · decorative graphics on statutory docs.

---

## 5. ZATCA print compliance matrix

| Field | Source | Print zone | Rule |
|-------|--------|------------|------|
| Seller name | `Company.NameAr` | A | mandatory |
| VAT number | 15 digits | A | mandatory |
| Invoice number | document | B | mandatory |
| Issue datetime | UTC ISO | B | mandatory |
| Line items | document lines | D | qty · price · VAT · total |
| Subtotal / VAT / Total | computed | E | tabular · 2 decimals |
| QR TLV | `ZatcaEInvoiceDocument.QrTlvBase64` | F | **scannable image** — not text |
| UUID | ZATCA doc | F | standard invoices |
| ICV / PIH | ZATCA doc | F | audit chain |
| Transmission mode | Reporting/Clearance | F | badge |

**Simplified invoice:** QR image MUST print. Current `SalesInvoicePdfGenerator` footer text is **non-compliant** — replace with QRCoder bitmap (same as `PosReceiptWindow`).

Reference: `docs/ZATCA_LIVE_CSID.md` · `design-system/pages/compliance-tax.md`

---

## 6. Voucher layout spec (سند قبض / سند صرف)

Current `VoucherPdfGenerator` — upgrade target:

| Block | Receipt (قبض) | Payment (صرف) |
|-------|---------------|---------------|
| Title | سند قبض | سند صرف |
| Party label | العميل | المورد |
| Amount | prominent center · tabular | same |
| Method | نقداً / بنك / شيك | same |
| Reference | check/transfer ref | same |
| Description | البيان | same |
| Signatures | المستلم · المحاسب · المدير | المعتمد · المحاسب |
| JE link | قيد رقم … | same |

**MUST** print only **posted** vouchers unless user explicitly requests draft watermark.

---

## 7. Report layout spec

| Element | Rule |
|---------|------|
| Orientation | landscape for GL · statements · wide TB |
| Header repeat | company + report title every page |
| Column headers | repeat on page break |
| Amounts | right-aligned AR · tabular N2 |
| Footer | period · branch filter · run timestamp |
| Balance check | TB/BS show balanced ✓/✗ |

---

## 8. Commands & UX (target)

### Per-document actions

| Action | Behavior |
|--------|----------|
| **Print PDF** | generate → preview tab OR save temp |
| **Save PDF** | `SaveFileDialog` default name `{DocType}-{Number}-{Date}.pdf` |
| **Print** | WebView2 `ShowPrintUI` — system print dialog from preview | ✅ |
| **Email** | attach PDF (future) |

### Shared service (target API)

```csharp
public interface IDocumentPrintService
{
    Task<PrintResult> ExportAsync(PrintRequest request, CancellationToken ct);
    // PrintRequest: DocType, EntityId, Options(IncludeQr, Language, Watermark)
}
```

**MUST** log: `PrintAuditEntry` — user · doc type · id · timestamp.

---

## 9. Typography & paper

| Setting | Value |
|---------|-------|
| Statutory body | 11pt · Tahoma/Segoe UI AR |
| Titles | 14–16pt SemiBold |
| Amounts | tabular · 2 decimal SAR |
| Simplified invoice | A5 portrait (thermal-compatible scaling) |
| Standard / reports | A4 |
| Margins | 24mm statutory · 20mm reports |
| Bilingual | AR primary · EN subtitle in footer zone G |

---

## 10. Implementation phases

| Phase | Deliverable | CEEF |
|-------|-------------|------|
| **P0** | `PrintDocumentBranding` shared header/footer · embed QR in invoice PDF | — |
| **P1** | `DocumentPrintService` · Save As · ZATCA lookup on sales/credit print | — |
| **P2** | Separate layouts: INV-STD · CN · DN · PV purchase | — |
| **P3** | `JournalEntryPdfGenerator` · debit note print | — |
| **P4** | Print audit trail · system print via WebView2 | ✅ |
| **P5** | Cash flow · consolidated · POS report PDFs | — |
| **P6** | Live PDF preview panel (`invoicing-sales.md` spec) | ✅ WebView2 |

---

## 11. Verify gate

```
□ Posted-only guard on statutory print
□ ZATCA simplified shows scannable QR
□ Amounts tabular · VAT 15% correct
□ Document title matches type (not wrong reuse)
□ Footer: printed by + timestamp
□ AR typography readable · A4/A5 correct
□ dotnet test FinancialReportPdfTests PASS
□ Manual: scan QR with ZATCA validator app (⏭ live O-01)
```

---

## 12. Related specs

- `design-system/pages/invoicing-sales.md` — editor + PDF preview
- `design-system/pages/compliance-tax.md` — ZATCA UI
- `design-system/pages/bank-cash.md` — vouchers UI
- `design-system/pages/reports-analytics.md` — export chips
- `LIFECYCLE-ROADMAP-100.md` — steps 26–50 · 61–70
