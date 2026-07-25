# Compliance & Taxes — Luxury Spec (ZATCA-Aligned)

**Feel:** Formal · Audit-ready · Jurisdiction-aware (Saudi ZATCA + Arabic markets)

> **Lifecycle steps:** 41–50 · 92 · see [`LIFECYCLE-ROADMAP-100.md`](../LIFECYCLE-ROADMAP-100.md)  
> **Official specs:** [fatoora.zatca.gov.sa](https://fatoora.zatca.gov.sa) — ZATCA publishes technical requirements and updates officially.

## Layout

```
┌─────────────────────────────────────────────────────────────┐
│ Filing calendar strip — upcoming · overdue · completed        │
├──────────────────────────┬──────────────────────────────────┤
│ VAT / GST summary        │ Exception flags · audit queue     │
│ Output · input · net     │ Document-level compliance status  │
├──────────────────────────┴──────────────────────────────────┤
│ ZATCA transmission log · QR preview · XML · clearance mode    │
└─────────────────────────────────────────────────────────────┘
```

## ZATCA Technical Requirements (UI must surface)

| Requirement | Implementation | UI |
|-------------|----------------|-----|
| UBL 2.1 XML | `ZatcaUblInvoiceBuilder` | XML view/export on `ZatcaInvoicesPage` |
| QR (simplified) | TLV Base64 `ZatcaQrGenerator` | QR preview · POS receipt |
| UUID | per invoice | grid column · detail |
| Hash / PIH / ICV | chain integrity | compliance detail panel |
| CSID / signing | `ZatcaXmlSigner` · CSR wizard | `ZatcaCsidWizardWindow` |
| Submission | Reporting vs Clearance | status badge · mode indicator |
| Archive | signed XML storage | document retention view |

### Mandatory invoice data fields (validation UI)

- Seller VAT (15 digits) · legal name · address
- Buyer data (standard invoices)
- Invoice number · issue date/time (ISO)
- Line: description · qty · unit price · VAT rate · line total
- Document totals: excl VAT · VAT · incl VAT
- Simplified: QR on print/preview

## Compliance Indicators

| Flag | Meaning |
|------|---------|
| Green | Cleared / reported OK |
| Amber | Pending submission |
| Red | Rejected / exception |
| Gold | Audit-ready export |

## WPF Mapping

| Screen | Path |
|--------|------|
| ZATCA settings | `ZatcaSettingsPage` |
| E-invoice log | `ZatcaInvoicesPage` |
| CSID onboarding | `ZatcaCsidWizardWindow` |
| Fiscal periods | `FiscalPeriodsPage` |
| VAT in invoices | `SalesInvoiceDialog` · `CreditNoteDialog` |

## Runbooks

- `docs/ZATCA_LIVE_CSID.md` — OTP/CSID test
- `docs/ops/O-01_ZATCA_PRODUCTION_RUNBOOK.md` — production gate

## Bilingual

- AR: «الامتثال الضريبي» · «فاتورة إلكترونية» · «اعتماد» / «إبلاغ»
- EN: «Tax Compliance» · «E-Invoice» · «Clearance» / «Reporting»

## External gates (⏭)

- ZATCA OTP · Production CSID approval — UI shows real status; never fake «cleared» without submission result
