# قائمة تحقق أمنية — BRQOSH API / Desktop

**المرجع:** ROADMAP M12 · OWASP ASVS خفيف  
**التاريخ:** 2026-07-08  
**النطاق:** ما يمكن إثباته داخل المستودع؛ اختبار اختراق طرف ثالث سنوي

---

## 1. سطح الهجوم الحالي

| السطح | الحماية | الحالة |
|-------|---------|--------|
| Desktop login | PBKDF2 · lockout · MustChangePassword | ✅ |
| RBAC شاشات/أزرار | `PermissionService` + `SecurityContext` fail-closed | ✅ |
| ترحيل بدون مستخدم | مرفوض إلا `BRQOSH_TEST=1` | ✅ |
| EnsureCreated في الإنتاج | مرفوض (`ProductionSchemaGuard`) | ✅ |
| API `/health` | عام (بدون مفتاح) | ✅ مقصود |
| API `/api/v1/*` | `X-Api-Key` عند تعيين `BRQOSH_API_KEY` | ✅ |
| SQL خام في SchemaUpdater | `SchemaSqlGuard` whitelist | ✅ |
| أسرار ZATCA | مخزّنة محلياً — لا تُرفع للمستودع | ⚠ تشغيلي |

## 2. OWASP Top 10 — خريطة سريعة

| # | بند | تقييم داخلي |
|---|-----|-------------|
| A01 Broken Access Control | Desktop RBAC؛ API بمفتاح مشترك (ليس OAuth بعد) | 🟡 |
| A02 Cryptographic Failures | PBKDF2 كلمات المرور؛ ترخيص RSA | 🟢 |
| A03 Injection | EF parameterized؛ SchemaSqlGuard للـ DDL | 🟢 |
| A04 Insecure Design | fail-closed على الترحيل | 🟢 |
| A05 Security Misconfiguration | API بدون مفتاح = مفتوح للقراءة على الشبكة المحلية | 🟡 وثّق في README API |
| A07 Auth Failures | lockout موجود | 🟢 |
| A09 Logging | AuditLog للعمليات الحرجة | 🟢 |
| A10 SSRF | لا استدعاءات مستخدم مخصّصة لـ URL إلا ZATCA ثابتة | 🟢 |

## 3. بوابة pen test سنوي (خارجي)

1. تشغيل `Brqosh.Api` مع `BRQOSH_API_KEY` على شبكة معزولة.
2. مسح OWASP ZAP baseline على `/api/v1`.
3. محاولة وصول Viewer لترحيل من UI.
4. توثيق النتائج في ملحق CEEF أمني.

## 4. قرار الإغلاق داخل المستودع

بنود التطبيق والتحصين القابلة للكود **مُغلقة**. اختبار الاختراق الحي يُسجَّل عند التنفيذ التشغيلي — ليس «مكمل جزئياً» في الشيفرة.
