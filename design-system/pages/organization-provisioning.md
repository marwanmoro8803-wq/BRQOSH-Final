# Organization Provisioning — إنشاء وإعداد المنشآت (G-69)

**Pattern:** Multi-step wizard · ProfileCard sector picker · Template preview  
**Status:** 🔜 مخطّط — V13-A2 · CEEF-116  
**Plan:** `.cursor/plans/مهم_v12_إغلاق_شامل_8c9973cd.plan.md` — قسم G-69

---

## المشكلة

شاشة **تسجيل الدخول / إنشاء حساب** تُنشئ **مستخدماً** فقط داخل منشأة موجودة.  
لا يوجد مسار واضح لإنشاء **منشأة جديدة** بقطاع محدد (مقاولات · محلات · مولات · مستشفيات · صيدليات · مراكز · مستودعات …) بعد الإعداد الأولي.

---

## الفصل الإلزامي (Identity vs Organization)

| التدفق | الشاشة | الناتج |
|--------|--------|--------|
| تسجيل دخول | `LoginWindow` | جلسة مستخدم |
| طلب حساب مستخدم | `LoginWindow` → تبويب التسجيل | `AppUser` Viewer معطّل |
| إعداد أولي (مرة واحدة) | `SetupWizardWindow` | منشأة + مدير |
| إنشاء منشأة إضافية | `OrganizationProvisionerWindow` 🔜 | منشأة + هيكل + مدير |
| SaaS signup | Web portal 🔜 | Tenant + Company |

---

## Layout — OrganizationProvisionerWindow (5 خطوات)

```
┌─────────────────────────────────────────────────────────────┐
│ Stepper: ① قطاع  ② دولة  ③ بيانات قانونية  ④ هيكل  ⑤ مدير │
├─────────────────────────────────────────────────────────────┤
│ ① Sector grid (ProfileCard × N)                             │
│    معاينة: وحدات + ميزات من ConfigurationTemplateLoader      │
├─────────────────────────────────────────────────────────────┤
│ [← السابق]                              [التالي →] [إنشاء]  │
└─────────────────────────────────────────────────────────────┘
```

### Step ① — القطاع والنوع الفرعي

| BusinessProfile | بطاقات OrganizationSubType (أمثلة) |
|-----------------|-------------------------------------|
| Construction ✅ | مقاول عام · مقاول باطن |
| Manufacturing ✅ | إنتاج متقطع · إنتاج مستمر |
| Retail ✅ | محل واحد · سلسلة · POS فقط |
| RealEstate 🔜 | مول · مجمع تجاري |
| Healthcare 🔜 | مستشفى · عيادة · صيدلية |
| Logistics 🔜 | مستودع 3PL · توزيع |
| General ✅ | مكتب محاسبة · خدمات عامة |

### Step ② — قالب الدولة

- نفس نمط `SetupWizardWindow` — `CountryTemplateTitle` + compliance strip
- افتراضي: `saudi-arabia`

### Step ③ — بيانات المنشأة القانونية

| Field | Required | Notes |
|-------|----------|-------|
| اسم المنشأة | ✅ | `Company.NameAr` |
| الرقم الضريبي | 🔶 | 15 رقم — مطلوب قبل ZATCA |
| السجل التجاري | 🔜 | `CommercialRegistration` |
| العنوان الوطني | 🔜 | ZATCA / فوترة |
| الشعار | اختياري | مستقبلاً |

### Step ④ — هيكل تشغيلي أولي

| قطاع | عناصر تُنشأ تلقائياً |
|------|---------------------|
| Retail | فرع رئيسي + مخزن/محل |
| Construction | فرع HQ + مركز تكلفة «مشاريع» |
| Healthcare | فرع + قسم/صيدلية داخلية |
| Mall | فرع + وحدات إيجار (stub) |
| Warehouse | فرع + مستودع افتراضي |

### Step ⑤ — مدير المنشأة

- اسم مستخدم · كلمة مرور · تأكيد
- `PasswordPolicy` · دور Administrator
- Desktop: فحص `LicenseStore` قبل الإنشاء

---

## OrganizationsPage (إدارة المنشآت)

| Action | Permission |
|--------|------------|
| عرض قائمة المنشآت | `CanManageCompanies` |
| إنشاء منشأة | `CanManageCompanies` |
| التبديل للمنشأة | مستخدم مرتبط بالمنشأة |
| أرشفة/تجميد | `CanManageCompanies` + LIFECYCLE-100 |

---

## Company Switcher — MainWindow

- موضع: شريط علوي بجانب اسم المنشأة الحالية
- Dropdown: منشآت المستخدم
- عند التبديل: `AppServices.CompanyId` · إعادة بناء `NavigationCatalog`

---

## WPF Mapping

| شاشة | ملف |
|------|-----|
| معالج إنشاء منشأة | `Views/OrganizationProvisionerWindow.xaml` 🔜 |
| قائمة المنشآت | `Views/Pages/OrganizationsPage.xaml` 🔜 |
| إعداد أولي | `Views/SetupWizardWindow.xaml` (توسيع) |
| إعدادات منشأة | `Views/Pages/CompanySettingsPage.xaml` |
| تسجيل مستخدم | `Views/LoginWindow.xaml` (توضيح نصي) |

---

## Config templates

```
config/templates/
  countries/saudi-arabia.json
  industries/
    construction.json      ✅
    manufacturing.json     🔜
    retail.json            🔜
    retail-chain.json      🔜
    mall.json              🔜
    hospital.json          🔜
    medical-center.json    🔜
    pharmacy.json          🔜
    warehouse.json         🔜
    general.json           🔜
```

---

## Lifecycle

- Step 1 (Identity): company · branch · **multi-sector provisioning**
- Step 10: sector templates for new companies
- G-69 closes the gap between hero marketing copy and actual onboarding UX
