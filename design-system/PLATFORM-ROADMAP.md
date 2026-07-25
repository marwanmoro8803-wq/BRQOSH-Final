# BRQOSH — Platform Roadmap & UI Alignment

> **Source of truth:** [`docs/مهم`](../docs/مهم) — خارطة طريق v4.0 · مجلس الخبراء  
> **Purpose:** Bridge strategic phases → design system · domain architecture → UI modules  
> **Product identity:** BRQOSH is a **Financial Operating System Platform** — not merely accounting software

---

## 0. الرؤية النهائية

BRQOSH = **نظام تشغيل اقتصادي للمؤسسات** يجمع:

| Domain | Scope |
|--------|-------|
| المحاسبة · التجارة · الإدارة · العمليات · الامتثال · AI · التحليلات | منصة واحدة |
| تعدد الدول · العملات · التشريعات · القطاعات | Global-ready architecture |
| الأفراد · SME · Enterprise · Government | Scalable segments |

**UI implication:** Every screen must feel like one platform — luxury design system is the visual layer of this operating system, not decoration.

---

## 1. Financial Truth First (إلزامي قبل أي UI فاخر)

Any global ERP UI must answer with confidence:

| Question | UI must show |
|----------|--------------|
| أين ذهبت الأموال؟ | Ledger · reports · drill-down · tabular amounts |
| لماذا حدثت الحركة؟ | Line descriptions · linked documents · audit context |
| من قام بها؟ | User badge · audit log · posted-by column |
| متى حدثت؟ | Timestamps · fiscal period · Hijri option |
| ما أثرها المالي؟ | Balance impact · trial balance tie-out |
| هل يمكن تدقيقها؟ | Immutable audit trail · posting status · journal link |

**Priority order (strategic):**

1. صحة المحاسبة → debit/credit balance UI · posting gates · ACC rules feedback
2. سلامة البيانات → validation messages · fail-closed dialogs
3. الأمن → RBAC · permission-denied states · session idle
4. الامتثال → ZATCA/VAT status chips · filing calendar
5. التوسع → only after 1–4 are trustworthy in UI

**Design rule:** Beauty never hides financial truth. Glass/luxury surfaces wrap **auditable data**, not replace it.

---

## 2. Domain Architecture → UI Module Map

```
Financial Core          → UI modules
├── General Ledger      → general-ledger.md · reports-analytics.md
├── Accounts Payable    → purchases-suppliers.md · bank-cash.md
├── Accounts Receivable → invoicing-sales.md · bank-cash.md
├── Treasury            → bank-cash.md · dashboard-executive.md
├── Fixed Assets        → fixed-assets.md
└── Tax Engine          → compliance-tax.md

Commercial Core
├── Sales               → invoicing-sales.md
├── Procurement         → purchases-suppliers.md
├── Inventory           → inventory.md
├── Pricing             → (future — product dialogs)
└── CRM                 → customers · suppliers pages

Operational Core
├── Projects            → Projects · BOQ · Progress Billings pages
├── Construction        → Retentions · Contracts · WIP pages
├── Manufacturing       → BOM · Work Orders pages
├── HR                  → payroll-hr.md · UsersPage
└── Payroll             → payroll-hr.md

Platform Core
├── Identity            → login.md · settings-multi-company.md
├── Workflow            → approval chains UI (P1 — expenses · PO)
├── Audit               → AuditLogPage · general-ledger audit drawer
├── Notifications       → AlertStrip · toast · status bar
├── Reporting           → reports-analytics.md
└── AI Platform         → ai-assistant.md · dashboard AI column
```

Reference: `docs/DOMAIN_MAP.md` · `NavigationCatalog.cs`

---

## 3. Strategic Phases → UI Deliverables

| Phase | Name | UI focus | Design system gate |
|-------|------|----------|-------------------|
| **−1** | Domain Architecture Freeze | Nav groups match domains · no orphan pages | Module map stable |
| **0** | Financial Integrity Layer | Posting status · audit trail visible · golden test screens | TabularText · audit chips |
| **1** | P0 Production Safety | RBAC UI · fail-closed · ZATCA status · permission matrix | Settings · compliance-tax |
| **2** | P1 Enterprise Foundation | Workflow approval UI · master data governance forms | expenses · PO pipeline |
| **3** | Saudi Market Production | Country/industry templates in settings · GOSI · construction | compliance · project modules |
| **4** | Enterprise ERP Maturity | API status · integration management panel | settings-multi-company |
| **5** | Data Intelligence | Report drill-down · historical comparison widgets | reports-analytics · charts |
| **6** | AI Financial Intelligence | Decision support cards — **not chatbot-only** | ai-assistant · dashboard intel |
| **7** | Global Platform | Multi-tenant admin · billing UI · HA status | ⏭ SaaS shell — separate design pass |

### Phase 6 — AI UI Rules (from roadmap)

AI must be **Decision Support**, not decorative chat:

- Anomaly alerts on dashboard · reconciliation · VAT
- Next-best-action chips with **document context**
- Quick explain on selected grid row
- Predictive liquidity · AR aging · slow inventory
- **No** purple gradient chat clone · use `AiInsightCard` + rules engine first

### Phase 7 — Global Platform (⏭ external architecture)

Multi-tenant · billing · cloud HA = strategic redesign — luxury shell applies but provisioning UI is out of current Desktop scope. Document as ⏭ in UI specs.

