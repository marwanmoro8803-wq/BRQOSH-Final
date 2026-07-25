# CEEF-025 — إغلاق O-07 كاملاً: BranchId على كل الكيانات التشغيلية

| الحقل | القيمة |
|-------|--------|
| **النطاق** | `IBranchScoped` على 28 كياناً تشغيلياً + مخطط + ترحيل |
| **التاريخ** | 2026-07-08 |
| **الحكم** | **O-07 مغلق داخل المستودع** — O-01…O-06 و O-08…O-13 تبقى ⏭ (خارجي/استراتيجي) |

---

## 1. ما أُغلق (O-07)

### واجهة موحّدة
- `IBranchScoped` في `Brqosh.Domain.Companies`
- `BranchAssignmentHelper` — يفضّل فرع المستند الأصل ثم جلسة المستخدم ثم HQ

### 28 كياناً تشغيلياً بـ `BranchId` + `AssignBranch`

| المجموعة | الكيانات |
|----------|----------|
| محاسبة/مبيعات/مشتريات (7 سابقة) | JE · SI · PI · GRN · SM · RV · PV |
| مبيعات/مشتريات إضافية | CreditNote · DebitNote · PurchaseOrder |
| خزينة | Cheque · BankAccount · PaymentAllocationLine · ReconciledBankEntry |
| ZATCA | ZatcaEInvoiceDocument |
| مخزون | Warehouse · InventoryBalance · InventoryCostLayer |
| أصول/رواتب/POS | FixedAsset · PayrollRun · PosShift |
| مقاولات/تصنيع | ProgressBilling · ConstructionProject · Contract · Subcontract · ProjectCostEntry · WorkOrder · BOM |

**مستثنى متعمد (بيانات رئيسية):** Company · Branch · Account · Customer · Supplier · Product · FiscalPeriod · CostCenter · AppUser · ZatcaSettings

### بنية تحتية
- `MapBranchScope<T>` في `BrqoshDbContext` — فهرس `(CompanyId, BranchId)` لـ 21 كياناً جديداً
- `EnsureBranchIdColumnsAsync` — 28 جدولاً (Postgres + SQLite)

### ترحيل/تشغيل
- CN/DN يرثان فرع الفاتورة الأصل
- PB · Payroll · Cheque · FixedAsset · ReconciledBankEntry — `EnsureAssignedAsync` عند الترحيل
- POS shift — فرع الجلسة عند فتح الوردية

---

## 2. أدلة تشغيل

| الفحص | النتيجة |
|--------|---------|
| Domain | **43/43 PASS** |
| Application | **144/144 PASS** (`BRQOSH_SKIP_STRESS=1`) — +1 CN فرع |
| Api | **13/13 PASS** |
| **المجموع** | **200/200 PASS** |
| Solution build | **0 Error / 0 Warning** |
| BranchAssignment tests | **6/6 PASS** |

---

## 3. ما لم يُغلق — ولماذا (صريح)

| البند | السبب — لا يُغلق بالكود وحده |
|-------|------------------------------|
| O-01 | OTP هيئة الزكاة + إنتاج حي |
| O-02 | مراجع SOCPA مستقل |
| O-03 | MSI على VM ميداني نظيف |
| O-04 | Load 1M — أجهزة/ساعات |
| O-05 | OWASP ZAP حي — شبكة/ماسح |
| O-06 | Coverlet ≥75% على **كل** مشاريع الحل (ليس artifact واحد) |
| O-08 | FX translation + تقارير مجموعة — نموذج GroupCompany |
| O-09…O-13 | SaaS · Cloud HA · BI · LLM · Mobile — إعادة تصميم/منتج منفصل |

**قاعدة المشروع:** لا نُعلّم ✅ على بنود ⏭ دون دليل خارجي أو إعادة تصميم كاملة.

---

## 4. الحكم

| الادعاء | الحكم |
|---------|--------|
| BranchId على كل الكيانات التشغيلية + ترحيل | ✅ |
| O-01…O-06 · O-08…O-13 مغلقة | ❌ — انظر القسم 3 |

**التالي:** Global Platform (مرحلة 7) أو O-01…O-05 حسب السوق.
