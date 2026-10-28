# QuestPDF licence review (Phase 7) — 2026-10-28

**Decision: keep QuestPDF under the free Community License. No purchase, no replacement needed today.**

Source: QuestPDF LICENSE.md / License Selection Guide v3.0 (effective 6 July 2026),
https://github.com/QuestPDF/QuestPDF/blob/main/LICENSE.md. Pinned package: `QuestPDF 2025.7.0`
(the licence text shipped with a release governs that release).

| Question | Licence text | BRQOSH |
|---|---|---|
| Revenue threshold | Free for individuals and organisations with annual gross revenue **under USD 1,000,000** (consolidated, previous completed fiscal year; first year = good-faith estimate) | Marwan: no revenue yet → eligible |
| Commercial use | "All eligible users may use QuestPDF for commercial purposes" | Selling BRQOSH is allowed |
| Distribution | May "redistribute the Software only as a compiled component of your own Derivative Applications"; keep QuestPDF's proprietary notices; no source redistribution | We ship the NuGet DLLs inside the app; `THIRD-PARTY-NOTICES.txt` already lists QuestPDF |
| End customers | Category 7 (transitive users whose code does not call QuestPDF) and "End users … do not need a separate licence" | Customers need nothing |
| Not eligible | Public-sector entities and publicly traded companies **as the licensee** (the developer/owner of the app) | Not Marwan; customers are end users |
| Restrictions | No general-purpose PDF library/SDK competing with QuestPDF; no implied endorsement | BRQOSH is an ERP; PDF is one feature |
| Code setting | `QuestPDF.Settings.License = LicenseType.Community` | Already set in every generator |

**Trigger to revisit:** if BRQOSH's owner exceeds USD 1,000,000 gross revenue in a fiscal year, there are
90 days (from that fiscal year's end) to buy a paid licence or stop using QuestPDF. Under the "nothing
purchased" rule the fallback is replacing it with an MIT-licensed library (PDFsharp/MigraDoc) behind the
existing `*PdfGenerator` classes — not needed now.
