"""Business logic oracles — debit=credit, tabular amounts."""

from __future__ import annotations

from dataclasses import dataclass, field


@dataclass
class OracleFinding:
  rule: str
  status: str
  detail: str


@dataclass
class OracleResult:
  findings: list[OracleFinding] = field(default_factory=list)


def infer_accounting_oracles() -> OracleResult:
  """Static oracles from BRQOSH domain — validated by Golden tests in repo."""
  res = OracleResult()
  res.findings.append(OracleFinding(
    "debit_equals_credit", "verified_by_tests",
    "Golden tests in Brqosh.Application.Tests — agent defers to dotnet test",
  ))
  res.findings.append(OracleFinding(
    "decimal_precision", "monitor",
    "NUMERIC(18,2) in SchemaSqlGuard — UI must not round before post",
  ))
  res.findings.append(OracleFinding(
    "branch_id_scope", "monitor",
    "Every query must filter CompanyId/BranchId — grep in code review",
  ))
  return res
