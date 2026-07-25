# اختبار CSID حي (ZATCA)

اختبار اختياري يتطلب OTP فعلي من [بوابة فاتورة](https://fatoora.zatca.gov.sa).

## المتغيرات

| المتغير | مطلوب | الوصف |
| ------- | ------ | ------ |
| `BRQOSH_ZATCA_OTP` | نعم | رمز OTP من البوابة (صلاحية قصيرة) |
| `BRQOSH_ZATCA_VAT` | نعم | الرقم الضريبي 15 رقماً للمنشأة |
| `BRQOSH_ZATCA_ORG` | لا | اسم المنشأة (افتراضي: شركة BRQOSH) |
| `BRQOSH_ZATCA_API_URL` | لا | بوابة المطورين (افتراضي: developer-portal) |

## تشغيل الاختبار

```powershell
$env:BRQOSH_ZATCA_OTP = "123456"
$env:BRQOSH_ZATCA_VAT = "300000000000003"
$env:BRQOSH_ZATCA_ORG = "اسم الشركة"
dotnet test tests/Brqosh.Application.Tests --filter "FullyQualifiedName~ZatcaLiveCsidTests"
```

بدون المتغيرات يُتخطَّى الاختبار بنجاح (لا فشل في CI).

## من الواجهة

استخدم **إعدادات ZATCA → معالج CSID** (`ZatcaCsidWizardWindow`) لنفس التدفق يدوياً:

1. توليد مفتاح + CSR
2. إدخال OTP وطلب شهادة الامتثال
3. طلب شهادة الإنتاج
