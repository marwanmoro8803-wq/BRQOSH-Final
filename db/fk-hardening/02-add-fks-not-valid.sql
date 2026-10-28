-- BRQOSH · المرحلة 2: إضافة المفاتيح الأجنبية بـ RESTRICT و NOT VALID (PostgreSQL)
-- NOT VALID: تُفرض على الكتابات الجديدة فوراً دون فحص الصفوف القديمة ودون قفل طويل.
-- لا تُشغَّل إلا بعد أن يُرجع 01-orphan-check.sql صفر صفوف (أو بعد تصحيحها يدوياً).
BEGIN;
ALTER TABLE journal_entries        ADD CONSTRAINT fk_journal_entries_company      FOREIGN KEY ("CompanyId")        REFERENCES companies("Id")        ON DELETE RESTRICT NOT VALID;
ALTER TABLE sales_invoices         ADD CONSTRAINT fk_sales_invoices_company       FOREIGN KEY ("CompanyId")        REFERENCES companies("Id")        ON DELETE RESTRICT NOT VALID;
ALTER TABLE purchase_invoices      ADD CONSTRAINT fk_purchase_invoices_company    FOREIGN KEY ("CompanyId")        REFERENCES companies("Id")        ON DELETE RESTRICT NOT VALID;
ALTER TABLE credit_notes           ADD CONSTRAINT fk_credit_notes_company         FOREIGN KEY ("CompanyId")        REFERENCES companies("Id")        ON DELETE RESTRICT NOT VALID;
ALTER TABLE payment_allocation_lines ADD CONSTRAINT fk_pal_company               FOREIGN KEY ("CompanyId")        REFERENCES companies("Id")        ON DELETE RESTRICT NOT VALID;
ALTER TABLE sales_invoice_lines    ADD CONSTRAINT fk_sales_invoice_lines_product  FOREIGN KEY ("ProductId")        REFERENCES products("Id")         ON DELETE RESTRICT NOT VALID;
ALTER TABLE purchase_invoice_lines ADD CONSTRAINT fk_purchase_invoice_lines_product FOREIGN KEY ("ProductId")      REFERENCES products("Id")         ON DELETE RESTRICT NOT VALID;
ALTER TABLE stock_movement_lines   ADD CONSTRAINT fk_stock_movement_lines_product FOREIGN KEY ("ProductId")        REFERENCES products("Id")         ON DELETE RESTRICT NOT VALID;
ALTER TABLE purchase_invoices      ADD CONSTRAINT fk_purchase_invoices_po         FOREIGN KEY ("PurchaseOrderId")  REFERENCES purchase_orders("Id")  ON DELETE RESTRICT NOT VALID;
ALTER TABLE credit_notes           ADD CONSTRAINT fk_credit_notes_original_invoice FOREIGN KEY ("OriginalInvoiceId") REFERENCES sales_invoices("Id") ON DELETE RESTRICT NOT VALID;
ALTER TABLE payment_allocation_lines ADD CONSTRAINT fk_pal_receipt_voucher       FOREIGN KEY ("ReceiptVoucherId") REFERENCES receipt_vouchers("Id") ON DELETE RESTRICT NOT VALID;
ALTER TABLE payment_allocation_lines ADD CONSTRAINT fk_pal_payment_voucher       FOREIGN KEY ("PaymentVoucherId") REFERENCES payment_vouchers("Id") ON DELETE RESTRICT NOT VALID;
COMMIT;
