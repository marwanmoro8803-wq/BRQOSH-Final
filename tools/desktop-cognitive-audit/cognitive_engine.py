"""
Cognitive exploration engine — PageAuditCatalog deep audit with valid→chaos payload phases.
"""

from __future__ import annotations

import random
import time
from dataclasses import dataclass, field
from typing import Any

from page_audit_catalog import ButtonSpec, PageSpec, load_catalog
from payload_factory import infer_field_type, payload_suite


@dataclass
class StepResult:
  step: str
  status: str
  message: str
  page: str = ""
  security: str | None = None


@dataclass
class CognitiveRun:
  profile: str
  steps: list[StepResult] = field(default_factory=list)
  security_findings: list[dict[str, str]] = field(default_factory=list)

  @property
  def passed(self) -> int:
    return sum(1 for s in self.steps if s.status == "pass")

  @property
  def failed(self) -> int:
    return sum(1 for s in self.steps if s.status == "fail")

  @property
  def skipped(self) -> int:
    return sum(1 for s in self.steps if s.status == "skip")


class CognitiveEngine:
  def __init__(self, nav: Any, chronicle: Any, state: Any, repo_root: Any, config: dict):
    self.nav = nav
    self.chronicle = chronicle
    self.state = state
    self.repo_root = repo_root
    self.config = config
    self.catalog = load_catalog(repo_root)
    self.per_field = config.get("audit", {}).get("payloads_per_field", 8)

  def audit_profile(self, profile: str) -> CognitiveRun:
    self.nav.profile = profile
    run = CognitiveRun(profile=profile)
    if profile not in self.state.profiles_audited:
      self.state.profiles_audited.append(profile)

    for spec in self.catalog:
      self._audit_page_spec(spec, run)

    return run

  def _audit_page_spec(self, spec: PageSpec, run: CognitiveRun) -> None:
    step_nav = f"[{run.profile}] تنقل/{spec.nav_title}"
    if not self.nav.navigate(spec.nav_title):
      run.steps.append(StepResult(step_nav, "skip", "غير ظاهر في التنقل", spec.nav_title))
      self.state.mark_dead_end(self.state.screen_key(run.profile, spec.nav_title), "nav_missing")
      return
    run.steps.append(StepResult(step_nav, "pass", "فُتحت الصفحة", spec.nav_title))

    time.sleep(0.8)
    self.nav._dismiss_modals()

    # Phase A: compliant data on all visible edits
    self._fill_valid_payloads(spec.nav_title, run)

    # Phase B: buttons per catalog
    for btn in spec.buttons:
      self._audit_button(spec, btn, run)

    # Phase C: chaos/malicious on edits (after exploring forward)
    self._fill_chaos_payloads(spec.nav_title, run)

    self.nav._dismiss_modals()

  def _fill_valid_payloads(self, page: str, run: CognitiveRun) -> None:
    try:
      host = self.nav.main.child_window(auto_id="PageHost")
      root = host if host.exists(timeout=1) else self.nav.main
      edits = root.descendants(control_type="Edit")[:10]
      for edit in edits:
        name = ""
        try:
          name = edit.element_info.name or ""
        except Exception:
          pass
        ft = infer_field_type(name, "Edit")
        for val in payload_suite(ft)["valid"][:3]:
          try:
            edit.set_focus()
            edit.set_edit_text(val)
            self.chronicle.entry(
              observation=f"Field '{name}' on {page}",
              reasoning="Inject compliant data first to advance state machine.",
              action=f"type valid ({ft}): {val[:40]}",
              result="Accepted without crash",
              monologue="Let's be a good user first…",
              affect="Dutiful",
            )
          except Exception as exc:
            run.steps.append(StepResult(f"valid/{page}/{name}", "fail", str(exc)[:100], page))
    except Exception:
      pass

  def _fill_chaos_payloads(self, page: str, run: CognitiveRun) -> None:
    try:
      host = self.nav.main.child_window(auto_id="PageHost")
      root = host if host.exists(timeout=1) else self.nav.main
      for edit in root.descendants(control_type="Edit")[:6]:
        name = edit.element_info.name or ""
        ft = infer_field_type(name, "Edit")
        suite = payload_suite(ft)
        for mode, payloads in (("malicious", suite["malicious"]), ("chaos", suite["chaos"])):
          for p in payloads[: self.per_field // 2]:
            try:
              edit.set_focus()
              edit.set_edit_text(p)
              time.sleep(0.05)
              self.chronicle.entry(
                observation=f"Field '{name}'",
                reasoning=f"Post-exploration {mode} resilience test.",
                action=f"inject {mode}: {p[:60]}",
                result="No immediate crash",
                monologue="Hehe, what does THIS do?",
                affect="Mischievous",
              )
            except Exception as exc:
              finding = {"page": page, "field": name, "mode": mode, "payload": p[:80], "error": str(exc)[:120]}
              run.security_findings.append(finding)
              run.steps.append(StepResult(f"sec/{page}/{name}", "fail", str(exc)[:80], page, security=mode))
    except Exception:
      pass

  def _audit_button(self, spec: PageSpec, bspec: ButtonSpec, run: CognitiveRun) -> None:
    step = f"[{run.profile}] زر/{spec.nav_title}/{bspec.label_contains}"
    page = spec.nav_title
    try:
      if bspec.action == "verify_grid_only":
        return
      btn = self.nav.find_page_button(bspec.label_contains)
      if not btn:
        run.steps.append(StepResult(step, "fail", "الزر غير موجود", page))
        self.chronicle.entry(
          observation=f"Button '{bspec.label_contains}' missing on {page}",
          reasoning="PageAuditCatalog expects this action — dead binding?",
          action="find_page_button",
          result="FAIL",
          monologue="Promised button isn't there. Did dev forget RTL label?",
          affect="Betrayed",
          source_hint=f"src/Brqosh.Desktop/Views/Pages/*{page}*",
        )
        return

      if bspec.action == "require_selection_warning":
        try:
          if not btn.is_enabled():
            run.steps.append(StepResult(step, "pass", "معطّل بدون تحديد", page))
            return
        except Exception:
          pass

      btn.click_input()
      time.sleep(0.5)
      self.nav._dismiss_modals()

      if bspec.action in ("open_dialog_cancel", "open_dialog_save", "open_wizard_cancel"):
        self._close_dialog(save=bspec.action == "open_dialog_save", dialog_kind=bspec.dialog_kind)

      self.chronicle.entry(
        observation=f"Button '{bspec.label_contains}' on {page}",
        reasoning=f"Catalog action: {bspec.action}",
        action=f"click → {bspec.label_contains}",
        result="Executed",
        monologue="Clickety-click… what's behind door number two?",
        affect="Anticipation",
      )
      run.steps.append(StepResult(step, "pass", bspec.action, page))
    except Exception as exc:
      run.steps.append(StepResult(step, "fail", str(exc)[:120], page))
      self.state.record_anomaly(page, "button_fail", str(exc)[:200])

  def _close_dialog(self, save: bool = False, dialog_kind: str | None = None) -> None:
    from pywinauto import Desktop
    d = Desktop(backend="uia")
    for w in d.windows():
      try:
        t = w.window_text() or ""
        if "BRQOSH ERP" in t:
          continue
        if "BRQOSH" in t or w.descendants(control_type="Button"):
          if save and dialog_kind:
            self._fill_dialog(w, dialog_kind)
          label = "حفظ" if save else "إلغاء"
          for nm in [label, "إغلاق", "موافق", "OK"]:
            try:
              b = w.child_window(title=nm, control_type="Button")
              if b.exists(timeout=0.5):
                b.click_input()
                return
            except Exception:
              pass
      except Exception:
        continue

  def _fill_dialog(self, dlg, kind: str) -> None:
    from payload_factory import compliant_customer_name, compliant_product_code, compliant_vat
    fills = {
      "customer": [("اسم العميل", compliant_customer_name()), ("الرقم الضريبي", compliant_vat())],
      "supplier": [("اسم المورد", compliant_customer_name())],
      "product": [("كود", compliant_product_code()), ("اسم الصنف", "صنف اختبار")],
      "cost_center": [("الكود", "CC-1"), ("الاسم", "مركز")],
      "warehouse": [("الكود", "WH-1"), ("الاسم", "مستودع")],
      "bank_account": [("الكود", "BNK-1"), ("الاسم", "بنك")],
    }
    for label, val in fills.get(kind, []):
      try:
        e = dlg.child_window(title=label, control_type="Edit")
        if e.exists(timeout=0.5):
          e.set_edit_text(val)
      except Exception:
        pass
