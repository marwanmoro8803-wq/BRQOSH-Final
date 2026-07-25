# حوكمة المخطط — BRQOSH Schema Governance

**الإصدار:** 1.0 · **التاريخ:** 2026-07-09  
**النطاق:** SQLite (تطوير/اختبار) + PostgreSQL (إنتاج)  
**القانون الدائم للوكيل:** `.cursor/rules/brqosh-schema-governance.mdc`

---

## 1. لماذا هذا القانون؟

مشروع BRQOSH يستخدم مساراً هجيناً للمخطط:

1. **EF Core** ينشئ المخطط على قاعدة فارغة (`EnsureCreated`)
2. **BrqoshSchemaUpdater** يضيف أعمدة وجداول ناقصة للقواعد القديمة
3. **SchemaSqlGuard** يتحقق من أنواع DDL قبل التنفيذ

هذا التصميم قوي للترقية التدريجية، لكنه يخلق **فجوة drift**: ما يُختبر على SQLite in-memory لا يكتشف أنواع PG غير المسجّلة حتى يُشغَّل Desktop على PostgreSQL.

### حالات حقيقية (V12)

| الحالة | SQLite | PostgreSQL |
|--------|--------|------------|
| `audit_log.Reason` | ✅ `TEXT NULL` | ❌ `character varying(512)` قد يُرفض إن لم يُسجَّل |
| `customers.ModifiedBy` | ✅ | ❌ `character varying(128)` — نفس النمط |
| Agent: «W1-B مكتمل» | اختبارات PASS | Desktop startup FAIL |

---

## 2. المبدأ الذهبي

> **كل تغيير schema يجب أن يمر بـ SQLite + PostgreSQL قبل الإغلاق.**

لا استثناء لمسارات Persistence. «يعمل عندي» على SQLite ليس دليل إغلاق.

---

## 3. المكوّنات والمسؤوليات

```
┌─────────────────┐     ┌──────────────────────┐     ┌─────────────────┐
│ BrqoshDbContext │────▶│ EnsureCreated (EF)   │────▶│ مخطط PG/SQLite  │
│ + Entities      │     │ على قاعدة فارغة      │     │ على DB جديدة    │
└─────────────────┘     └──────────────────────┘     └─────────────────┘
         │                                                        ▲
         │                                                        │
         ▼                                                        │
┌─────────────────┐     ┌──────────────────────┐     ┌──────────┴──────┐
│ SchemaUpdater   │────▶│ SchemaSqlGuard       │────▶│ ALTER / CREATE  │
│ ترقية تدريجية   │     │ سجل الأنواع          │     │ للقواعد القديمة │
└─────────────────┘     └──────────────────────┘     └─────────────────┘
         │
         ▼
┌─────────────────┐
│ ReadExisting    │  information_schema (PG) · pragma_table_info (SQLite)
│ Columns         │
└─────────────────┘
```

### EF = مصدر الحقيقة للنماذج

- تعريفات C# في `Brqosh.Domain`
- تكوين Fluent API في `BrqoshDbContext`
- ما يُنتجه Npgsql على PG يجب أن يفهمه SchemaUpdater

### SchemaUpdater = مصدر الحقيقة للترقية

- لا يستبدل EF — يكمّله للقواعد الموجودة
- كل `EnsurePgColumnAsync` له نظير `EnsureSqliteColumnAsync`
- يقرأ الأعمدة الموجودة قبل الإضافة (`ReadExistingColumns`)

### SchemaSqlGuard = بوابة الرفض

- `AllowedPgTypes` / `AllowedSqliteTypes` — قائمة بيضاء
- أي نوع خارج القائمة → `Unsupported PostgreSQL column type: ...`
- **الحل ليس تعطيل الحارس** — توسيع السجل

---

## 4. سجل أنواع PostgreSQL

### مسجّل في `SchemaSqlGuard.AllowedPgTypes` (2026-07-09)

```
BIGINT NULL
UUID NULL
UUID NOT NULL DEFAULT gen_random_uuid()
TIMESTAMPTZ NULL
INTEGER NOT NULL DEFAULT 0
BOOLEAN NOT NULL DEFAULT false
NUMERIC(18,2) NOT NULL DEFAULT 0
character varying(3) NOT NULL DEFAULT 'SAR'
character varying(16) NULL
character varying(16) NOT NULL DEFAULT 'Active'
character varying(32) NOT NULL DEFAULT 'WeightedAverage'
character varying(64) NULL
character varying(64) NOT NULL DEFAULT ''
character varying(128) NULL
character varying(512) NULL
character varying(512) NOT NULL DEFAULT ''
```

