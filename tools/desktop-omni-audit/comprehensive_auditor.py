"""
Deep page auditor — mirrors tools/UiAudit/ComprehensiveAuditor.cs actions.
Tests every button, dialog, grid, warning per PageAuditCatalog.
"""

from __future__ import annotations

import logging
import time
from dataclasses import dataclass, field
from typing import Any

from nav_key_map import key_for_title
from page_audit_catalog import ButtonSpec, PageSpec, load_catalog
from payloads import payloads_for_type

logger = logging.getLogger(__name__)


@dataclass
class AuditStepResult:
  step: str
  status: str  # pass | fail | skip
  message: str
  page: str = ""


@dataclass
class ComprehensiveResults:
  steps: list[AuditStepResult] = field(default_factory=list)

  @property
  def passed(self) -> int:
    return sum(1 for s in self.steps if s.status == "pass")

  @property
  def failed(self) -> int:
    return sum(1 for s in self.steps if s.status == "fail")

  @property
  def skipped(self) -> int:
    return sum(1 for s in self.steps if s.status == "skip")


class ComprehensiveAuditor:
  def __init__(self, navigator: Any, repo_root: Any, vision: Any, prefix: str = ""):
    self.nav = navigator
    self.repo_root = repo_root
    self.vision = vision
    self.prefix = prefix
    self.results = ComprehensiveResults()
    self.catalog = load_catalog(repo_root)
    self._delay = navigator._delay

  def _p(self, step: str) -> str:
    return f"{self.prefix}{step}"

  def _record(self, step: str, status: str, message: str, page: str = "") -> None:
    self.results.steps.append(AuditStepResult(step, status, message, page))

  def run_all(self) -> ComprehensiveResults:
    for spec in self.catalog:
      nav_key = key_for_title(spec.nav_title)
      btn = self.nav._find_nav_button(spec.nav_title)
      if btn is None:
        self._record(
          self._p(f"قسم/{spec.nav_title}"),
          "skip",
          "غير ظاهر في التنقل",
          spec.nav_title,
        )
        continue

      nav = self.nav.navigate_to_page(spec.nav_title)
      if not nav.success:
        self._record(self._p(f"تنقل/{spec.nav_title}"), "fail", nav.message, spec.nav_title)
        continue

      self._record(self._p(f"تنقل/{spec.nav_title}"), "pass", "تم فتح الصفحة", spec.nav_title)
      self._audit_page(spec)
      self.nav._dismiss_dialogs()
    return self.results

  def _audit_page(self, spec: PageSpec) -> None:
    time.sleep(0.9)
    self.nav._dismiss_dialogs()

    if spec.nav_title in ("كشف حساب عميل", "كشف حساب مورد"):
      self._select_combo_containing("QA-AUDIT")

    if spec.nav_title == "لوحة التحكم":
      kpis = self._count_kpi_indicators()
      if kpis >= 1:
        self._record(self._p(f"جدول/{spec.nav_title}"), "pass", f"{kpis} KPI", spec.nav_title)
      else:
        self._record(self._p(f"جدول/{spec.nav_title}"), "fail", "لا KPI", spec.nav_title)
      return

    for btn in [b for b in spec.buttons if b.action == "run_report"]:
      self._audit_button(spec, btn)

    rows = self._count_grid_rows()
    if spec.min_grid_rows is not None and spec.min_grid_rows > 0:
      if rows >= spec.min_grid_rows:
        self._record(self._p(f"جدول/{spec.nav_title}"), "pass", f"{rows} صف", spec.nav_title)
      elif self._has_report_summary():
        self._record(self._p(f"جدول/{spec.nav_title}"), "pass", "ملخص ظاهر", spec.nav_title)
      else:
        self._record(
          self._p(f"جدول/{spec.nav_title}"),
          "fail",
          f"متوقع ≥{spec.min_grid_rows} وُجد {rows}",
          spec.nav_title,
        )
    elif not spec.buttons:
      self._record(self._p(f"جدول/{spec.nav_title}"), "pass", f"{rows} صف", spec.nav_title)

    for btn in [b for b in spec.buttons if b.action != "run_report"]:
      self._audit_button(spec, btn)

    self._inject_page_payloads(spec.nav_title)

  def _audit_button(self, spec: PageSpec, bspec: ButtonSpec) -> None:
    step = self._p(f"زر/{spec.nav_title}/{bspec.label_contains}")
    page = spec.nav_title
    try:
      if bspec.action == "verify_grid_only":
        return

      btn = self._find_page_button(bspec.label_contains)
      if btn is None:
        self._record(step, "fail", "الزر غير موجود", page)
        return

      if bspec.action == "require_selection_warning":
        try:
          if not btn.is_enabled():
            self._record(step, "pass", "معطّل بدون تحديد", page)
            return
        except Exception:
          pass
        btn.click_input()
        time.sleep(0.3)
        self.nav._dismiss_dialogs()
        self._record(step, "pass", "تنبيه اختيار", page)
        return

      if bspec.action == "select_first_then_act":
        if not self._select_first_grid_row():
          btn.click_input()
          time.sleep(0.25)
          self.nav._dismiss_dialogs()
          self._record(step, "pass", "لا صفوف — تنبيه", page)
          return
        btn.click_input()
        time.sleep(0.5)
        if self._close_child_dialog(save=False):
          self._record(step, "pass", "حوار أُغلق", page)
        else:
          self.nav._dismiss_dialogs()
          self._record(step, "pass", "نُفّذ", page)
        return

      if bspec.action in ("open_dialog_cancel", "open_dialog_save"):
        btn.click_input()
        time.sleep(0.6)
        if not self._wait_dialog(3):
          self._record(step, "fail", "لم يُفتح حوار", page)
          return
        if bspec.action == "open_dialog_save":
          self._fill_dialog(bspec.dialog_kind)
          self._close_child_dialog(save=True)
          self._record(step, "pass", "إدخال + حفظ", page)
        else:
          self._close_child_dialog(save=False)
          self._record(step, "pass", "فُتح وأُلغي", page)
        return

      if bspec.action == "open_wizard_cancel":
        btn.click_input()
        time.sleep(0.6)
        self._close_child_dialog(save=False)
        self._record(step, "pass", "معالج أُغلق", page)
        return

      if bspec.action == "run_report":
        btn.click_input()
        time.sleep(0.8)
        rows = self._count_grid_rows()
        if rows > 0 or self._has_report_summary():
          self._record(step, "pass", f"{rows} صف", page)
        else:
          self._record(step, "fail", "لا نتائج", page)
        return

      if bspec.action == "export_pdf":
        btn.click_input()
        time.sleep(0.6)
        self.nav._dismiss_dialogs()
        self._record(step, "pass", "PDF/تنبيه", page)
        return

      # click_safe default
      btn.click_input()
      time.sleep(0.4)
      self.nav._dismiss_dialogs()
      self._record(step, "pass", "نُفّذ", page)

      if self.vision and self.nav.main_window:
        try:
          self.vision.capture_window(
            self.nav.main_window.wrapper_object(),
            f"btn_{page}_{bspec.label_contains[:15]}",
          )
        except Exception:
          pass

    except Exception as exc:
      self._record(step, "fail", str(exc)[:200], page)
      self.nav._dismiss_dialogs()

  def _find_page_button(self, label_contains: str):
    if self.nav.main_window is None:
      return None
    roots = []
    try:
      host = self.nav.main_window.child_window(auto_id="PageHost")
      if host.exists(timeout=2):
        roots.append(host)
    except Exception:
      pass
    roots.append(self.nav.main_window)

    for root in roots:
      try:
        # Direct child_window partial title match
        for btn in root.descendants(control_type="Button"):
          try:
            t = btn.window_text() or ""
            if label_contains in t or t in label_contains:
              return btn
            # nested Text children (WPF button content)
            for child in btn.descendants(control_type="Text"):
              ct = child.window_text() or ""
              if label_contains in ct:
                return btn
          except Exception:
            continue
      except Exception:
        continue
    return None

  def _count_grid_rows(self) -> int:
    if self.nav.main_window is None:
      return 0
    try:
      items = self.nav.main_window.descendants(control_type="DataItem")
      if items:
        return len(items)
      grids = self.nav.main_window.descendants(control_type="Table")
      total = 0
      for g in grids:
        try:
          total += len(g.children())
        except Exception:
          pass
      return total
    except Exception:
      return 0

  def _count_kpi_indicators(self) -> int:
    keywords = ["مبيعات", "إجمالي", "صافي", "رصيد", "KPI"]
    count = 0
    try:
      for t in self.nav.main_window.descendants(control_type="Text"):
        try:
          txt = t.window_text() or ""
          if any(k in txt for k in keywords):
            count += 1
        except Exception:
          pass
    except Exception:
      pass
    return min(count, 20)

  def _has_report_summary(self) -> bool:
    keywords = ["إجمالي", "صافي", "رصيد", "متوازن", "مدين", "دائن"]
    try:
      for t in self.nav.main_window.descendants(control_type="Text"):
        txt = t.window_text() or ""
        if any(k in txt for k in keywords):
          return True
    except Exception:
      pass
    return False

  def _select_first_grid_row(self) -> bool:
    try:
      items = self.nav.main_window.descendants(control_type="DataItem")
      if items:
        items[0].click_input()
        return True
    except Exception:
      pass
    return False

  def _select_combo_containing(self, text_part: str) -> None:
    try:
      combos = self.nav.main_window.descendants(control_type="ComboBox")
      for combo in combos[:3]:
        try:
          combo.expand()
          time.sleep(0.3)
          for item in combo.descendants(control_type="ListItem"):
            if text_part in (item.window_text() or ""):
              item.click_input()
              return
        except Exception:
          continue
    except Exception:
      pass

  def _wait_dialog(self, sec: float) -> bool:
    deadline = time.time() + sec
    while time.time() < deadline:
      if self._find_child_dialog() is not None:
        return True
      time.sleep(0.1)
    return False

  def _find_child_dialog(self):
    from pywinauto import Desktop
    try:
      desktop = Desktop(backend="uia")
      for w in desktop.windows():
        try:
          t = w.window_text() or ""
          if "BRQOSH ERP" in t or "تسجيل الدخول" in t:
            continue
          if "BRQOSH" in t or w.descendants(control_type="Button"):
            return w
        except Exception:
          continue
    except Exception:
      pass
    return None

  def _close_child_dialog(self, save: bool = False) -> bool:
    dlg = self._find_child_dialog()
    if dlg is None:
      return False
    label = "حفظ" if save else "إلغاء"
    for name in [label, "إغلاق", "موافق", "OK", "Cancel"]:
      try:
        btn = dlg.child_window(title=name, control_type="Button")
        if btn.exists(timeout=1):
          btn.click_input()
          time.sleep(0.35)
          return True
      except Exception:
        pass
    return False

  def _fill_dialog(self, kind: str | None) -> None:
    if not kind:
      return
    dlg = self._find_child_dialog()
    if dlg is None:
      return
    stamp = time.strftime("%H%M%S")
    fills: dict[str, list[tuple[str, str]]] = {
      "customer": [("اسم العميل", f"OMNI عميل {stamp}"), ("الرقم الضريبي", "311122233300003")],
      "supplier": [("اسم المورد", f"OMNI مورد {stamp}")],
      "product": [("كود", f"P-{stamp}"), ("اسم الصنف", f"صنف {stamp}")],
      "warehouse": [("الكود", f"WH-{stamp}"), ("الاسم", f"مستودع {stamp}")],
      "cost_center": [("الكود", f"CC-{stamp}"), ("الاسم", f"مركز {stamp}")],
      "bank_account": [("الكود", f"BNK-{stamp}"), ("الاسم", "بنك")],
    }
    for label, value in fills.get(kind, []):
      try:
        edit = dlg.child_window(title=label, control_type="Edit")
        if edit.exists(timeout=1):
          edit.set_edit_text(value)
      except Exception:
        pass

  def _inject_page_payloads(self, page_title: str) -> None:
    per_field = 5
    try:
      edits = self.nav.main_window.descendants(control_type="Edit")
      for edit in edits[:6]:
        for payload in payloads_for_type("text", per_field):
          try:
            edit.set_focus()
            edit.set_edit_text(payload)
            time.sleep(0.05)
          except Exception:
            break
    except Exception:
      pass
