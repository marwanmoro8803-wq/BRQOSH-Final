# خريطة نطاقات BRQOSH — Enterprise Domain Architecture Freeze

**المرجع:** `docs/مهم` مرحلة -1 · ROADMAP مرحلة 5  
**التاريخ:** 2026-07-09 (V12 مغلق — CEEF-116)  
**الحالة:** معتمدة كمرجع معماري — لا تُخلط النطاقات عبر حدود غير موثّقة

---

## 1. النطاقات المستقلة

```
Financial Core
├── General Ledger          Brqosh.Domain.Accounting
├── Accounts Payable        Purchasing + PaymentVouchers
├── Accounts Receivable     Sales + ReceiptVouchers
├── Treasury                Brqosh.Domain.Treasury (+ Branch FX stubs)
├── Fixed Assets            Domain.Assets
└── Tax Engine              VAT · ZATCA · VatReturnQuery

Commercial Core
├── Sales                   Domain.Sales
├── Procurement             Domain.Purchasing (PO · GRN · PI · DN)
├── Inventory               Domain.Inventory (WAC/FIFO · Landed cost)
├── Pricing                 عبر Product.SalePrice (توسيع لاحق)
└── CRM                     stub — خارج النطاق الحالي

Operational Core
├── Projects / Construction Domain.Construction
├── Manufacturing           Modules.Manufacturing · BOM/WO
├── HR                      stub
└── Payroll                 Domain.Payroll

Platform Core
├── Identity                Domain.Security · AppUser/RBAC
├── Workflow                ✅ PR + PO/Payment/ManualJE — `WorkflowEngine` (CEEF-105)
├── Audit                   AuditService + AuditLog
├── Notifications           UiFeedback (desktop)
├── Reporting               Application.Reporting contracts
├── Events                  Domain.Platform · IDomainEventPublisher
└── AI Platform             ⏭ بعد Data Foundation
```

---

## 2. حدود الاعتماد

| من → إلى | القاعدة |
|----------|---------|
| Domain | لا يعتمد على Infrastructure/UI |
| Application | عقود فقط؛ تنفيذ في Infrastructure |
| Infrastructure | ينفّذ Domain + Application contracts |
| Desktop / Api | يستدعي Application/Infrastructure عبر DI أو AppServices |
| Modules.* | قطاعات اختيارية فوق النواة |

## 3. عزل البيانات

- **اليوم:** `CompanyId` على كل الكيانات التشغيلية (multi-company soft).
- **فرع:** كيان `Branch` + ربط اختياري لاحقاً.
- **عملة:** `Company.BaseCurrencyCode` / `FunctionalCurrencyCode` + `FxConversion`.
- **SaaS tenant:** خارج هذا التجميد — يتطلب صفوف Tenant + connection isolation.

## 4. قاعدة التجميد

لا تُضاف ميزة عالمية (SaaS/AI/cloud) قبل:

1. صحة مالية (Financial Truth).
2. حدث نطاق قابل للتدقيق عند الترحيل/الإلغاء.
3. اختبارات Golden للحركات المالية.
