# CEEF-121 — Skills v16 (توحيد الأدوات + طبقات التحميل)

| الحقل | القيمة |
|-------|--------|
| **النطاق** | S1–S3 من CEEF-120 |
| **التاريخ** | 2026-07-10 |
| **الحكم** | **✅ مغلق (كود + اختبار بنية)** |

## التغييرات

| ملف | التغيير |
|-----|---------|
| `supreme-programming-mastery/tools-cursor.md` | **canonical** — المصدر الوحيد لقواعد الأدوات |
| `brqosh-engineering/tools.md` | أُعيدت كتابته: BRQOSH-only (PowerShell · verify toolchain · domain map) + redirect للـ canonical |
| `supreme-programming-mastery/FILE-ROADMAP.yaml` | v16 + `load_tiers` (session ≤4 ملفات · wave · deep) — يمنع تحميل 31 ملفاً |
| `brqosh-closure/zero-gap-matrix.md` | روابط G1–G3 مصححة للمجلد الكامل |
| `brqosh-skills-hub/STATUS.yaml` | version → **v16** |
| `brqosh-skills-hub/leak-synthesis.md` | سجل موجة v16 |
| `scripts/ops/agent-skill-regression.ps1` | regex → `v1[3-6][a-z]?` |

## الدليل

```
agent-skill-regression.ps1 → PASS (v16)
```

| المستوى | الحالة | الدليل |
|---------|--------|--------|
| ✅ كود | تم | الملفات أعلاه |
| ✅ اختبار | PASS | regression run 2026-07-10 |
| ✅ حي | — | لا ينطبق (وثائق وكلاء) |
| ⏭ خارجي | — | — |

**درجة Skills:** A− → **A** (لا تكرار canonical · حمل جلسة محدود)
