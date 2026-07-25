# أعراف المشروع

- **العربية RTL أولاً.** رسائل الأخطاء والاستثناءات المحاسبية بالعربية (`AccountingException("...")`).
- **القيد المرحّل لا يُعدَّل أبداً** — التصحيح بقيد عكسي (`CreateReversal`). لا حذف فعلي — soft delete + audit.
- **كل ترحيل مالي داخل `UnitOfWork.ExecuteInTransactionAsync`.** الإرسال الخارجي (ZATCA HTTP) **بعد** الـ commit فقط.
- **لا `new XxxService(db)` في Desktop** — حارس `DesktopDiGuardTests` يفرض الحل من DI عبر `DesktopAccountingFacade`.
- **دقة decimal:** `HasPrecision(18,2)` نقد، `(18,4)` كميات/تكاليف، `(18,6)` أسعار صرف. تقريب `MidpointRounding.AwayFromZero`.
- **الأسرار:** متغيرات بيئة أو DPAPI (`LocalSecretProtector`)، لا نص صريح في القاعدة/الكود. الإنتاج يرفض غير PostgreSQL.
- **تعريف «مغلق» رباعي المستويات** في `docs/OPEN_ITEMS.md` — لا يُكتب «جاهز» إلا باستيفاء المستوى.
- **تقارير CEEF مرقّمة** في `تقارير/` تشكل أثر التدقيق. أضف تقريراً لكل حملة كبيرة.
- **⚠️ لا تشغّل `git add -A`** قبل تجاهل `dist/` و`system_prompts_leaks-main/` وملفات البرومبتات في الجذر.
- **⚠️ لا dotnet أثناء جلسة Codex نشطة** (تعارض build-server).
