"""
Malicious and edge-case payload generators for BRQOSH desktop audit.
Each category returns at least 10 variations per input type.
"""

from __future__ import annotations

import random
import string
from typing import Callable


def _repeat(s: str, n: int) -> str:
    return s * n


# --- Core generators ---

def sql_injection_payloads() -> list[str]:
    return [
        "' OR '1'='1",
        "'; DROP TABLE Users;--",
        "1; SELECT * FROM JournalEntries--",
        "admin'--",
        "' UNION SELECT NULL,NULL,NULL--",
        "1' AND SLEEP(5)--",
        "' OR 1=1#",
        "') OR ('1'='1",
        "'; EXEC xp_cmdshell('dir');--",
        "1; UPDATE Accounts SET Balance=999999 WHERE Id=1--",
    ]


def xss_payloads() -> list[str]:
    return [
        "<script>alert('xss')</script>",
        "<img src=x onerror=alert(1)>",
        "javascript:alert(document.cookie)",
        "<svg onload=alert(1)>",
        "'\"><script>alert(String.fromCharCode(88,83,83))</script>",
        "<iframe src=\"javascript:alert(1)\">",
        "<body onload=alert(1)>",
        "{{constructor.constructor('alert(1)')()}}",
        "<details open ontoggle=alert(1)>",
        "&#60;script&#62;alert(1)&#60;/script&#62;",
    ]


def unicode_rtl_payloads() -> list[str]:
    return [
        "مرحبا\u202E\u202Dtest",
        "\u200F\u200E\u061Chidden",
        "اسم\u202B\u202Cعميل",
        "A\u202E123\u202C",
        "\uFEFF\u200B\u200C\u200D",
        "‏test‏",
        "‮reversed‬",
        "عربي\u2066English\u2069",
        "\u00A0\u1680\u2000\u2001",
        "ZATCA\u202E\u202D\u202C",
    ]


def huge_blob_payloads() -> list[str]:
    return [
        _repeat("A", 10_000),
        _repeat("مرحبا ", 5_000),
        _repeat("x", 50_000),
        _repeat("\n", 1_000),
        _repeat("0", 20_000),
        _repeat("SQL", 8_000),
        _repeat("🙂", 2_000),  # emoji stress
        _repeat("a" * 100 + " ", 500),
        _repeat("\\n", 3_000),
        _repeat("BRQOSH ", 4_000),
    ]


def numeric_edge_payloads() -> list[str]:
    return [
        "0",
        "-1",
        "-0.01",
        "999999999999999.99",
        "0.0000001",
        "NaN",
        "Infinity",
        "1e308",
        "١٢٣٤٥",  # Arabic-Indic digits
        "",
    ]


def empty_and_whitespace_payloads() -> list[str]:
    return [
        "",
        " ",
        "   ",
        "\t",
        "\n",
        "\r\n",
        "\u00A0",
        " \t \n ",
        "\u200B",
        "\uFEFF",
    ]


def path_traversal_payloads() -> list[str]:
    return [
        "..\\..\\Windows\\System32\\config\\SAM",
        "../../../etc/passwd",
        "C:\\Windows\\win.ini",
        "%USERPROFILE%\\Desktop\\secret.txt",
        "file:///etc/passwd",
        "\\\\server\\share\\file",
        "..%2F..%2Fweb.config",
        "....//....//etc/passwd",
        "C:/Users/Public/",
        "\\\\?\\C:\\",
    ]


def logical_paradox_payloads() -> list[str]:
    return [
        "true AND false",
        "null",
        "undefined",
        "[]",
        "{}",
        "0/0",
        "MAX+1",
        "MIN-1",
        "1900-02-30",
        "2026-13-45",
    ]


def username_payloads() -> list[str]:
    base = [
        "admin",
        "marwan",
        "user@domain.com",
        "user name with spaces",
        "user\twith\ttabs",
    ]
    return (base + sql_injection_payloads() + unicode_rtl_payloads())[:12]


def password_payloads() -> list[str]:
    base = [
        "UiAudit9!",
        "password",
        "P@ssw0rd!",
        " " * 8,
        _repeat("x", 128),
    ]
    return (base + xss_payloads() + empty_and_whitespace_payloads())[:12]


def text_field_payloads() -> list[str]:
    pool: list[str] = []
    for fn in (
        sql_injection_payloads,
        xss_payloads,
        unicode_rtl_payloads,
        empty_and_whitespace_payloads,
        logical_paradox_payloads,
    ):
        pool.extend(fn())
    random.shuffle(pool)
    return pool[:12]


def amount_payloads() -> list[str]:
    return (numeric_edge_payloads() + sql_injection_payloads() + unicode_rtl_payloads())[:12]


def description_payloads() -> list[str]:
    return (huge_blob_payloads()[:5] + xss_payloads()[:5] + path_traversal_payloads()[:2])[:12]


def email_payloads() -> list[str]:
    return [
        "not-an-email",
        "@missing-local.com",
        "user@",
        "user@.com",
        "user@domain",
        "a@" + _repeat("b", 200) + ".com",
        "user+tag@domain.co.sa",
        "<script>@x.com",
        "user@domain.com; DROP TABLE--",
        "مرحبا@domain.com",
        "",
        "user@domain.com\u202E",
    ]


def phone_payloads() -> list[str]:
    return [
        "0501234567",
        "+966501234567",
        "abc",
        "0" * 50,
        "-1",
        "٠٥٠١٢٣٤٥٦٧",
        "050-123-4567",
        "",
        "05012345678",
        "123",
        "<script>050</script>",
        "0501234567';--",
    ]


# --- Registry ---

PAYLOAD_REGISTRY: dict[str, Callable[[], list[str]]] = {
    "username": username_payloads,
    "password": password_payloads,
    "text": text_field_payloads,
    "amount": amount_payloads,
    "description": description_payloads,
    "email": email_payloads,
    "phone": phone_payloads,
    "sql": sql_injection_payloads,
    "xss": xss_payloads,
    "numeric": numeric_edge_payloads,
    "unicode_rtl": unicode_rtl_payloads,
    "huge": huge_blob_payloads,
    "empty": empty_and_whitespace_payloads,
    "path": path_traversal_payloads,
    "paradox": logical_paradox_payloads,
}


def infer_payload_type(control_name: str, automation_name: str, xaml_tag: str) -> str:
    """Heuristic mapping from control metadata to payload category."""
    hay = f"{control_name} {automation_name}".lower()
    if xaml_tag.lower() == "passwordbox":
        return "password"
    if any(k in hay for k in ("email", "بريد", "mail")):
        return "email"
    if any(k in hay for k in ("phone", "جوال", "هاتف", "mobile")):
        return "phone"
    if any(k in hay for k in ("amount", "مبلغ", "قيمة", "سعر", "balance", "رصيد", "debit", "credit")):
        return "amount"
    if any(k in hay for k in ("username", "مستخدم", "اسم المستخدم")):
        return "username"
    if any(k in hay for k in ("password", "كلمة المرور", "مرور")):
        return "password"
    if any(k in hay for k in ("note", "وصف", "description", "ملاحظ", "comment")):
        return "description"
    return "text"


def payloads_for_type(payload_type: str, count: int = 10) -> list[str]:
    fn = PAYLOAD_REGISTRY.get(payload_type, text_field_payloads)
    items = fn()
    if len(items) >= count:
        return items[:count]
    # pad with random alphanumeric if needed
    while len(items) < count:
        items.append("".join(random.choices(string.ascii_letters + string.digits, k=16)))
    return items[:count]