### في CREATE TABLE خام (DDL) — أمثلة شائعة

`character varying(256)`, `DATE`, `BIGSERIAL`, `JSONB` — تظهر في `SafePgIndexAsync` لإنشاء جداول.  
**قاعدة:** أي نوع جديد في DDL خام يجب توثيقه وإضافته للسجل عند إعادة استخدامه في `EnsurePgColumnAsync`.

### خريطة SQLite ↔ PostgreSQL

| PostgreSQL | SQLite |
|------------|--------|
| `character varying(n)` | `TEXT` |
| `BIGINT` | `INTEGER` |
| `BOOLEAN` | `INTEGER` (0/1) |
| `NUMERIC(18,2)` | `REAL` |
| `TIMESTAMPTZ` | `TEXT` (ISO datetime) |
| `UUID` | `TEXT` |
| `DATE` | `TEXT` |

---

## 5. تعريف الإغلاق (4 مستويات)

| المستوى | المعنى | الأمر / الدليل |
|---------|--------|----------------|
| 1 · كود | Entity + DbContext + SchemaUpdater (مساران) + Guard | مراجعة PR |
| 2 · SQLite | اختبارات Application PASS | `dotnet test Brqosh.sln` |
| 3 · PostgreSQL | SchemaUpdater + Bootstrap على PG | `PostgreSqlStartupTests` |
| 4 · Desktop | تشغيل فعلي أو `DbStartupProbe` | لا استثناء `Unsupported...` |

---

## 6. بوابة PR — Checklist

عند أي تغيير يمس المخطط:

- [ ] **schema-gate-sqlite** — `dotnet test` كامل PASS
- [ ] **schema-gate-pg** — `PostgreSqlStartupTests` مع `database.json`
- [ ] **schema-type-registry** — نوع جديد في `AllowedPgTypes` + `SchemaSqlGuardTests`
- [ ] مسار SQLite و PG متزامنان في SchemaUpdater
- [ ] لا إغلاق في OPEN_ITEMS أو الخطة دون ذكر حالة PG

### إعداد PostgreSQL محلي للاختبار

1. تشغيل PostgreSQL محلياً
2. إنشاء `%AppData%\BRQOSH\database.json`:

```json
{
  "Provider": "postgresql",
  "ConnectionString": "Host=localhost;Database=brqosh_test;Username=postgres;Password=..."
}
```

3. `dotnet test --filter "FullyQualifiedName~PostgreSqlStartup"`

---

## 7. ماذا نقول للمستخدم عند الفشل؟

**لا نطلب «جرب SQLite».**

الرسالة الصحيحة:

> فشل تشغيل PostgreSQL: نوع العمود غير مسجّل في سجل المخطط.  
> هذا ليس خطأ في قاعدة البيانات — إنه نقص في تسجيل النوع في الكود.  
> الحل: توسيع `AllowedPgTypes` + اختبار PG + إعادة التشغيل.

---

## 8. منع تكرار خطأ الوكيل

| الخطأ | البديل الصحيح |
|-------|---------------|
| «الاختبارات PASS — تم» | «SQLite PASS · PostgreSQL: لم يُختبر / FAIL» |
| إضافة عمود دون Guard | إضافة للسجل أولاً |
| إصلاح PG بتحويل المستخدم لـ SQLite | إصلاح التوافق في الكود |
| تخطي Desktop smoke | `DbStartupProbe` على PG |

---

## 9. مراجع

- `.cursor/rules/brqosh-schema-governance.mdc`
- `docs/OPEN_ITEMS.md` — قسم قانون Schema
- `.cursor/plans/مهم_v12_إغلاق_شامل_8c9973cd.plan.md` — W0 governance
- `src/Brqosh.Infrastructure/Persistence/BrqoshSchemaUpdater.cs`
- `src/Brqosh.Infrastructure/Persistence/SchemaSqlGuard.cs`
- `tests/Brqosh.Application.Tests/Persistence/PostgreSqlStartupTests.cs`
- `tests/Brqosh.Application.Tests/Persistence/SchemaSqlGuardTests.cs`
