"""
pywinauto UIA navigator for BRQOSH WPF desktop — login, nav, payload injection.
"""

from __future__ import annotations

import logging
import os
import subprocess
import time
from dataclasses import dataclass, field
from pathlib import Path
from typing import Any

from nav_key_map import key_for_title
from payloads import payloads_for_type
from pywinauto import Application, Desktop
from pywinauto.findwindows import ElementNotFoundError
from pywinauto.timings import TimeoutError as PywinautoTimeout

logger = logging.getLogger(__name__)


@dataclass
class NavResult:
  page: str
  success: bool
  message: str
  screenshot: str | None = None
  payloads_tested: int = 0
  buttons_clicked: int = 0


@dataclass
class AuditSession:
  username: str
  pages_visited: list[NavResult] = field(default_factory=list)
  failures: list[dict[str, Any]] = field(default_factory=list)
  payloads_injected: int = 0


class UiNavigator:
  def __init__(
    self,
    config: dict[str, Any],
    repo_root: Path,
    vision: Any,
    crisis: Any,
    ui_map: dict[str, Any] | None = None,
  ):
    self.config = config
    self.repo_root = repo_root
    self.vision = vision
    self.crisis = crisis
    self.ui_map = ui_map or {}
    self.app: Application | None = None
    self.main_window = None
    self.session = AuditSession(username="")
    self._delay = config.get("audit", {}).get("interaction_delay_ms", 800) / 1000.0

  def _resolve_exe(self) -> Path | None:
    candidates = self.config.get("app", {}).get("exe_candidates", [])
    found: list[Path] = []
    for rel in candidates:
      p = (self.repo_root / rel).resolve()
      if p.exists():
        found.append(p)
    if not found:
      return None
    return max(found, key=lambda x: x.stat().st_mtime)

  def start_app(self) -> None:
    exe = self._resolve_exe()
    env = {**os.environ, **self.config.get("app", {}).get("env", {})}
    desktop_proj = (self.repo_root / self.config["desktop_project"]).resolve()

    if exe:
      logger.info("Starting %s", exe)
      self.app = Application(backend="uia").start(str(exe), work_dir=str(exe.parent), timeout=60)
    else:
      logger.info("Starting via dotnet run: %s", desktop_proj)
      # pywinauto can't dotnet-run directly — spawn process then connect
      psi = subprocess.Popen(
        ["dotnet", "run", "--project", str(desktop_proj), "-c", "Release", "--nologo"],
        cwd=str(self.repo_root),
        env=env,
      )
      time.sleep(8)
      self.app = Application(backend="uia").connect(process=psi.pid, timeout=60)

    for k, v in env.items():
      os.environ[k] = v

  def connect_login_window(self, timeout: int = 120):
    login_title = self.config["app"].get("window_title_login", "تسجيل الدخول")
    desktop = Desktop(backend="uia")
    deadline = time.time() + timeout
    while time.time() < deadline:
      for title in [login_title, "BRQOSH"]:
        try:
          win = desktop.window(title_re=f".*{title}.*", found_index=0)
          if win.exists(timeout=2):
            return win
        except (ElementNotFoundError, PywinautoTimeout):
          pass
      time.sleep(1)
    raise TimeoutError(f"Login window not found within {timeout}s")

  def login(self) -> None:
    creds = self.config.get("credentials", {})
    user = os.environ.get(creds.get("username_env", "BRQOSH_TEST_USERNAME"), creds.get("default_username", "marwan"))
    passwd = os.environ.get(creds.get("password_env", "BRQOSH_TEST_PASSWORD"), creds.get("default_password", "UiAudit9!"))
    self.session.username = user

    self._handle_blocking_wizards()
    login_win = self.connect_login_window(self.config.get("audit", {}).get("login_timeout_sec", 120))
    self.vision.capture_window(login_win.wrapper_object(), "login_window")

    self._login_fill(login_win, user, passwd)
    time.sleep(4)
    self._dismiss_dialogs()
    self._handle_password_change(passwd)
    self._handle_blocking_wizards()
    time.sleep(2)

    self.main_window = self._connect_main_window()
    self.vision.capture_window(self.main_window.wrapper_object(), "main_window_ready")

  def _login_fill(self, login_win, username: str, password: str) -> None:
    """Mirror tools/UiAudit/UiAutomationHelpers.LoginAsync."""
    # Username
    for spec in [
      {"auto_id": "LoginUsername"},
      {"title": "اسم المستخدم", "control_type": "Edit"},
    ]:
      if self._set_field(login_win, spec, username):
        break

    # Reveal password (ToggleButton)
    try:
      reveal = login_win.child_window(auto_id="LoginPasswordReveal", control_type="Button")
      if reveal.exists(timeout=1):
        reveal.click_input()
        time.sleep(0.2)
    except Exception:
      pass

    # Password plain text box after reveal
    if not self._set_field(login_win, {"auto_id": "LoginPasswordPlain"}, password):
      self._set_password_fallback(login_win, password)

    # Submit
    submitted = self._click_button(login_win, ["تسجيل الدخول", "LoginSubmit", "Login"])
    if not submitted:
      try:
        login_win.type_keys("{ENTER}")
      except Exception:
        pass

  def _set_field(self, parent, spec: dict, value: str) -> bool:
    try:
      kwargs = {}
      if "auto_id" in spec:
        kwargs["auto_id"] = spec["auto_id"]
      if "title" in spec:
        kwargs["title"] = spec["title"]
      if "control_type" in spec:
        kwargs["control_type"] = spec["control_type"]
      ctrl = parent.child_window(**kwargs)
      if ctrl.exists(timeout=2):
        ctrl.set_focus()
        ctrl.set_edit_text(value)
        return True
    except Exception:
      pass
    return False

  def _set_password_fallback(self, parent, password: str) -> None:
    try:
      edits = parent.descendants(control_type="Edit")
      target = None
      for edit in edits:
        try:
          name = edit.element_info.name or ""
          if "كلمة المرور" in name:
            target = edit
            break
        except Exception:
          continue
      if target is None and len(edits) >= 2:
        target = edits[1]
      elif target is None and edits:
        target = edits[0]
      if target:
        target.set_focus()
        try:
          target.set_edit_text(password)
        except Exception:
          target.type_keys(password, with_spaces=True)
    except Exception as exc:
      logger.debug("Password fallback failed: %s", exc)

  def _handle_blocking_wizards(self) -> None:
    desktop = Desktop(backend="uia")
    for title_part in ["إعداد النظام", "Setup", "ترخيص", "License"]:
      try:
        w = desktop.window(title_re=f".*{title_part}.*")
        if w.exists(timeout=2):
          self.vision.capture_window(w.wrapper_object(), f"blocking_{title_part}")
          # Try cancel/close — live env should be pre-seeded
          self._click_button(w, ["إلغاء", "Cancel", "إغلاق", "Close", "موافق", "OK", "تخطي", "Skip"])
          time.sleep(1)
      except Exception:
        pass

  def _connect_main_window(self, timeout: int | None = None):
    timeout = timeout or self.config.get("audit", {}).get("login_timeout_sec", 120)
    main_title = self.config["app"].get("window_title_main", "BRQOSH ERP")
    desktop = Desktop(backend="uia")
    deadline = time.time() + timeout
    while time.time() < deadline:
      for pattern in [main_title, "BRQOSH ERP", "BRQOSH"]:
        try:
          win = desktop.window(title_re=f".*{pattern}.*", found_index=0)
          if win.exists(timeout=1):
            # Exclude login window
            t = win.window_text()
            if "تسجيل الدخول" in t or "Login" in t and "ERP" not in t:
              continue
            # Prefer window with Nav buttons
            try:
              navs = [b for b in win.descendants(control_type="Button")
                      if (b.element_info.automation_id or "").startswith("Nav_")]
              if navs or "ERP" in t:
                return win
            except Exception:
              if "ERP" in t:
                return win
        except (ElementNotFoundError, PywinautoTimeout):
          pass
      self._dismiss_dialogs()
      time.sleep(1)
    raise TimeoutError("Main window not found after login")

  def _handle_password_change(self, passwd: str) -> None:
    desktop = Desktop(backend="uia")
    for title in ["تغيير كلمة المرور", "Password"]:
      try:
        dlg = desktop.window(title_re=f".*{title}.*")
        if dlg.exists(timeout=2):
          self._set_edit(dlg, ["كلمة المرور الجديدة", "New"], passwd)
          self._set_edit(dlg, ["تأكيد", "Confirm"], passwd)
          self._click_button(dlg, ["حفظ", "موافق", "Save", "OK"])
          time.sleep(2)
          return
      except Exception:
        pass

  def _dismiss_dialogs(self) -> None:
    desktop = Desktop(backend="uia")
    for title in ["موافق", "OK", "تنبيه", "Warning", "خطأ", "Error"]:
      try:
        dlg = desktop.window(title_re=f".*{title}.*", control_type="Window")
        if dlg.exists(timeout=1):
          self._click_button(dlg, ["موافق", "OK", "إغلاق", "Close"])
      except Exception:
        pass

  def _set_edit(self, parent, name_candidates: list[str], value: str) -> bool:
    for name in name_candidates:
      try:
        ctrl = parent.child_window(title=name, control_type="Edit")
        if ctrl.exists(timeout=1):
          ctrl.set_focus()
          ctrl.set_edit_text(value)
          return True
      except Exception:
        pass
      try:
        ctrl = parent.child_window(auto_id=name, control_type="Edit")
        if ctrl.exists(timeout=1):
          ctrl.set_focus()
          ctrl.set_edit_text(value)
          return True
      except Exception:
        pass
    return False

  def _click_button(self, parent, name_candidates: list[str]) -> bool:
    for name in name_candidates:
      try:
        btn = parent.child_window(title=name, control_type="Button")
        if btn.exists(timeout=1):
          btn.click_input()
          time.sleep(self._delay)
          return True
      except Exception:
        pass
    return False

  def _find_nav_button(self, page_title: str):
    if self.main_window is None:
      return None
    nav_key = key_for_title(page_title)
    if nav_key:
      try:
        btn = self.main_window.child_window(auto_id=f"Nav_{nav_key}", control_type="Button")
        if btn.exists(timeout=2):
          return btn
      except Exception:
        pass
    try:
      btn = self.main_window.child_window(title=page_title, control_type="Button")
      if btn.exists(timeout=3):
        return btn
    except Exception:
      pass
    # partial match scan
    try:
      buttons = self.main_window.descendants(control_type="Button")
      for b in buttons:
        try:
          t = b.window_text()
          if page_title in t or t in page_title:
            return b
        except Exception:
          continue
    except Exception:
      pass
    return None

  def navigate_to_page(self, page_title: str) -> NavResult:
    result = NavResult(page=page_title, success=False, message="")
    try:
      btn = self._find_nav_button(page_title)
      if btn is None:
        result.message = f"Nav button not found: {page_title}"
        self.session.failures.append({"page": page_title, "error": result.message})
        return result

      btn.click_input()
      time.sleep(self._delay * 2)
      shot = self.vision.capture_window(self.main_window.wrapper_object(), f"page_{page_title}")
      result.screenshot = str(shot)
      result.success = True
      result.message = "Navigated"
      self.crisis.mark_safe(page=page_title)
      return result
    except Exception as exc:
      result.message = str(exc)
      self.session.failures.append({"page": page_title, "error": str(exc)})
      return result

  def _audit_page_actions_body(self, page_title: str, inject_payloads: bool) -> tuple[int, int]:
    payloads_count = 0
    buttons_clicked = 0
    safe_labels = ["عرض", "تحديث", "فحص", "KPI"]
    skip_labels = ["حذف", "إلغاء", "عكس", "إيقاف", "ترحيل", "إرسال", "إقفال"]

    if self.main_window is None:
      return payloads_count, buttons_clicked

    try:
      buttons = self.main_window.descendants(control_type="Button")
      for b in buttons[:30]:
        try:
          label = b.window_text() or ""
          if any(s in label for s in skip_labels):
            continue
          if any(s in label for s in safe_labels) or "جديد" in label:
            b.click_input()
            time.sleep(self._delay)
            buttons_clicked += 1
            self.vision.capture_window(self.main_window.wrapper_object(), f"btn_{page_title}_{label[:20]}")
            self._dismiss_dialogs()
        except Exception:
          continue
    except Exception as exc:
      logger.debug("Button audit skip: %s", exc)

    if inject_payloads:
      payloads_count = self._inject_payloads_on_page(page_title)

    return payloads_count, buttons_clicked

  def audit_page_actions(self, page_title: str, inject_payloads: bool = True) -> NavResult:
    nav = self.navigate_to_page(page_title)
    if not nav.success:
      return nav

    payloads_count, buttons_clicked = self._audit_page_actions_body(page_title, inject_payloads)
    nav.payloads_tested = payloads_count
    nav.buttons_clicked = buttons_clicked
    self.session.payloads_injected += payloads_count
    self.session.pages_visited.append(nav)
    return nav

  def _inject_payloads_on_page(self, page_title: str) -> int:
    count = 0
    per_field = self.config.get("audit", {}).get("payloads_per_field", 10)
    if self.main_window is None:
      return 0

    # Find inputs in live UI
    try:
      edits = self.main_window.descendants(control_type="Edit")
    except Exception:
      return 0

    for edit in edits[:8]:
      try:
        auto_name = ""
        try:
          auto_name = edit.element_info.name or ""
        except Exception:
          pass
        payload_type = "text"
        # cross-ref ui_map
        for page in self.ui_map.get("pages", []):
          if page_title and page.get("nav_title") and page_title not in str(page.get("nav_title")):
            continue
          for el in page.get("elements", []):
            if el.get("automation_name") and el["automation_name"] in auto_name:
              payload_type = el.get("payload_type", "text")
              break

        for payload in payloads_for_type(payload_type, per_field):
          try:
            edit.set_focus()
            edit.set_edit_text(payload)
            count += 1
            time.sleep(0.15)
          except Exception:
            break
      except Exception:
        continue

    return count

  def count_nav_buttons(self) -> int:
    if self.main_window is None:
      return 0
    try:
      return sum(
        1 for b in self.main_window.descendants(control_type="Button")
        if (getattr(b.element_info, "automation_id", None) or "").startswith("Nav_")
      )
    except Exception:
      return 0

  def run_navigation_audit(self, pages: list[str]) -> list[NavResult]:
    max_pages = self.config.get("audit", {}).get("max_nav_pages", 0)
    if max_pages > 0:
      pages = pages[:max_pages]

    inject = self.config.get("audit", {}).get("payload_injection", True)
    results: list[NavResult] = []
    for page in pages:
      def _do():
        return self.audit_page_actions(page, inject_payloads=inject)

      try:
        r = self.crisis.wrap(
          f"nav_{page}",
          _do,
          restart_fn=self._full_restart,
          recover_fn=lambda: self._recover_to_page(page),
          source_hint=self._source_hint_for_page(page),
        )
        if r:
          results.append(r)
      except Exception as exc:
        fail = NavResult(page=page, success=False, message=str(exc))
        results.append(fail)
        self.session.failures.append({"page": page, "error": str(exc)})
    return results

  def _source_hint_for_page(self, page_title: str) -> str | None:
    for page in self.ui_map.get("pages", []):
      if page.get("nav_title") == page_title:
        return page.get("xaml_file")
    return "src/Brqosh.Desktop/Navigation/NavigationCatalog.cs"

  def _full_restart(self) -> None:
    self.crisis.kill_app()
    time.sleep(2)
    self.start_app()
    self.login()

  def _recover_to_page(self, page_title: str) -> None:
    if self.crisis.state.last_safe_page and self.crisis.state.last_safe_page != page_title:
      self.navigate_to_page(self.crisis.state.last_safe_page)
    time.sleep(1)
