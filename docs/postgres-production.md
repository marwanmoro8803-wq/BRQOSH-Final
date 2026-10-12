# PostgreSQL للإنتاج — BRQOSH

**المرجع:** المرحلة 3 · `BRQOSH_ENV=production`  
**التاريخ:** 2026-07-08

---

## 1. متغير البيئة الإلزامي

على جهاز التشغيل (Service / جلسة المستخدم / اختصار MSI):

```powershell
[Environment]::SetEnvironmentVariable("BRQOSH_ENV", "production", "Machine")
```

أو على مستوى المستخدم:

```powershell
[Environment]::SetEnvironmentVariable("BRQOSH_ENV", "production", "User")
```

في الإنتاج:

| السلوك | التفاصيل |
|--------|----------|
| قاعدة البيانات | **PostgreSQL فقط** عبر `%APPDATA%\BRQOSH\database.json` |
| SQLite | مرفوض — فشل تشغيل واضح |
| الترخيص | لا توجد فترة تجربة 30 يوماً |
| المخطط | **Migrate + SchemaUpdater فقط** — لا يُنشأ مخطط فارغ بـ `EnsureCreated` على قاعدة بلا جداول بدون migrations |

---

## 2. ملف الاتصال

المسار: `%APPDATA%\BRQOSH\database.json`

> **(CEEF-236 · ت-٣) الطريقُ الافتراضيّ صار من داخل التطبيق:** معالجُ التهيئة يسأل عن الخادم
> والمنفذ والقاعدة والمستخدم وكلمة المرور، **يُجرّب الاتصالَ** ويرفض الإكمالَ إن أخفق، ثمّ يكتب
> هذا الملفَّ **وسلسلةُ الاتصال محميّةٌ بـDPAPI** (تبدأ بـ`BRQOSH1:`) — لا كلمةَ مرورٍ صريحةً على
> القرص. **ويكتب `SSL Mode=Require` افتراضاً** (خانةُ «اشتراط اتصال مشفّر» في الشاشة، مؤشَّرةٌ
> ابتداءً) — فما يكتبه المنتجُ يطابق ما تُوصي به هذه الوثيقة. الكتابةُ باليد أدناه تبقى مقبولةً
> (القراءةُ تقبل الصريحَ والمحميّ) لتركيبٍ يُدار بسكربتات، لكنها لم تعد الطريقَ الوحيد ولا الموصى به.
>
> **وملفٌّ كُتب باليد بـ`SSL Mode=Require` أو `VerifyFull` يحتفظ باشتراطه** إن حُرِّر من الشاشة —
> كان يسقط بصمتٍ لأنّ الشاشة تُعيد البناء من خمسة حقولٍ لا سادسَ لها.

```json
{
  "Provider": "postgresql",
  "ConnectionString": "Host=db.example.local;Port=5432;Database=brqosh;Username=brqosh_app;Password=***;SSL Mode=Require"
}
```

**صلاحيات الملف:** قيّد ACL على مجلد `%APPDATA%\BRQOSH` لمستخدم التطبيق فقط (Properties → Security).

---

## 3. دور قاعدة البيانات (Least Privilege)

أنشئ مستخدم تطبيق بلا صلاحيات خارقة:

```sql
CREATE ROLE brqosh_app LOGIN PASSWORD '...';
CREATE DATABASE brqosh OWNER brqosh_app;
GRANT CONNECT ON DATABASE brqosh TO brqosh_app;
-- بعد إنشاء المخطط:
GRANT SELECT, INSERT, UPDATE, DELETE ON ALL TABLES IN SCHEMA public TO brqosh_app;
GRANT USAGE, SELECT ON ALL SEQUENCES IN SCHEMA public TO brqosh_app;
```

لا تستخدم مستخدم `postgres` من التطبيق.

**وفعّل TLS على الخادم قبل تشغيل المعالج** — خطوةُ ④ تشترطه افتراضاً، فبدونه يُخفق «اختبار
الاتصال» برسالةٍ تُسمّي السبب ولا يُكمل المعالج:

```
# postgresql.conf
ssl = on
ssl_cert_file = 'server.crt'
ssl_key_file  = 'server.key'
```

