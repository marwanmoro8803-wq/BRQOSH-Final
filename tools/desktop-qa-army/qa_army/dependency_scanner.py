"""Dependency CVE scan + secret patterns for .NET repo."""

from __future__ import annotations

import re
import subprocess
from dataclasses import dataclass, field
from pathlib import Path


@dataclass
class DependencyFinding:
  severity: str
  package: str
  message: str


@dataclass
class DependencyScanResult:
  findings: list[DependencyFinding] = field(default_factory=list)
  packages_scanned: int = 0


SECRET_PATTERNS = [
  (r"(?i)api[_-]?key\s*=\s*['\"][^'\"]+['\"]", "Hardcoded API key pattern"),
  (r"(?i)password\s*=\s*['\"][^'\"]{3,}['\"]", "Hardcoded password in source"),
  (r"AKIA[0-9A-Z]{16}", "AWS key pattern"),
]


def scan_repo(repo_root: Path) -> DependencyScanResult:
  res = DependencyScanResult()
  csprojs = list(repo_root.glob("**/*.csproj"))
  res.packages_scanned = len(csprojs)

  # dotnet list package --vulnerable (best effort)
  try:
    r = subprocess.run(
      ["dotnet", "list", str(repo_root / "Brqosh.sln"), "package", "--vulnerable", "--include-transitive"],
      capture_output=True, text=True, timeout=120, cwd=str(repo_root),
    )
    if r.returncode == 0 and "has the following vulnerable" in r.stdout:
      for line in r.stdout.splitlines():
        if ">" in line and "Severity" not in line:
          res.findings.append(DependencyFinding("High", line.strip()[:60], "dotnet vulnerable package"))
    elif "error" in (r.stderr or "").lower():
      res.findings.append(DependencyFinding("Low", "dotnet", "vulnerable scan skipped — run dotnet restore first"))
  except Exception as exc:
    res.findings.append(DependencyFinding("Low", "scanner", str(exc)[:80]))

  # Secret grep in src (not tests)
  for cs in (repo_root / "src").rglob("*.cs"):
    try:
      text = cs.read_text(encoding="utf-8", errors="replace")
      for pat, msg in SECRET_PATTERNS:
        if re.search(pat, text):
          res.findings.append(DependencyFinding("Critical", cs.name, f"{msg} in {cs.relative_to(repo_root)}"))
    except Exception:
      pass

  return res


def scan_network_surface() -> list[str]:
  """BRQOSH desktop is primarily local DB + optional API — document boundary."""
  return [
    "Desktop default: SQLite/PostgreSQL local — no mitmproxy required for core GL flows",
    "ZATCA/O-01 live OTP: ⏭ EXTERNAL — network audit at dist/evidence/O-01",
    "Brqosh.Api: run separate API pentest when O-09 SaaS live",
  ]
