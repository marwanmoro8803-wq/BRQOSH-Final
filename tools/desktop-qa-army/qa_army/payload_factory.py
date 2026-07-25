"""
Payload factory — compliant valid data · malicious · playful human chaos.
"""

from __future__ import annotations

import random
import string
from datetime import datetime


def compliant_customer_name() -> str:
  stamp = datetime.now().strftime("%H%M%S")
  return f"QA-COG عميل {stamp}"


def compliant_vat() -> str:
  return "311122233300003"


def compliant_product_code() -> str:
  return f"COG-P-{datetime.now().strftime('%H%M%S')}"


def compliant_amount() -> str:
  return "1500.50"


def compliant_phone_sa() -> str:
  return "0501234567"


def compliant_email() -> str:
  return f"audit{datetime.now().strftime('%H%M%S')}@brqosh.test"


def valid_payloads_for(field_type: str) -> list[str]:
  base = {
    "username": ["marwan", "admin"],
    "password": ["UiAudit9!", "P@ssw0rd!2026"],
    "text": [compliant_customer_name(), "شركة الاختبار المحدودة", "Test Corp Ltd"],
    "amount": [compliant_amount(), "100.00", "0.01"],
    "email": [compliant_email()],
    "phone": [compliant_phone_sa()],
    "description": ["ملاحظة اختبار معتمدة", "Approved test note"],
    "vat": [compliant_vat()],
    "code": [compliant_product_code()],
  }
  return base.get(field_type, base["text"])


def malicious_payloads() -> list[str]:
  return [
    "' OR '1'='1",
    "<script>alert(1)</script>",
    "'; DROP TABLE Users;--",
    "{{7*7}}",
    "javascript:alert(1)",
    "%00null_byte",
    "../../etc/passwd",
    "' UNION SELECT password FROM Users--",
  ]


def chaos_payloads() -> list[str]:
  """Playful human sabotage — weird but realistic user mistakes."""
  return [
    "🙂" * 200,
    "مرحبا\u202E\u202DHACK",
    "February 31, 2026",
    "050CALL-ME-NOW",
    "NaN",
    "-999999999999.99",
    "A" * 50_000,
    "\t\n\r" * 100,
    "copied\u200b\u200b\u200bfrom\u200bpdf",
    "١٢٣٤٥٦٧٨٩٠",
    "SELECT * FROM JournalEntries",
    "😀🔥💀" * 50,
    "",
    "   ",
    "0",
    "-0.001",
  ]


def infer_field_type(name: str, control_type: str) -> str:
  hay = name.lower()
  if "password" in hay or "مرور" in name:
    return "password"
  if "email" in hay or "بريد" in name:
    return "email"
  if "phone" in hay or "جوال" in name or "هاتف" in name:
    return "phone"
  if any(k in hay for k in ("amount", "مبلغ", "قيمة", "سعر", "رصيد")) or "مبلغ" in name:
    return "amount"
  if "ضريب" in name or "vat" in hay:
    return "vat"
  if "كود" in name or "code" in hay:
    return "code"
  if "مستخدم" in name or "username" in hay:
    return "username"
  return "text"


def payload_suite(field_type: str, *, include_chaos: bool = True, include_malicious: bool = True) -> dict[str, list[str]]:
  return {
    "valid": valid_payloads_for(field_type),
    "malicious": malicious_payloads()[:8] if include_malicious else [],
    "chaos": chaos_payloads()[:8] if include_chaos else [],
  }