---

## 4. Mandatory Platform Layers (UI touchpoints)

| Layer | UI expression |
|-------|---------------|
| **Accounting Rules Engine** | Inline validation · pre-post dialog · ACC error states |
| **Immutable Audit Ledger** | AuditLogPage · row history · who/when/why |
| **Workflow Engine (P1)** | Approval stepper · status pipeline · delegation UI |
| **Treasury Management** | Bank reconciliation matcher · cash KPIs · liquidity charts |
| **Configuration Engine** | Country/industry template picker in settings |
| **Observability** | Status bar: DB · session · sync · health (future metrics) |

---

## 5. Quality Strategy → UI Verification

Beyond code coverage — test the **system**:

| Test type | UI verification |
|-----------|-----------------|
| **Technical** | Desktop.Tests · audit script · keyboard nav |
| **Financial** | Golden scenarios visible in reports · TB balances · posting flows |
| **Business** | Real workflow paths: invoice → receipt → reconciliation → BS |

Before marking UI ✅: scenario must be **runnable** in Desktop, not mock-only.

---

## 6. Readiness Indicators (platform · not UI alone)

| Milestone | Target readiness |
|-----------|------------------|
| بعد Financial Integrity | 45% |
| بعد P0 | 55% |
| بعد P1 Foundation | 70% |
| بعد Saudi Production | 80% |
| بعد Enterprise ERP | 90% |
| بعد Global Platform | 95% |

Luxury design system closure ≠ platform readiness. UI follows domain maturity.

---

## 7. Council Rules → Agent Rules

| Strategic rule | UI interpretation |
|----------------|-------------------|
| **لا توسع قبل الثقة** | Don't add flashy KPIs/charts without live data source |
| **لا ذكاء قبل البيانات** | AI cards = rules engine first · LLM later |
| **لا عالمية قبل المعمارية** | No multi-tenant UI before domain map stable |
| **لا ميزات قبل صحة المال** | Posting/audit UI before cosmetic modules |
| **Financial Operating System** | Shell + domains + audit — not isolated pretty pages |

---

## 8. Document Stack (read order)

| # | Document | Role |
|---|----------|------|
| 1 | `docs/مهم` | Strategic roadmap v4.0 — authoritative |
| 2 | `design-system/LIFECYCLE-ROADMAP-100.md` | **100 steps** login → close · ZATCA · UI/code status |
| 3 | `design-system/PLATFORM-ROADMAP.md` | This file — phase/UI bridge |
| 4 | `design-system/ECOSYSTEM-VISION.md` | Luxury visual north star |
| 5 | `design-system/MASTER-LUXURY-ECOSYSTEM.md` | Tokens · components |
| 6 | `design-system/MASTER.md` | Pro Max ERP baseline |
| 7 | `design-system/pages/*.md` | Per-screen specs |
| 8 | `.cursor/skills/brqosh-ui-design-system/` | Agent skill |

---

## 9. Current Phase Context (repository)

Per CEEF closure and `docs/OPEN_ITEMS.md`:

| Closed in repo | Phase alignment |
|----------------|-----------------|
| P0 · P1 · Pilot · ERP maturity · CEEF-024 | Phases 0–4 partial UI |
| Luxury ecosystem · design system skill | Visual layer v1 |
| ⏭ ZATCA OTP · SOCPA · MSI · 1M load · SaaS full | External gates — UI shows status only |

**Next strategic UI work:** P1 workflow surfaces · treasury depth · Saudi template UI · AI decision cards · **lifecycle steps 🔜** per `LIFECYCLE-ROADMAP-100.md`.

---

## 10. Operational Lifecycle (100 Steps)

> **Canonical:** [`LIFECYCLE-ROADMAP-100.md`](LIFECYCLE-ROADMAP-100.md)

End-to-end ERP nucleus from **login (1)** to **final freeze (100)**:

| Block | Steps | Design/UI focus |
|-------|-------|-----------------|
| Identity foundation | 1–10 | `login.md` · `settings-multi-company.md` · shell |
| Accounting setup | 11–20 | `general-ledger.md` · COA · posting rules |
| Document lifecycle | 21–30 | Status badges · audit · period lock on all docs |
| Sales | 31–40 | `invoicing-sales.md` |
| **ZATCA e-invoice** | 41–50 | `compliance-tax.md` · XML/QR/CSID UI |
| Purchases | 51–60 | `purchases-suppliers.md` · 3-way match |
| Cash & banks | 61–70 | `bank-cash.md` · reconciliation matcher |
| Journal entries | 71–80 | `general-ledger.md` · double-entry |
| Inventory | 81–90 | `inventory.md` |
| Period/year close | 91–100 | `reports-analytics.md` · close checklist |

**ZATCA technical anchor:** UBL XML · QR TLV · UUID · PIH/ICV · CSID/P-256 signing · clearance/reporting modes. Official specs: [fatoora.zatca.gov.sa](https://fatoora.zatca.gov.sa). Repo: `src/Brqosh.Infrastructure/Zatca/` · `docs/ZATCA_LIVE_CSID.md`.

**Golden paths (must be runnable):**
- Sell → ZATCA → Collect (35, 41–48, 63, 40)
- Procure → Receive → Pay (53–56, 59–60)
- Month close (91–97)
- Year close (98–100)
