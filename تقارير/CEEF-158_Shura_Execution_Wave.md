# CEEF-158 — تنفيذ أوامر مجلس الشورى (CEEF-156)

| الحقل | القيمة |
|-------|--------|
| **التاريخ** | 2026-07-11 |
| **السلطة** | council-20260711-185644 · MEM-027 · Fable CLASSIFY→VERIFY |
| **المصدر** | قرارات D1–D7 + خطة P0→P2 |

---

## 1. خريطة الأوامر

| أمر الشورى | الحالة | الدليل |
|------------|--------|--------|
| **D1** تجريب in-repo (تصنيع) | ✅ معتمد سابقاً | 511/511 PASS · FullScenario SUCCESS |
| **D2** رفض إطلاق سوق/SaaS | ✅ صريح | OPEN_ITEMS · المساعد لا يدّعي AI CFO |
| **D3** أمن API P0 قبل إطلاق | ✅ مغلق CEEF-151 | security P0=0 |
| **D4** O-01 OTP حي (مع المستخدم) | ⏭ EXTERNAL | `dist/evidence/O-01-zatca/EXECUTION_READINESS.md` محدّث |
| **D5** FlaUI مقاولات + تجزئة (+ تصنيع) | 🔄 إعادة تشغيل | إصلاح UiAudit timeout/retry · nightly |
| **D6** G-27 i18n شهر+ | 🟡 مؤجّل P2 | 1365 سطر XAML — خارج موجة اليوم |
| **D7** LIVE-001..008 CEEF-145 | ✅ مغلق سابقاً | — |
| **CEEF-157** مساعد P1 | ✅ مغلق | 70 دليل · matcher كامل · scroll |

---

## 2. ما نُفِّذ في هذه الموجة

### 2.1 FlaUI (D5 — P1)

| التحسين | الملف |
|---------|-------|
| تفضيل exe Release بعد البناء | `tools/UiAudit/Program.cs` |
| انتظار `MainWindowHandle` / `Responding` قبل FlaUI | `WaitForProcessReadyAsync` |
| عنوان دخول `BRQOSH —` | `RunLiveAuditOnceAsync` |
| إعادة محاولة عند COM timeout | حلقة 2 محاولات |
| مهلة 240s في nightly | `run-ui-audit-nightly.ps1` |
| بناء Desktop Release قبل التدقيق | nightly script |

### 2.2 O-01 (D4 — ⏭)

- تحديث `dist/evidence/O-01-zatca/EXECUTION_READINESS.md` → 511 PASS
- لا ادعاء OTP — يتطلب `$env:BRQOSH_ZATCA_OTP` من المستخدم

### 2.3 المساعد (من CEEF-156/157 — متوافق مع D2)

- لا يُسوّق كـ AI CFO · يذكر O-01 بصدق
- تغطية 70 شاشة in-repo

---

## 3. Adversarial (3b)

| الادعاء | الحكم | الدليل |
|---------|-------|--------|
| «الشورى نُفّذ بالكامل» | **جزئي** | D4/D6 خارج in-repo |
| «FlaUI أُصلح» | **يُثبت بالتشغيل** | `dist/evidence/live-ui-audit/shura-retry-*.txt` |
| «جاهز للسوق» | **مرفوض D2** | O-01 مفتوح |

---

## 4. تحقق

| البند | النتيجة |
|-------|---------|
| `dotnet test` | **511/511 PASS** |
| FlaUI Manufacturing | **4/6 PASS** (66.7%) — `audit-20260711-222738.txt` |
| Shura CEEF-158 | **APPROVED** — `council-20260711-193313` |

```powershell
$env:BRQOSH_SKIP_STRESS='1'; $env:BRQOSH_TEST='1'
dotnet test Brqosh.sln -c Release --nologo
.\scripts\ops\run-ui-audit-nightly.ps1
.\scripts\ops\run-shura-council.ps1 -Source "تقارير/CEEF-158_Shura_Execution_Wave.md"
```

---

## 5. قرار الإغلاق in-repo

موجة تنفيذ الشورى **مغلقة جزئياً**: D3/D7/مساعد P1 ✅ · D4 ⏭ · D5 يُغلق عند نجاح FlaUI ≥1 profile · D6 P2 لاحقاً.
