# O-04 — اختبار حمل القيود (Stress Load)

## المستوى 1 — موثّق في CI (2k–5k)

```powershell
$env:BRQOSH_SKIP_STRESS = "1"   # الافتراضي في CI
dotnet test Brqosh.sln
```

لتشغيل stress فعلي:

```powershell
.\scripts\ops\run-stress-benchmark.ps1
```

## المستوى 2 — هدف 1M قيد

### متطلبات الأجهزة (مقترح)

| المورد | الحد الأدنى |
|--------|-------------|
| CPU | 8 cores |
| RAM | 16 GB |
| PostgreSQL | SSD · shared_buffers 2GB+ |
| الزمن | ساعات |

### التشغيل

```powershell
.\scripts\ops\run-stress-benchmark.ps1 -FullMillion -Postgres
```

`-Postgres` يُشغّل على خادم PostgreSQL للاختبار (`brqosh_t_*`) ويشترطه (`BRQOSH_REQUIRE_PG=1`)؛ وبدونه
SQLite بالذاكرة — **ليس قياساً لـO-04**. (ح-٥٩: قبلها كان `-FullMillion` لا يغيّر شيئاً — ألفا قيدٍ على SQLite.)

### ما يُؤرشف

- `stress-log.txt` — stdout كامل
- `pg-stat.txt` — `pg_stat_activity` لقطة
- زمن إجمالي و throughput (قيد/ثانية)

## الحكم

- 2k–5k: ✅ داخل المستودع (`JournalStressLoadTests`)
- 1M: ⏭ خارجي حتى تشغيل ميداني موثّق
