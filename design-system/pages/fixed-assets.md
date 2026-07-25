# Fixed Assets & Depreciation — Luxury Spec

**Feel:** Elegant asset lifecycle · Timeline depth · Schedule precision

## Layout

```
┌─────────────────────────────────────────────────────────────┐
│ Asset register grid · filters · book value summary KPIs       │
├──────────────────────────┬──────────────────────────────────┤
│ Asset detail             │ Depreciation timeline visual      │
│ Acquisition · life · NBV │ Monthly schedule table            │
│ Movement history         │ Disposal / impairment markers     │
└──────────────────────────┴──────────────────────────────────┘
```

## Visual Elements

- **Timeline:** horizontal bar — acquisition → depreciation phases → disposal
- **Schedule table:** period · depreciation · accumulated · NBV · tabular
- **Cost breakdown:** acquisition · additions · revaluation chips
- **Maintenance notes:** collapsible `ActivityFeedItem` list

## WPF Mapping

`FixedAssetsPage` · `FixedAssetDialog`

## Bilingual

- AR: «الأصول الثابتة» · «الاستهلاك» · EN: «Fixed Assets» · «Depreciation»
