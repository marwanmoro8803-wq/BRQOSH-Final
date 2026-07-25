# WhatsApp OTP — إرسال حي عبر Meta Cloud API

## البدء السريع (بدون خبرة تقنية)

**انقر مرتين على الملف في جذر المشروع:**

```
فعّل-واتساب.bat
```

المعالج يفتح Meta ويوجّهك خطوة بخطوة، ويحفظ الإعداد، ويرسل رسالة تجريبية لرقمك.

> **مهم:** رقمك الشخصي (مثل `0582778405`) = **المستلم** (أين يصل الرمز).  
> **المرسل** = رقم تجاري من Meta (مجاني في وضع التطوير) — ليس رقمك الشخصي.

---

## لماذا تظهر «بيئة محلية» والرمز على الشاشة؟

بدون إعداد Meta WhatsApp Business API، BRQOSH يستخدم `LocalWhatsAppNotificationChannel`:

1. **لا يُرسل** أي رسالة واتساب حقيقية.
2. يكتب الرسالة في مجلد `%APPDATA%\BRQOSH\whatsapp-outbox\`.
3. يعرض الرمز على الشاشة للتطوير (`DevHint`).

هذا سلوك مقصود — ليس عطل شبكة. التكامل الحي كان مؤجلاً كبوابة خارجية.

## المتطلبات (مرة واحدة)

| البند | المصدر |
|-------|--------|
| حساب Meta Business | [business.facebook.com](https://business.facebook.com) |
| تطبيق WhatsApp Cloud API | Meta Developers |
| رقم واتساب Business مُفعّل | Phone Number ID |
| Access Token دائم | System User أو App Token |
| قالب مصادقة OTP معتمد | اسم افتراضي: `brqosh_otp` |

### إنشاء قالب المصادقة

في **WhatsApp Manager → Message templates**:

- **الفئة:** Authentication
- **الاسم:** `brqosh_otp` (أو غيّر `TemplateName` في الإعداد)
- **اللغة:** Arabic (`ar`)
- **المحتوى (مثال):**
  ```
  رمز التحقق في BRQOSH هو {{1}}. صالح 10 دقائق. لا تشاركه.
  ```
- **زر:** Copy code (للقوالب من فئة Authentication)

انتظر موافقة Meta (عادة دقائق إلى 24 ساعة).

## التفعيل — طريقتان

### 1) ملف إعداد (موصى به للـ Desktop)

أنشئ `%APPDATA%\BRQOSH\whatsapp.json`:

```json
{
  "AccessToken": "EAAxxxxxxxx",
  "PhoneNumberId": "123456789012345",
  "TemplateName": "brqosh_otp",
  "TemplateLanguage": "ar",
  "UseAuthenticationTemplate": true
}
```

### 2) متغيرات البيئة

```powershell
$env:BRQOSH_WHATSAPP_ACCESS_TOKEN = "EAAxxxxxxxx"
$env:BRQOSH_WHATSAPP_PHONE_NUMBER_ID = "123456789012345"
$env:BRQOSH_WHATSAPP_TEMPLATE_NAME = "brqosh_otp"
$env:BRQOSH_WHATSAPP_TEMPLATE_LANG = "ar"
```

اختياري:

| المتغير | الافتراضي |
|---------|-----------|
| `BRQOSH_WHATSAPP_API_VERSION` | `v21.0` |
| `BRQOSH_WHATSAPP_AUTH_TEMPLATE` | `1` (زر نسخ) — `0` لتعطيل الزر |

## التحقق

```powershell
cd C:\Users\qc\Projects\BRQOSH
dotnet build src/Brqosh.Desktop/Brqosh.Desktop.csproj -c Release
.\dist\BRQOSH\Brqosh.Desktop.exe
```

- تذييل الشاشة: **«واتساب حي — Meta Cloud API»** (وليس «بيئة محلية»).
- عند الإرسال: **لا يظهر الرمز على الشاشة**.
- يصل الرمز على واتساب الرقم المُدخل (مثال: `0582778405` → `966582778405`).

## استكشاف الأخطاء

| الخطأ | السبب المحتمل |
|-------|----------------|
| `بيئة محلية` ما زالت تظهر | `whatsapp.json` غير موجود أو `AccessToken`/`PhoneNumberId` فارغان |
| `template name does not exist` | القالب غير معتمد أو الاسم/اللغة خاطئ |
| `Recipient phone number not in allowed list` | في وضع التطوير — أضف الرقم في Meta → API Setup → test numbers |
| `فشل إرسال واتساب (401)` | Token منتهي أو غير صالح |
| الرمز على الشاشة فقط | القناة المحلية ما زالت نشطة — راجع الإعداد |

## صندوق الصادر المحلي (تطوير)

عند عدم الإعداد، الرسائل تُحفظ في:

```
%APPDATA%\BRQOSH\whatsapp-outbox\
```

## أمان

- **لا تُرفع** `whatsapp.json` أو الـ Token إلى Git.
- استخدم System User Token بصلاحيات `whatsapp_business_messaging` فقط.
- دوّر الـ Token عند التسريب.
