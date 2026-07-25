# سجل العمل

## 2026-07-25 — تحليل شامل متعدد الوكلاء (CEEF-200)
سرب 8 وكلاء + تحقق يدوي مباشر. النتيجة: `تقارير/CEEF-200_تحليل_شامل_متعدد_الوكلاء_2026-07-25.md`.
- بناء ✅ (0/0)، اختبارات 509/510 (الفاشل: `DesktopDiGuardTests` بسبب `new DatabaseBootstrapService` في `App.xaml.cs:78` — جذر تركيب).
- **3 ثغرات حرجة محقَّقة بعينيّ:**
  1. OTP استعادة كلمة المرور يُعرض على الشاشة (`OtpHintFormatter`/`InAppOtpNotificationChannel` بلا بوابة إنتاج) → استيلاء على أي حساب.
  2. هجرات EF بأنواع SQLite (`INTEGER`/`TEXT`/`Sqlite:Autoincrement`) → مخطط معطوب على PostgreSQL جديد.
  3. `DbContextFactory.Create` بلا `AddInterceptors` → حارس ثبات التدقيق لا يعمل على مسار Desktop.
- **خطر تشغيلي:** دفعة ضخمة غير مرصودة (269 مرحّل + 1987 untracked مترابطة) + ملوثات (`dist/` 391MB, `system_prompts_leaks-main/`) غير مُتجاهلة.
- عالية: عزل مستأجري API (4)، امتثال ZATCA (توقيع/UBL)، ميزانية/دخل عبر حدود السنة، غياب رموز التزامن، Outbox.
- **لم يُشغَّل:** الذكي (DeepSeek/Gemini) والمدقق (TestSprite) — متاحان لطبقة تحقق إضافية.

## 2026-07-25 — المرحلة ١: إصلاح الثغرات الحرجة (منجز ومُثبت)
بناء ✅ 0/0، **اختبارات 511/511 ✅** (كانت 509/510). أُصلح:
- **ح-3:** `DbContextFactory.Create` صار يضيف `AuditLogImmutabilityInterceptor` → حارس التدقيق يسري على كل السياقات بما فيها Desktop.
- **ح-1:** `InAppOtpNotificationChannel.SendOtp` يُخفي الرمز (`DevHint=null`) في الإنتاج → إغلاق استيلاء الحساب. متابعة: يلزم قناة OTP خارجية أو حصر إعادة التعيين بالمدير للإنتاج.
- **الاختبار الفاشل:** `App.xaml.cs` صار يوجّه التمهيد عبر `DesktopAccountingFacade` + `GetService<DatabaseBootstrapService>()` بدل `new` → حارس DI أخضر (Desktop 123/123).
- **ح-2 (هجرات Npgsql) لم تُنفَّذ بعد** — مرحلة مخصصة عالية الخطورة (تحتاج PG حقيقي).

## 2026-07-25 — المرحلة ٢: تأمين git (منجز)
- commit `82b1260`: حفظ ١٥٢٧ ملفاً (~١٩ يوم عمل) + إصلاحات المرحلة ١. الشجرة نظيفة.
- `.gitignore`: استُبعد `dist/` (٣٩١MB)، `system_prompts_leaks-main/`، برومبتات الجذر، `*.pyc`.
- `.gitattributes` جديد: `eol=lf` عام، `crlf` للـps1/bat.
- **درس:** `git check-ignore dist/` (بشرطة) ضلّل بأنها مُتجاهَلة؛ الاختبار الحاسم ملف ملموس `git check-ignore dist/x.exe`.

## 2026-07-25 — المرحلة ٤ (جزئي): أمن API
- commit `854488e`: ع-A1 — حارس ٤٠٣ على `/companies` و`/company-groups` عند `EnforceTenantIsolation` بلا مستأجر. +٢ اختبار (مصنع SaaS). **513/513 ✅**.
- **مؤجّل بوعي (تشابك/عقد/بيئة):**
  - ع-R1+ع-R2 (ميزانية/دخل عبر السنة): **مترابطان** — إصلاح الدخل وحده يزدوج حساب صافي الدخل في الميزانية (صف 3999 + رصيد 3104). يحتاج تصميماً متماسكاً + اختبارات كل حالات حدود السنة.
  - ع-A3 (الدور الافتراضي Admin→Viewer): عقد له اختبار `ApiSecurityContextMiddlewareTests:17` + تكاملات خارجية.
  - ح-2 هجرات PG، ZATCA (توقيع/UBL)، التحقق الحيّ للواجهة: تحتاج بيئة (PG/ZATCA sandbox/تشغيل WPF).
