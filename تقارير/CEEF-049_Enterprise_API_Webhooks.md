# CEEF-049 — المرحلة 9: Enterprise API — Webhook Dispatch + Idempotency

| الحقل | القيمة |
|-------|--------|
| **النطاق** | إغلاق M9 أدنى — HTTP dispatch · Idempotency-Key |
| **التاريخ** | 2026-07-09 |
| **الحكم** | **M9 ✅ مغلق (أدنى)** |

---

## 1. ما أُغلق

| # | البند | الدليل |
|---|-------|--------|
| 9.1 | REST `/api/v1/*` | 26 Api tests PASS |
| 9.2 | Webhook subscriptions | `WebhookSubscriptionRegistry` في Infrastructure |
| 9.3 | Event dispatch HTTP | `WebhookDispatcher` + ربط `PersistingDomainEventPublisher` |
| 9.4 | Idempotency keys | `IdempotencyStore` + middleware في `Program.cs` |

### الكود الرئيسي

- `Brqosh.Application/Platform/WebhookContracts.cs` — `IWebhookDispatcher`
- `Brqosh.Infrastructure/Webhooks/WebhookDispatcher.cs`
- `Brqosh.Infrastructure/Webhooks/WebhookSubscriptionRegistry.cs`
- `Brqosh.Infrastructure/Platform/PersistingDomainEventPublisher.cs` — dispatch بعد persist
- `Brqosh.Api/Idempotency/IdempotencyStore.cs`

### الاختبارات الجديدة

| ملف | عدد |
|-----|-----|
| `WebhookDispatcherTests` | 3 PASS |
| `IdempotencyApiTests` | 1 PASS |
| `WebhookSubscriptionApiTests` | 2 PASS (موجود) |

---

## 2. سلوك dispatch

1. عند `PublishAsync` → حفظ في `StoredDomainEvents` → ثم `_webhooks.DispatchAsync`
2. المشتركون يُطابقون بـ `DocumentPostedEvent` أو alias `DocumentPosted` أو `*`
3. POST JSON envelope: `eventId`, `eventType`, `occurredAtUtc`, `payload`
4. فشل شبكة **لا** يلغي الترحيل المحاسبي (LogWarning فقط)

---

## 3. Idempotency

- Header: `Idempotency-Key` على POST `/api/v1/*`
- يُخزَّن الرد 2xx في ذاكرة العملية
- الطلب المكرر يُعاد نفس body/status دون تنفيذ المعالج مرة ثانية

**⏭ لاحقاً (SaaS):** Redis · TTL · HMAC signature على webhook payload

---

## 4. القياس

| المشروع | PASS |
|---------|------|
| Domain | 58 |
| Application | 177 (+3 dispatch) |
| Api | 26 (+1 idempotency) |
| Desktop | 12 |
| **المجموع** | **273** |

`BRQOSH_SKIP_STRESS=1` · Release build · coverlet ≥75%

---

## 5. ما لم يُغلق (⏭ خارج نطاق M9)

- HMAC / secret per subscription
- Retry queue · dead-letter
- Webhook persistence في DB (multi-instance)

**مرجع:** M12 SaaS · `CEEF-035`
