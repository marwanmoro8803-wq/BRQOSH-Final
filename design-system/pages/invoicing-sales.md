# Invoicing & Sales — Luxury Spec

**Feel:** Executive invoice document · Split editor/preview · High transparency

## Layout

```
┌─────────────────────────────────────────────────────────────┐
│ ToolbarCard — New · Templates · Recurring · Export PDF       │
├──────────────────────────┬──────────────────────────────────┤
│ Editor Column (55%)      │ Preview Column (45%)              │
│ Customer selector        │ PDF-style preview panel           │
│ Line-item grid           │ Totals · tax breakdown            │
│ Tax · discount rules     │ Payment status badge              │
│ Smart auto-fill hints    │ Send / print actions              │
└──────────────────────────┴──────────────────────────────────┘
│ Customer history strip · linked credit notes · activity       │
└─────────────────────────────────────────────────────────────┘
```

## Components

| Zone | Pattern |
|------|---------|
| Invoice header | `GlassPanel` · invoice # · date · customer chip |
| Line editor | DataGrid 44px · product · qty · unit · VAT · line total · tabular |
| Tax engine | Inline VAT calc · ZATCA-ready indicators |
| Templates | `QuickActionChip` row · recurring badge |
| Preview | `ContentCard` pearl/doc surface · executive typography |
| Payment status | `StatusBadge` — draft · posted · paid · overdue |

## Interactions

- Customer selector: searchable combo + recent customers
- Auto-fill: last price · default VAT · payment terms
- Discount: line or document level with validation
- Recurring: schedule chip on toolbar
- PDF preview: live update on line change (debounced 200ms)

## WPF Mapping

| Screen | Path |
|--------|------|
| List | `SalesInvoicesPage` |
| Dialog | `SalesInvoiceDialog` |
| Styles | `ToolbarCard` · `ContentCard` · `TabularText` · `FormSectionTitle` |

## Bilingual

- AR: «فاتورة مبيعات» · EN: «Sales Invoice»
- Line headers localized · numbers always tabular Western in EN

## Anti-patterns

- Cluttered preview with editor chrome bleeding through
- Non-tabular line totals
- Emoji payment icons
