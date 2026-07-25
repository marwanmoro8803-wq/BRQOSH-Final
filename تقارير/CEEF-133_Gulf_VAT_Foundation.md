# CEEF-133 — G-11…G-15 Gulf VAT Matrix (Foundation)

| الحقل | القيمة |
|-------|--------|
| **النطاق** | G-11 UAE · G-12 QA · G-13 BH · G-14 KW · G-15 OM |
| **التاريخ** | 2026-07-10 |
| **الحكم** | **✅ foundation in-repo** · **⏭ e-invoicing حي** |

## المخرجات

- `TaxJurisdiction` enum (6 سلاطات)
- `VatJurisdictionProfiles.All` — معدلات + عملة + سلطة فوترة
- `VatCalculator.FromTaxable(amount, jurisdiction)`
- `VatJurisdictionTests` — SA 15% · AE 5% · BH 10%

## ⏭ خارجي

FTA/QFC/NBR/KTA/Oman APIs — لا تكامل حي
