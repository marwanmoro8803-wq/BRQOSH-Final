# Print Document System (v18 Strict)

> **Binding:** [enforcement.md](enforcement.md) · **Canonical:** [PRINT-DOCUMENT-SYSTEM.md](../../../design-system/PRINT-DOCUMENT-SYSTEM.md)  
> **Scope:** invoices · vouchers · journals · reports · ZATCA print artifacts

---

## When to read

- Any task touching PDF, print, voucher layout, invoice print, ZATCA QR on paper
- Before modifying `*PdfGenerator.cs` or `OnPrintPdf` handlers
- Before claiming print/ZATCA compliance

---

## Immutable print laws

| ID | Law |
|----|-----|
| PL01 | **Statutory truth** — printed amounts MUST match posted document — no preview mock as final |
| PL02 | **ZATCA simplified** — QR MUST be scannable image (TLV), not placeholder text |
| PL03 | **Document type fidelity** — title/layout MUST match doc type (no sales shell for purchase/credit) |
| PL04 | **Posted default** — print statutory docs posted only unless DRAFT watermark explicit |
| PL05 | **Audit footer** — printed by · timestamp · branch on every page |
| PL06 | **Tabular money** — all amounts N2 · SAR · right-aligned AR |
| PL07 | **No marketing layout** on print — letterhead + tables only |
| PL08 | **Central service target** — new print paths MUST use `IDocumentPrintService` when P1 lands |

---

## Document location map (verified)

### Treasury — receipt and payment vouchers

| Document | Page | Generator | Handler |
|----------|------|-----------|---------|
| Receipt voucher | `ReceiptVouchersPage` | `VoucherPdfGenerator.ReceiptVoucher` | `OnPrintPdf` |
| Payment voucher | `PaymentVouchersPage` | `VoucherPdfGenerator.PaymentVoucher` | `OnPrintPdf` |

Dialogs (create only, no print): `ReceiptVoucherDialog` · `PaymentVoucherDialog`

### Sales / purchasing

| Document | Page | Generator | Gap |
|----------|------|-----------|-----|
| Sales invoice | `SalesInvoicesPage` | `SalesInvoicePdfGenerator` | no ZATCA QR load |
| Purchase invoice | `PurchaseInvoicesPage` | reuses sales | wrong title |
| Credit note | `CreditNotesPage` | reuses sales | no CN layout |
| Debit note | `DebitNotesPage` | **none** | ❌ |
| POS | `PosSessionPage` + `PosReceiptWindow` | screen QR ✅ · PDF placeholder | ⚠️ |

### Accounting reports

| Report | Page | Generator |
|--------|------|-----------|
| Trial balance | `TrialBalancePage` | `TrialBalancePdfGenerator` |
| Income statement | `IncomeStatementPage` | `FinancialReportPdfGenerator` |
| Balance sheet | `BalanceSheetPage` | `FinancialReportPdfGenerator` |
| General ledger | `GeneralLedgerPage` | `FinancialReportPdfGenerator` |
| VAT return | `VatReturnPage` | `FinancialReportPdfGenerator` |
| Customer statement | `CustomerStatementPage` | `StatementPdfGenerator` |
| Supplier statement | `SupplierStatementPage` | `StatementPdfGenerator` |

### Missing print

Journal entry · debit note · cash flow · consolidated · POS reports · progress billing · ZATCA page print

---

## Layout zones (MUST follow)

```
A Letterhead → B Identity → C Parties → D Body → E Totals → F ZATCA → G Footer
```

See full spec: `design-system/PRINT-DOCUMENT-SYSTEM.md` §4

---

## ZATCA print checklist

```
□ Seller name + 15-digit VAT in letterhead
□ Invoice number + ISO datetime
□ Line table: description · qty · unit · VAT · line total
□ Totals: excl · VAT 15% · incl
□ QR image from QrTlvBase64 (QRCoder — copy PosReceiptWindow pattern)
□ UUID · ICV on standard invoices
□ Credit note: reference to original invoice
```

**MUST NOT** claim ZATCA print compliance without scannable QR on simplified invoices.

---

## Voucher print checklist

```
□ Title: receipt voucher or payment voucher (not generic) — bilingual via LocaleManager.T in product
□ Party: customer or supplier name
□ Amount centered · tabular
□ Method + reference + description
□ Signature lines (target P1)
□ Link to journal entry number when posted
```

---

## Agent workflow (print task)

```
1. CLASSIFY — which DocType ID (INV-SMP · RV · JE · …)
2. READ    — PRINT-DOCUMENT-SYSTEM.md + compliance-tax.md + page spec
3. LOCATE  — generator + page handler (grep OnPrintPdf)
4. CHECK   — gap table — do not fake compliance
5. BUILD   — shared branding first · smallest diff
6. VERIFY  — FinancialReportPdfTests · manual PDF open · QR scan ⏭ live
```

---

## QR embedding pattern (required fix)

Copy from `PosReceiptWindow.xaml.cs`:

```csharp
// Infrastructure layer: inject byte[] qrPng into QuestPDF footer
// SalesInvoicesPage: load ZatcaEInvoiceDocument.QrTlvBase64 before Generate()
```

**MUST NOT** leave a text-only ZATCA QR placeholder footer (e.g. embedded-QR text stub).

---

## Regulatory alignment

| Authority | Print obligation |
|-----------|------------------|
| **ZATCA** | simplified QR · UBL fields · VAT 15% |
| **SOCPA** | audit trail · JE printable · who/when footer |
| **SAMA** (bank vouchers) | reference · method · reconciliation link |
| Internal audit | print log · posted-only default |

---

## Phase priority (implement in order)

1. **P0** — QR in `SalesInvoicePdfGenerator` + shared `PrintBranding`
2. **P1** — `DocumentPrintService` + ZATCA lookup on sales/credit print
3. **P2** — separate doc type layouts
4. **P3** — journal + debit note PDF
5. **P4** — print audit + Save As

Full roadmap: `design-system/PRINT-DOCUMENT-SYSTEM.md` §10
