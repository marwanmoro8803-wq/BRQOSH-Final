-- BRQOSH · المرحلة 3: التحقق من المفاتيح على البيانات القديمة (قفل SHARE UPDATE EXCLUSIVE فقط).
-- يفشل أيّ سطر هنا إن بقيت بيانات يتيمة — أعد تشغيل 01 وصحّح ثم أعد المحاولة.
ALTER TABLE journal_entries          VALIDATE CONSTRAINT fk_journal_entries_company;
ALTER TABLE sales_invoices           VALIDATE CONSTRAINT fk_sales_invoices_company;
ALTER TABLE purchase_invoices        VALIDATE CONSTRAINT fk_purchase_invoices_company;
ALTER TABLE credit_notes             VALIDATE CONSTRAINT fk_credit_notes_company;
ALTER TABLE payment_allocation_lines VALIDATE CONSTRAINT fk_pal_company;
ALTER TABLE sales_invoice_lines      VALIDATE CONSTRAINT fk_sales_invoice_lines_product;
ALTER TABLE purchase_invoice_lines   VALIDATE CONSTRAINT fk_purchase_invoice_lines_product;
ALTER TABLE stock_movement_lines     VALIDATE CONSTRAINT fk_stock_movement_lines_product;
ALTER TABLE purchase_invoices        VALIDATE CONSTRAINT fk_purchase_invoices_po;
ALTER TABLE credit_notes             VALIDATE CONSTRAINT fk_credit_notes_original_invoice;
ALTER TABLE payment_allocation_lines VALIDATE CONSTRAINT fk_pal_receipt_voucher;
ALTER TABLE payment_allocation_lines VALIDATE CONSTRAINT fk_pal_payment_voucher;
