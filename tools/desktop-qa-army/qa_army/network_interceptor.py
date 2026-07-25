"""Network interception — mitmproxy hook for API-enabled runs (optional)."""

from __future__ import annotations

from dataclasses import dataclass, field


@dataclass
class NetworkNote:
  severity: str
  message: str


@dataclass
class NetworkScanResult:
  notes: list[NetworkNote] = field(default_factory=list)
  mitm_available: bool = False


def scan_local_desktop_boundary() -> NetworkScanResult:
  """BRQOSH Desktop core flows use local DB; API pentest is separate."""
  res = NetworkScanResult()
  res.notes.append(NetworkNote("Info", "Core ERP: SQLite/PG local — no HTTP required for GL posting"))
  res.notes.append(NetworkNote("Info", "Brqosh.Api: enable mitmproxy when testing SaaS (O-09)"))
  res.notes.append(NetworkNote("Info", "ZATCA live OTP: dist/evidence/O-01 — EXTERNAL"))
  try:
    import mitmproxy  # noqa: F401
    res.mitm_available = True
  except ImportError:
    res.notes.append(NetworkNote("Low", "mitmproxy not installed — pip install mitmproxy for API runs"))
  return res
