# O-01 — ZATCA Production OTP (خطوة بخطوة)

## المتطلبات المسبقة

- [ ] شركة مسجّلة VAT 15 رقماً في النظام
- [ ] اتصال إنترنت مستقر
- [ ] حساب بوابة فاتورة (هيئة الزكاة)
- [ ] بيئة إنتاج (ليس sandbox فقط)

## الخطوة 1 — التحقق الداخلي

```powershell
.\scripts\ops\run-external-gates.ps1
dotnet run --project tools\FullScenarioAudit\FullScenarioAudit.csproj
```

## الخطوة 2 — الحصول على OTP

1. ادخل [بوابة فاتورة](https://fatoora.zatca.gov.sa)
2. اختر «ربط نظام جديد» أو تجديد CSID
3. انسخ OTP (صلاحية دقائق — لا تؤجل)

## الخطوة 3 — معالج CSID (Desktop)

1. شغّل BRQOSH Desktop
2. **الفوترة الإلكترونية → إعدادات هيئة الزكاة**
3. **معالج CSID:**
   - توليد مفتاح و CSR
   - إدخال OTP
   - طلب شهادة الامتثال (Compliance)
   - طلب شهادة الإنتاج (Production)

## الخطوة 4 — اختبار حي (CLI اختياري)

```powershell
$env:BRQOSH_ZATCA_OTP = "<OTP>"
$env:BRQOSH_ZATCA_VAT = "<VAT15>"
$env:BRQOSH_ZATCA_ORG = "اسم المنشأة"
dotnet test tests/Brqosh.Application.Tests --filter "FullyQualifiedName~ZatcaLiveCsidTests"
```

## الخطوة 5 — 10 فواتير إنتاج

لكل فاتورة (1…10):

1. أنشئ فاتورة مبيعات عادية
2. رحّل + أرسل ZATCA
3. سجّل: رقم الفاتورة، UUID، حالة القبول، الطابع الزمني
4. لقطة شاشة من بوابة الهيئة (إن وُجدت)

## الخطوة 6 — أرشفة الأدلة

```
dist/evidence/O-01-zatca/
  invoices.csv          # رقم، UUID، تاريخ، حالة
  screenshots/          # لقطات القبول
  csid-summary.txt      # تواريخ الشهادات (بدون أسرار)
```

## الحكم

✅ مغلق فقط عند: OTP حي + 10 فواتير مقبولة + أدلة أرشيفية  
⏭ قبل ذلك: خارجي — ليس نقص كود
