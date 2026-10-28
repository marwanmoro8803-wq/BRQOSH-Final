# 1.0.34 — EULA acceptance write failure

The EULA dialog previously let storage exceptions escape from its Accept event handler. It also always overwrote the legacy eula-accepted.json file, including protected files left by an earlier installation.

Changes:
- New consent records are stored in eula-acceptance/<text SHA256>.json under the existing application data root. No data-root relocation or ACL changes.
- Legacy acceptance remains readable. Text changes still require consent.
- Fresh-install archival preserves both legacy records and the new consent directory.
- An IO/access failure keeps the EULA dialog open, explains write permissions in the selected language, and does not mark acceptance successful.

Validation:
- Full Release solution build succeeded: 0 errors, 21 warnings.
- EULA tests: 8 passed, including a read-only legacy file, legacy compatibility, changed agreement and an unreadable new record.
- Dialog localization and I18n coverage: 6 passed.
- No live GUI acceptance: native UI access is unavailable in this session.
- A whole data root that Windows refuses to write still requires its actual permission problem to be repaired. This change neither grants permissions nor bypasses consent.

Packaging results: see C:/workspace/brqosh-verify/eula-package.log. Review status remains pending live startup, full regression and installer acceptance.

Portable archive verified: 1537 file hashes match the final payload. SHA256 D3A04D65E2D62B1572C8C49DE8C44996E4F7078CD655DEC6072B51A53D6AACD4. Available as dist/release-1.0.34/BRQOSH-1.0.34-review-portable.zip. GUI not verified; review only.
MSI packaging was interrupted after the portable payload was complete; no validated 1.0.34 MSI is supplied.
