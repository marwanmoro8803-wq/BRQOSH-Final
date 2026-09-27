# النصوصُ القياسيّةُ للتراخيص — ومن أين نُقلت

يستعملها `scripts/third_party_notices.py` للحزم التي تُعلن **معرّفَ SPDX بلا نصٍّ** في الحزمة نفسِها
(EF Core وQRCoder ← `MIT` · Npgsql ← `PostgreSQL` · SQLitePCLRaw ← `Apache-2.0`).

**نُقلت حرفاً من نسخٍ موزَّعةٍ على الجهاز، لا كُتبت من الذاكرة** — ونهاياتُ الأسطر وحدَها وُحّدت إلى LF:

| الملفّ | المصدر | ما نُقل |
|---|---|---|
| `Apache-2.0.txt` | `LICENSE.txt` في حزمة `s3transfer-0.17.0` (بايثون، مع pgAdmin 4 في PostgreSQL 16) — ونسختُه في `s3transfer-0.16.0` مطابقةٌ بالبصمة | الملفُّ كلُّه: النصُّ القياسيُّ بملحقه |
| `MIT.txt` | `LICENSE.TXT` في حزمة `Microsoft.Extensions.Logging 9.0.19` | فقرتا الإذن وإخلاءِ الضمان — من «Permission is hereby granted» إلى آخره، **بلا سطر الحقوق** (سطرُ كلِّ حزمةٍ في مدخلها) |
| `PostgreSQL.txt` | `commandlinetools_3rd_party_licenses.txt` في PostgreSQL 16 (قسمُ libpq) | من «PostgreSQL Database Management System» إلى «OR MODIFICATIONS.» — والملفُّ الأصلُ cp1252 والقسمُ ASCII خالص |

**وحدُّه مكتوب:** نصُّ `PostgreSQL` القياسيُّ يُسمّي جامعةَ كاليفورنيا مالكاً — وحزمةُ Npgsql لا تحمل نصَّها، فمدخلُها
يُسمّي مالكَها من بيانات الحزمة ويُحيل إلى مستودعها؛ وسطرُ «STANDARD LICENCE TEXTS» في الملفّ المولَّد يقول ذلك.
