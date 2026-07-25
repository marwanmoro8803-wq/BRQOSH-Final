# CEEF-034 — SAMA FX + سلسلة مزوّدين

| الحقل | القيمة |
|-------|--------|
| **النطاق** | `SamaFxRateProvider` · `ChainedFxRateProvider` · عيّنة JSON |
| **التاريخ** | 2026-07-09 |
| **الحكم** | بنية SAMA جاهزة — التغذية الرسمية الحية ⏭ خارجية |

## ما أُنجز

- `SamaFxRateProvider` — `BRQOSH_SAMA_FX_URL` أو `BRQOSH_SAMA_FX_JSON`
- `ChainedFxRateProvider` — SAMA ثم Frankfurter في DI و Desktop
- عيّنة: `docs/samples/sama-fx-sample.json`
- اختبارات: `SamaFxRateProviderTests`

## أدلة

| الفحص | النتيجة |
|--------|---------|
| Tests | ضمن **245/245 PASS** |

**التالي:** ربط نقطة SAMA الرسمية عند التوفر (O-08 ⏭).
