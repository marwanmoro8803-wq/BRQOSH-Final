# BRQOSH — دليل التركيب والتشغيل (6 أقسام)

**المرجع:** CEEF-010 · المرحلة 3

---

## 1. المتطلبات

| المكوّن | الحد الأدنى |
|---------|-------------|
| نظام التشغيل | Windows 10/11 x64 |
| بيئة التشغيل | [.NET 9 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/9.0) |
| قاعدة البيانات (إنتاج) | **PostgreSQL 15+** (يُفضّل 16+) |
| قاعدة البيانات (تطوير) | SQLite تلقائي أو PostgreSQL |
| قرص | ≥ 2 GB حر للنشرات والنسخ |

متغير الإنتاج:

```powershell
[Environment]::SetEnvironmentVariable("BRQOSH_ENV", "production", "Machine")
```

راجع التفاصيل في [`docs/postgres-production.md`](../docs/postgres-production.md).

---

## 2. تثبيت MSI

```powershell
dotnet tool install --global wix   # مرة واحدة لبناة الحزمة
.\installer\publish.ps1
.\installer\build-msi.ps1
```

ناتج: `dist\BRQOSH-Setup.msi` + اختصارات قائمة ابدأ وسطح المكتب.

**على جهاز نظيف (Pilot):**

1. ثبّت .NET 9 Desktop Runtime إن لزم  
2. ثبّت `BRQOSH-Setup.msi`  
3. عيّن `BRQOSH_ENV=production`  
4. أنشئ `database.json` (القسم 3)  
5. شغّل BRQOSH → أكمل المعالج إن طُلب → سجّل الدخول  
6. نفّذ `tools/FullScenarioAudit` وتوثيق SUCCESS  

نشر محمول بدون MSI:

```powershell
.\installer\publish.ps1
# الناتج: dist\BRQOSH
```

---

## 3. التشغيل الأول (Setup Wizard)

عند أول تشغيل بدون إعداد مكتمل:

1. اسم المنشأة + الرقم الضريبي (15 خانة)  
2. اختيار نظام العمل (محاسبة / مقاولات / تصنيع / تجزئة)  
3. إنشاء مستخدم المدير وكلمة المرور (سياسة 12+ حرفاً)  
4. تهيئة الدليل المحاسبي السعودي والفترات  

بعد الإكمال تُحفظ الحالة في مخزن الإعداد المحلي ولا يُعاد المعالج.

---

## 4. تفعيل الترخيص

1. افتح **إعدادات المنشأة**  
2. انسخ **معرّف الجهاز**  
3. فعّل عبر نافذة الترخيص بالمفتاح الصادر  
4. في الإنتاج لا تُمنح فترة تجربة — التفعيل إلزامي  

---

## 5. إعداد ZATCA (CSID)

راجع الدليل التفصيلي: [`docs/ZATCA_LIVE_CSID.md`](../docs/ZATCA_LIVE_CSID.md)

ملخص:

1. شاشة إعدادات زاتكا داخل البرنامج  
2. معالج CSID (OTP من بوابة فاتورة)  
3. بيئة Simulation ثم Production بعد الاعتماد  
4. التحقق باختبار `ZatcaLiveCsidTests` عند توفر OTP  

---

## 6. النسخ الاحتياطي والاستعادة

| السكربت | الاستخدام |
|---------|-----------|
| `scripts/backup-postgres.ps1` | `pg_dump -Fc` يومي |
| `scripts/restore-postgres.ps1` | استعادة إلى بيئة اختبار/تعافٍ |

**هدف RTO:** &lt; 4 ساعات لاستعادة أحدث نسخة.  
**تمرين:** استعادة شهرية موثَّقة على قاعدة اختبار.

تقسيم الجداول الكبيرة (اختياري): `docs/postgres-partitioning.sql`.
