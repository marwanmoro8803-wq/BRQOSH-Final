# CEEF-048 — المرحلة 9: Webhooks API (أدنى)

| الحقل | القيمة |
|-------|--------|
| **النطاق** | REST webhook subscriptions stub |
| **التاريخ** | 2026-07-09 |
| **الحكم** | **M9 جزئي** — تسجيل اشتراك فقط · dispatch 🔜 |

---

## 1. ما أُغلق

| Endpoint | السلوك |
|----------|--------|
| `GET /api/v1/webhooks/subscriptions` | قائمة الاشتراكات |
| `POST /api/v1/webhooks/subscriptions` | تسجيل eventType + targetUrl |

**الكود:** `Brqosh.Api/Webhooks/WebhookSubscriptionRegistry.cs`  
**اختبارات:** `WebhookSubscriptionApiTests` (2 PASS)

---

## 2. ما لم يُغلق

- إرسال HTTP فعلي عند `DocumentPostedEvent`
- Idempotency keys · HMAC signature

---

## 3. Api tests

**25/25 PASS** (23 + 2 webhook)
