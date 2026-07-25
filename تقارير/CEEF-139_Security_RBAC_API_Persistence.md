# CEEF-139 — أمن · RBAC · API · TenantGuard · Persistence

| الحقل | القيمة |
|-------|--------|
| **النطاق** | محور C+G+D — أمن Desktop/API · عزل مستأجر · SQLite/PG |
| **التاريخ** | 2026-07-10 |
| **الأدلة** | `dist/evidence/CEEF-138-supreme-audit/` |
| **الحكم** | **تدقيق مكتمل** — 4 P0 · 8 P1 · 6 P2 — إصلاحات في موجات لاحقة |

---

## 1. ملخص تنفيذي

| المنطقة | الدرجة | الحكم |
|---------|--------|-------|
| Desktop RBAC | **A** | 67/67 pageKey مغطاة · fail-closed |
| API auth | **D** | مفتاح API اختياري · لا SecurityContext |
| Tenant isolation | **D** | opt-in فقط — بدون header = allow all |
| Persistence | **B+** | Production PG إلزامي · CI PG skip · dev fallback |
| Static scan | **B** | PASS presence-only — لا enforcement |

---

## 2. Top findings (P0)

| ID | الادعاء | الدليل |
|----|---------|--------|
| AUDIT-SEC-001 | API مفتوح بدون `BRQOSH_API_KEY` | `Program.cs:84-87` |
| AUDIT-SEC-002 | Tenant provisioning بدون auth | `Program.cs:344-363` |
| AUDIT-SEC-003 | Webhook SSRF | `Program.cs:616-627` |
| AUDIT-SEC-004 | TenantGuard مفتوح بدون header | `TenantGuard.cs:12-13` |

---

## 3. API — 34 route

3 health + 31 `/api/v1/*`. جرد كامل في `grep-inventory/architecture-inventory.txt`.

**إيجابيات:**
- Company-scoped routes تستدعي `RejectTenantCompanyAsync` عند وجود tenant header
- Health probes معفاة من API key (صحيح)
- 15 `*PostingService.cs` تحتوي `EnsureCanPost*` — static scan PASS

**سلبيات:**
- لا `SecurityContext` في `src/Brqosh.Api/` (0 matches)
- `IdempotencyStore` + `WebhookSubscriptionRegistry` في الذاكرة — M9 stub

---

## 4. Desktop RBAC

```81:87:src/Brqosh.Desktop/Security/PermissionService.cs
    public static bool CanAccess(AppUser? user, string pageKey)
    {
        if (user is null) return false;
        if (user.Role == UserRole.Administrator) return true;
        if (!ScreenRoles.TryGetValue(pageKey, out var allowed))
            return false;
```

- **67** pageKeys في PermissionService = NavigationCatalog = MainWindow cases
- `ShowPlaceholder` unreachable من catalog keys (defense in depth)
- **فجوة:** لا test آلي لمزامنة الثلاثة

---

## 5. Persistence (D)

| سلوك | Production | Dev/Test |
|------|------------|----------|
| PG required | ✅ `AppServices.cs:91-100` | optional |
| PG fail | throw | silent SQLite fallback `:146-152` |
| PG CI gate | skip without `database.json` | `PostgreSqlStartupTests.cs:19-20` |
| PG local gate | **PASS** (6 tests) | `schema-gate-pg.log` — probe admin login FAIL noted |

`SecurityContext` test bypass معطل في production (`SecurityContext.cs:16-18`).

---

## 6. مصفوفة 4 مستويات

| المستوى | الحالة | الدليل |
|---------|--------|--------|
| ✅ كود | findings موثّقة | `security-findings.csv` |
| ✅ اختبار | static scan PASS · 470/470 (clean run) | `security-static-scan.log` |
| ⏭ حي | ZAP · OTP | O-05 · O-01 |
| ⏭ خارجي | PG mandatory CI | OPEN_ITEMS |

---

## 7. Backlog مقترح (هذا المحور)

| P | Item | Fix |
|---|------|-----|
| P0 | API key required in prod | startup guard |
| P0 | Tenant default-deny SaaS mode | config flag |
| P0 | Webhook URL validation | SSRF blocklist |
| P1 | API SecurityContext middleware | scoped principal |
| P1 | Extend security-static-scan | enforcement checks |
