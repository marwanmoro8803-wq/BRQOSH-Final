-- BRQOSH · فحص البيانات اليتيمة قبل إضافة المفاتيح الأجنبية (PostgreSQL)
-- قراءة فقط: SELECT لا غير. شغّله على نسخة احتياطية أولاً. كل صفّ ناتج = مرجع معلّق.
-- افتراض: أسماء الجداول snake_case والأعمدة PascalCase بين علامتي تنصيص (كما في BrqoshSchemaUpdater).

-- 1) المرجع إلى الشركة
SELECT 'journal_entries.CompanyId' AS ref, e."Id", e."CompanyId" AS missing_id FROM journal_entries e
 WHERE NOT EXISTS (SELECT 1 FROM companies c WHERE c."Id" = e."CompanyId")
UNION ALL
SELECT 'sales_invoices.CompanyId', i."Id", i."CompanyId" FROM sales_invoices i
 WHERE NOT EXISTS (SELECT 1 FROM companies c WHERE c."Id" = i."CompanyId")
UNION ALL
SELECT 'purchase_invoices.CompanyId', i."Id", i."CompanyId" FROM purchase_invoices i
 WHERE NOT EXISTS (SELECT 1 FROM companies c WHERE c."Id" = i."CompanyId")
UNION ALL
SELECT 'credit_notes.CompanyId', n."Id", n."CompanyId" FROM credit_notes n
 WHERE NOT EXISTS (SELECT 1 FROM companies c WHERE c."Id" = n."CompanyId")
UNION ALL
SELECT 'payment_allocation_lines.CompanyId', a."Id", a."CompanyId" FROM payment_allocation_lines a
 WHERE NOT EXISTS (SELECT 1 FROM companies c WHERE c."Id" = a."CompanyId")

-- 2) المرجع إلى الصنف
UNION ALL
SELECT 'sales_invoice_lines.ProductId', l."Id", l."ProductId" FROM sales_invoice_lines l
 WHERE l."ProductId" IS NOT NULL AND NOT EXISTS (SELECT 1 FROM products p WHERE p."Id" = l."ProductId")
UNION ALL
SELECT 'purchase_invoice_lines.ProductId', l."Id", l."ProductId" FROM purchase_invoice_lines l
 WHERE l."ProductId" IS NOT NULL AND NOT EXISTS (SELECT 1 FROM products p WHERE p."Id" = l."ProductId")
UNION ALL
SELECT 'stock_movement_lines.ProductId', l."Id", l."ProductId" FROM stock_movement_lines l
 WHERE NOT EXISTS (SELECT 1 FROM products p WHERE p."Id" = l."ProductId")

-- 3) أمر الشراء والفاتورة الأصلية للإشعار
UNION ALL
SELECT 'purchase_invoices.PurchaseOrderId', i."Id", i."PurchaseOrderId" FROM purchase_invoices i
 WHERE i."PurchaseOrderId" IS NOT NULL AND NOT EXISTS (SELECT 1 FROM purchase_orders o WHERE o."Id" = i."PurchaseOrderId")
UNION ALL
SELECT 'credit_notes.OriginalInvoiceId', n."Id", n."OriginalInvoiceId" FROM credit_notes n
 WHERE NOT EXISTS (SELECT 1 FROM sales_invoices i WHERE i."Id" = n."OriginalInvoiceId")

-- 4) التخصيصات (مرجع عام متعدد الأنواع — PaymentAllocationLine.cs:21-22)
UNION ALL
SELECT 'payment_allocation_lines.ReceiptVoucherId', a."Id", a."ReceiptVoucherId" FROM payment_allocation_lines a
 WHERE a."ReceiptVoucherId" IS NOT NULL AND NOT EXISTS (SELECT 1 FROM receipt_vouchers v WHERE v."Id" = a."ReceiptVoucherId")
UNION ALL
SELECT 'payment_allocation_lines.PaymentVoucherId', a."Id", a."PaymentVoucherId" FROM payment_allocation_lines a
 WHERE a."PaymentVoucherId" IS NOT NULL AND NOT EXISTS (SELECT 1 FROM payment_vouchers v WHERE v."Id" = a."PaymentVoucherId")
UNION ALL
SELECT 'payment_allocation_lines.Document(SalesInvoice)', a."Id", a."DocumentId" FROM payment_allocation_lines a
 WHERE a."DocumentType" = 'SalesInvoice' AND NOT EXISTS (SELECT 1 FROM sales_invoices i WHERE i."Id" = a."DocumentId")
UNION ALL
SELECT 'payment_allocation_lines.Document(PurchaseInvoice)', a."Id", a."DocumentId" FROM payment_allocation_lines a
 WHERE a."DocumentType" = 'PurchaseInvoice' AND NOT EXISTS (SELECT 1 FROM purchase_invoices i WHERE i."Id" = a."DocumentId")
UNION ALL
SELECT 'payment_allocation_lines.DocumentType(unknown)', a."Id", NULL FROM payment_allocation_lines a
 WHERE a."DocumentType" NOT IN ('SalesInvoice','PurchaseInvoice','ProgressBilling')
 -- ProgressBilling: أضف جدول المستخلصات بعد تأكيد اسمه.

-- 5) القيد يشير إلى مستنده بنص (JournalEntry.cs:41) — فاتورة مبيعات/مشتريات
UNION ALL
SELECT 'journal_entries.SourceDocument(SalesInvoice)', e."Id", NULL FROM journal_entries e
 WHERE e."SourceDocument" LIKE 'SalesInvoice:%'
   AND NOT EXISTS (SELECT 1 FROM sales_invoices i WHERE e."SourceDocument" = 'SalesInvoice:' || i."InvoiceNumber" AND i."CompanyId" = e."CompanyId")
UNION ALL
SELECT 'journal_entries.SourceDocument(PurchaseInvoice)', e."Id", NULL FROM journal_entries e
 WHERE e."SourceDocument" LIKE 'PurchaseInvoice:%'
   AND NOT EXISTS (SELECT 1 FROM purchase_invoices i WHERE e."SourceDocument" = 'PurchaseInvoice:' || i."InvoiceNumber" AND i."CompanyId" = e."CompanyId")
ORDER BY 1, 2;
