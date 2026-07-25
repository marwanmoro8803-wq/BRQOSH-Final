# CEEF-032 — مصادر أسعار FX حية (O-08)

| الحقل | القيمة |
|-------|--------|
| **النطاق** | `IFxRateProvider` · Frankfurter · `FxRateSyncService` · API · Desktop |
| **التاريخ** | 2026-07-08 |
| **الحكم** | **تعميق O-08 مُثبت** — O-08 **ليست** مغلقة بالكامل |

---

## 1. ما أُنجز

### معمارية مزوّدين قابلة للتبديل

| المكوّن | الملف |
|---------|-------|
| `FxRateQuote` · `IFxRateProvider` · `IFxRateSyncService` | `TreasuryContracts.cs` |
| مزوّد Frankfurter (ECB، مجاني) | `FrankfurterFxRateProvider.cs` |
| مزامنة وحفظ في `exchange_rates` | `FxRateSyncService.cs` |
| `StubFxRateProvider` (اختبارات) | `StubFxRateProvider.cs` |

### API

| Endpoint | الغرض |
|----------|--------|
| `GET /api/v1/companies/{id}/exchange-rates` | قائمة الأسعار المخزّنة |
| `POST /api/v1/companies/{id}/exchange-rates/sync` | جلب حي + تخزين (`asOf` · `currencies` اختياري) |

### Desktop

| الصفحة | المسار |
|--------|--------|
| `ExchangeRatesPage` | الخزينة والبنوك → **أسعار الصرف** |
| زر «جلب أسعار حية» | يستدعي Frankfurter ويخزّن |

### منطق الأهداف التلقائي

عند عدم تحديد عملات: USD/EUR/GBP/AED + عملة التقرير للمجموعة + عملات الحسابات البنكية الأجنبية.

---

## 2. أدلة تشغيل

| الفحص | النتيجة |
|--------|---------|
| Domain | **51/51 PASS** |
| Application | **160/160 PASS** (`BRQOSH_SKIP_STRESS=1` · `BRQOSH_TEST=1`) |
| Api | **19/19 PASS** |
| **المجموع** | **230/230 PASS** |
| Desktop Release | **0** أخطاء |

### اختبارات جديدة

- `FxRateSyncServiceTests` — تسجيل من Stub + اكتشاف عملات البنوك

---

## 3. ما لم يُغلق — صريح

| البند | السبب |
|-------|--------|
| O-08 كامل | تغذية SAMA/بنك مركزي رسمية (مزوّد منفصل) · خرائط حسابات IC |
| O-01…O-05 | خارجي |
| O-06, O-09…O-13 | كما في OPEN_ITEMS |

> Frankfurter = بيانات ECB للتطوير والتجميع؛ **ليس** بديل اعتماد SAMA الإنتاجي.

---

## 4. الحكم

| الادعاء | الحكم |
|---------|--------|
| بنية مزوّد FX حي + مزامنة + واجهة | ✅ داخل المستودع |
| O-08 مغلقة بالكامل | ❌ |
| O-01…O-06 · O-09…O-13 مغلقة | ❌ |

**التالي:** خرائط حسابات IC (O-08) **أو** تنفيذ حي O-01…O-05.
