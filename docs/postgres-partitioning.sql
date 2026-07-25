# BRQOSH — تقسيم PostgreSQL للأحجام الكبيرة

**متى تُنفَّذ:** عند تجاوز ~2–5 مليون قيد يومية أو بطء ملحوظ في تقارير GL/الفواتير.  
**المتطلبات:** PostgreSQL 15+، نسخة احتياطية كاملة، نافذة صيانة.

## 1. الجداول المرشّحة للتقسيم

| الجدول | مفتاح التقسيم | السبب |
|--------|---------------|--------|
| `journal_entries` | `EntryDate` (سنوي) | أعلى حجم واستعلامات زمنية |
| `journal_entry_lines` | عبر FK للقيد الأب | يتبع تقسيم القيود |
| `sales_invoices` | `InvoiceDate` (سنوي) | أرشفة فواتير قديمة |
| `zatca_e_invoice_documents` | `GeneratedAtUtc` (سنوي) | أرشفة مستندات ZATCA |

## 2. مثال: تقسيم `journal_entries` بالسنة

```sql
-- الخطوة 0: نسخ احتياطي
-- pg_dump -Fc brqosh > brqosh_pre_partition.dump

BEGIN;

-- 1) إنشاء جدول أب مُقسَّم
CREATE TABLE journal_entries_partitioned (
    LIKE journal_entries INCLUDING DEFAULTS INCLUDING CONSTRAINTS
) PARTITION BY RANGE ("EntryDate");

-- 2) أقسام السنوات (وسّع حسب الحاجة)
CREATE TABLE journal_entries_y2024 PARTITION OF journal_entries_partitioned
    FOR VALUES FROM ('2024-01-01') TO ('2025-01-01');
CREATE TABLE journal_entries_y2025 PARTITION OF journal_entries_partitioned
    FOR VALUES FROM ('2025-01-01') TO ('2026-01-01');
CREATE TABLE journal_entries_y2026 PARTITION OF journal_entries_partitioned
    FOR VALUES FROM ('2026-01-01') TO ('2027-01-01');
CREATE TABLE journal_entries_y2027 PARTITION OF journal_entries_partitioned
    FOR VALUES FROM ('2027-01-01') TO ('2028-01-01');

-- 3) نسخ البيانات
INSERT INTO journal_entries_partitioned SELECT * FROM journal_entries;

-- 4) إعادة تسمية (توقف التطبيق أولاً)
ALTER TABLE journal_entries RENAME TO journal_entries_legacy;
ALTER TABLE journal_entries_partitioned RENAME TO journal_entries;

-- 5) إعادة إنشاء الفهارس على الجدول الأب (تُورَّث للأقسام)
CREATE INDEX IF NOT EXISTS ix_journal_entries_company_date
    ON journal_entries ("CompanyId", "EntryDate");
CREATE UNIQUE INDEX IF NOT EXISTS ix_journal_entries_company_number
    ON journal_entries ("CompanyId", "EntryNumber");

COMMIT;

-- 6) بعد التحقق: DROP TABLE journal_entries_legacy;
```

## 3. سطور القيود (`journal_entry_lines`)

بعد تقسيم القيود، تأكد أن `journal_entry_id` يشير لمعرّفات صحيحة.  
إذا لزم، أنشئ `journal_entry_lines_partitioned` بنفس نطاق `EntryDate` عبر JOIN عند الترحيل.

## 4. فواتير المبيعات

```sql
CREATE TABLE sales_invoices_partitioned (
    LIKE sales_invoices INCLUDING DEFAULTS INCLUDING CONSTRAINTS
) PARTITION BY RANGE ("InvoiceDate");

CREATE TABLE sales_invoices_y2026 PARTITION OF sales_invoices_partitioned
    FOR VALUES FROM ('2026-01-01') TO ('2027-01-01');
-- ... نفس نمط journal_entries
```

## 5. صيانة دورية

```sql
-- قسم جديد لكل سنة قبل 1 يناير
CREATE TABLE journal_entries_y2028 PARTITION OF journal_entries
    FOR VALUES FROM ('2028-01-01') TO ('2029-01-01');

-- أرشفة قديم: DETACH + نقل إلى tablespace أرخص
ALTER TABLE journal_entries DETACH PARTITION journal_entries_y2024;
```

## 6. ملاحظات BRQOSH

- التطبيق **لا ينفّذ التقسيم تلقائياً** — يُدار من DBA.
- EF Core يعمل مع الجدول المُقسَّم كجدول عادي طالما الاسم `journal_entries` لم يتغيّر.
- بعد التقسيم شغّل `VACUUM ANALYZE` على الأقسام الجديدة.
- راجع `BrqoshSchemaUpdater` — لا يتعارض مع التقسيم اليدوي.