(‏وخادمٌ على الجهاز نفسِه يجوز فيه إزالةُ الاشتراط من الشاشة بدل تفعيل TLS.)

---

## 4. `pg_hba.conf` و TLS

- ارفض `trust` على الشبكات.
- فضّل `scram-sha-256` + مضيف محدد.
- فعّل SSL على الخادم واضبط `SSL Mode=Require` في سلسلة الاتصال.

مثال سطر (عدّل الشبكات):

```
hostssl brqosh brqosh_app 10.0.0.0/8 scram-sha-256
```

---

## 5. النسخ الاحتياطي والاستعادة

> **(جاهزيّةُ الإطلاق · ح-١٢٥) ما يملكه العميلُ فعلاً — وكان هذا القسمُ يُحيله إلى سكربتين لا يشحنهما المُنصِّب.**
>
> **المسارُ المشحون هو البرنامجُ نفسُه:** شاشةُ «النسخ الاحتياطي» (نسخٌ واستعادةٌ يدويّان) **والنسخُ المجدولُ** الذي تعرض شارتُه
> حالتَه وسببَ فشله. **وكلاهما يشترط أدواتِ عميل PostgreSQL على المحطّة** (`pg_dump` و`pg_restore`) **بإصدار الخادم أو أحدث** —
> فالأقدمُ يرفضه PostgreSQL صراحةً — والمُنصِّبُ لا يشحنها. **فثبّتها بمثبِّت PostgreSQL الرسميّ بخيار «Command Line Tools»**، ثمّ
> **تحقّق من «حالة النظام» ← «أدوات النسخ الاحتياطي»**: ✔ بالإصدارين، أو ✖ بما ينقص وما يُثبَّت.
>
> **والسكربتان أدناه في مستودع المصدر وحدَه** (`scripts/`) — لمن يُشغّل من المصدر أو يُعدّ مهمّةً مجدولةً خارج البرنامج، **ولا يُوجدان
> في تنصيبٍ من المُنصِّب**.

السكربتات (مستودعُ المصدر):

| الملف | الغرض |
|-------|--------|
| `scripts/backup-postgres.ps1` | `pg_dump -Fc` |
| `scripts/restore-postgres.ps1` | `pg_restore` |

> **(CEEF-236) من أين تأتي `-ConnectionString`؟** من بيانات الاتصال التي أُدخلت في خطوة ④
> نفسِها. **ولا تُقرأ من `database.json`** — صارت السلسلةُ فيه محميّةً بـDPAPI ولا تُفكّ إلا
> بالتطبيق على حساب المستخدم نفسِه. احفظها في مخزن أسرار التشغيل عند إعداد المهمّة المجدولة.

**هدف RTO:** استعادة كاملة لأحدث نسخة خلال **&lt; 4 ساعات**.  
**تمرين شهري:** نفّذ restore على قاعدة اختبار ووثّق التاريخ في سجل التشغيل.

```powershell
.\scripts\backup-postgres.ps1 -ConnectionString "Host=...;Database=brqosh;Username=...;Password=..."
.\scripts\restore-postgres.ps1 -DumpFile ".\backups\brqosh_....dump" -ConnectionString "..."
```

---

## 6. تقسيم الجداول الكبيرة (اختياري)

راجع `docs/postgres-partitioning.sql` عند تجاوز ~5M سجل في القيود/الفواتير.

---

## 7. قائمة تحقق تشغيل إنتاج

- [ ] `BRQOSH_ENV=production`
- [ ] `database.json` → postgresql + سلسلة صحيحة
- [ ] اتصال TLS / `pg_hba` مقيّد
- [ ] نسخة احتياطية يومية مجدولة
- [ ] تمرين restore شهري موثَّق
- [ ] ترخيص مفعّل (لا تجربة)
- [ ] بعد الترقية: دخولٌ وفتحُ «ميزان المراجعة» — **ولا `FullScenarioAudit` على قاعدة الإنتاج** (تكتب فيها؛ مكانُها جذرٌ معزولٌ عند المطوّر · ح-٧٦)

---

*مرتبط: `installer/README.md` · `docs/ZATCA_LIVE_CSID.md`*
