# CEEF-007-G — مجلس الوكلاء: CEEF-006-A (6 وحدات)

| الحقل | القيمة |
|-------|--------|
| **الملفات** | `CEEF-006-A_فهرس` + 01..06 |
| **التاريخ** | 2026-07-08 |

---

## 1. ملخص الوحدات

| الوحدة | الدرجة الأصلية | بعد 007 | أبرز الإغلاق |
|--------|----------------|---------|--------------|
| 01 ZATCA | 3.5/10 | **4.5/10** | Z-001 إلغاء فاتورة · Z-002/Z-003 DebitNote 383 |
| 02 المخزون | 5.0/10 | **6.5/10** | INV-001 Transfer · INV-002 WAC · INV-003 · INV-005 جرد |
| 03 المقاولات | 5.5/10 | **6.0/10** | VAT مستخلصات (سابقاً) — retention/supplier balance مفتوح |
| 04 الأمان | 4.5/10 | **6.0/10** | PasswordChange · SecurityContext · admin123 |
| 05 التقارير | 6.0/10 | **6.5/10** | GL opening · VAT مستخلصات UI |
| 06 UX | 6.5/10 | **6.8/10** | MustChangePassword UI · FlaUI dialogs |
| **الإجمالي** | **5.2/10** | **~6.2/10** | |

---

## 2. P0/P1 المفتوحة (موثّقة)

### ZATCA (01)
- Z-004..Z-013: UBL كامل، توقيع XAdES، CSID compliance، QR Phase 2
- Z-010: إلغاء مستخلص بلا ZATCA

### المخزون (02)
- INV-006 multi-warehouse
- INV-007 COGS من تكلفة البيع

### المقاولات (03)
- retention release → supplier balance
- subcontract accounting gaps

### الأمان (04)
- PEM bypass ترخيص
- RBAC كامل على كل الخدمات

### التقارير (05)
- cash flow statements
- SOCPA external review

### UX (06)
- Pagination قوائم
- UiAudit 0 فشل مُثبت

---

## 3. اختبارات جديدة (007-G)

| الاختبار | الوحدة |
|----------|--------|
| `WacCalculationTests` (2) | مخزون |
| `CancelPostedCreditNote_GeneratesZatcaDebitNote` | ZATCA |
| `StockTransferTests` | مخزون |
| `DepreciationServiceTests` JV فريد | محاسبة |

---

## 4. قرار المجلس

مراجعة CEEF-006-A **مُحدَّثة بالتنفيذ** على P0 الحرجة. الجاهزية التجارية تتطلب إغلاق ZATCA P1 وUiAudit.

*التالي: التقرير النهائي `CEEF-007-FINAL`*
