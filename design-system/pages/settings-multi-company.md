# Settings & Multi-Company — Luxury Spec

**Feel:** Enterprise control plane · Permissions depth · Integration hub

## Layout

```
┌─────────────────────────────────────────────────────────────┐
│ Settings nav (vertical tabs)                                  │
├──────────────────────────┬──────────────────────────────────┤
│ Section content          │ Context help / preview panel      │
│ Forms · matrices · lists │ Permission impact summary         │
└──────────────────────────┴──────────────────────────────────┘
```

## Sections

| Section | Content |
|---------|---------|
| Company | Legal name · VAT · logo · fiscal year · **business profile (read-only)** |
| **Organizations** 🔜 G-69 | List · create · switch · archive |
| Branches | Branch list · HQ flag · session default |
| Users & Roles | User grid · role assignment |
| Permissions | Matrix: module × action × role |
| Branding | Theme accent override (future) · template control |
| Language | Default locale · numeral preference |
| Security | Password policy · session idle · 2FA (future) |
| Integrations | Bank · ZATCA · export connectors |
| Audit | Link to `AuditLogPage` |

## Permissions Matrix

- Rows: modules · Columns: View/Create/Edit/Post/Delete
- Checkboxes with indeterminate group headers
- `TabularText` for role counts

## WPF Mapping

`CompanySettingsPage` · `UsersPage` · `BranchesPage` · `CompanyGroupsPage` · `AuditLogPage`

## Bilingual

- AR: «الإعدادات» · «الصلاحيات» · EN: «Settings» · «Permissions»
