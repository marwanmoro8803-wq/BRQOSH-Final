# خطة المفاتيح الأجنبية التدريجية (BRQOSH · Phase 1)

المرجع: BrqoshDbContext.cs:291,362,494 · PaymentAllocationLine.cs:21-22 · JournalEntry.cs:41
لم يُشغَّل أيّ شيء على قاعدة حقيقية. السكربتات لـ PostgreSQL. أما SQLite فلا يدعم `ADD CONSTRAINT`، ويحتاج إعادة بناء الجدول عبر هجرة EF.

| المرحلة | الملف | الأثر |
|---|---|---|
| 0 | نسخة احتياطية كاملة | — |
| 1 | `01-orphan-check.sql` | قراءة فقط. صحّح كل صفّ ناتج يدوياً (اربطه أو اعكسه) ولا تحذف شيئاً |
| 2 | `02-add-fks-not-valid.sql` | يمنع أي يتيم جديد فوراً دون فحص القديم ودون قفل طويل |
| 3 | `03-validate-fks.sql` | يتحقق من القديم، ويُشغَّل بعد أن تُرجع المرحلة 1 صفراً |
| 4 | هجرة EF | إضافة `HasOne<...>().HasForeignKey(...).OnDelete(Restrict)` في BrqoshDbContext لكل مفتاح أعلاه، ثم `dotnet ef migrations add AddRestrictForeignKeys` ومطابقة BrqoshSchemaUpdater |
| 5 | (لاحقاً) | استبدال المرجع العام في التخصيص (DocumentType+DocumentId) بأعمدة `SalesInvoiceId`/`PurchaseInvoiceId`/`ProgressBillingId` منفصلة لكل منها FK، واستبدال `SourceDocument` النصي في القيد بأعمدة مفاتيح فعلية |

ملاحظات:
- مفتاح مركز التكلفة مستبعد عمداً (التعليق عند BrqoshDbContext.cs:286).
- يُفترض أن `products` و`purchase_orders` و`receipt_vouchers` و`payment_vouchers` بمفتاح `"Id"`. تحقّق من ذلك قبل المرحلة 2.
- لم تُدرج جداول المستخلصات (ProgressBilling) لأن اسم جدولها لم يُتحقَّق منه.
