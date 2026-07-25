# O-03 — MSI على VM نظيفة + توقيع ميداني

## أ) تحقق داخل المستودع

```powershell
.\scripts\ops\verify-msi-build.ps1
```

يتوقع: `dist\BRQOSH\Brqosh.Desktop.exe` و (إن وُجد WiX) `dist\BRQOSH-Setup.msi`

## ب) VM نظيفة (Windows 10/11)

### مواصفات مقترحة

- 4 GB RAM · 40 GB قرص
- **بدون** Visual Studio / .NET SDK
- اتصال إنترنت (لتثبيت PostgreSQL إن لزم)

### خطوات التثبيت

1. انسخ `BRQOSH-Setup.msi` إلى VM
2. شغّل المثبت كمسؤول
3. أكمل معالج الإعداد الأولي (شركة + VAT)
4. سجّل الدخول كمسؤول
5. شغّل FullScenarioAudit ضد نفس قاعدة البيانات (أو استورد نسخة PostgreSQL)

### لقطات مطلوبة

| # | المحتوى |
|---|---------|
| 1 | شاشة تثبيت MSI |
| 2 | البرنامج بعد أول تشغيل |
| 3 | لوحة التحكم مع QA-AUDIT |
| 4 | ميزان مراجعة متوازن |

احفظ في `dist/evidence/O-03-msi-vm/screenshots/`

## ج) التوقيع الميداني (اختياري للإنتاج)

- شهادة Authenticode على `BRQOSH-Setup.msi`
- توثيق thumbprint في `installer/README.md`

## الحكم

⏭ خارجي حتى VM نظيف + لقطات
