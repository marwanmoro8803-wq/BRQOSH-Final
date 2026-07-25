"""
Human navigator — UIA-first interactions + bored-user sabotage + window stress.
"""

from __future__ import annotations

import logging
import os
import random
import subprocess
import time
from pathlib import Path
from typing import Any

from nav_key_map import key_for_title
from pywinauto import Application, Desktop
from pywinauto.keyboard import send_keys

logger = logging.getLogger(__name__)


class HumanNavigator:
  def __init__(self, config: dict, repo_root: Path, vision: Any, chronicle: Any, state: Any):
    self.config = config
    self.repo_root = repo_root
    self.vision = vision
    self.chronicle = chronicle
    self.state = state
    self.app = None
    self.main = None
    self.profile = ""
    self.last_screen = ""
    self._delay = config.get("audit", {}).get("interaction_delay_ms", 600) / 1000.0
    self.chaos_p = config.get("audit", {}).get("chaos_probability", 0.15)

  def _exe(self) -> Path | None:
    cands = []
    for rel in self.config["app"]["exe_candidates"]:
      p = (self.repo_root / rel).resolve()
      if p.exists():
        cands.append(p)
    return max(cands, key=lambda x: x.stat().st_mtime) if cands else None

  def start(self) -> None:
    exe = self._exe()
    env = {**os.environ, **self.config["app"].get("env", {})}
    if exe:
      self.app = Application(backend="uia").start(str(exe), work_dir=str(exe.parent), timeout=60)
    else:
      proj = self.repo_root / self.config["desktop_project"]
      proc = subprocess.Popen(["dotnet", "run", "--project", str(proj), "-c", "Release", "--nologo"], cwd=str(self.repo_root), env=env)
      time.sleep(8)
      self.app = Application(backend="uia").connect(process=proc.pid, timeout=60)

  def login(self) -> None:
    creds = self.config["credentials"]
    user = os.environ.get(creds.get("username_env", "BRQOSH_TEST_USERNAME"), creds["default_username"])
    passwd = os.environ.get(creds.get("password_env", "BRQOSH_TEST_PASSWORD"), creds["default_password"])
    login = self._wait_window(["تسجيل الدخول", "BRQOSH"], 120)
    frame = self.vision.perceive_window(login, "login")
    self.chronicle.entry(
      observation=f"Login screen: {frame.window_title}; {len(frame.elements)} UIA elements",
      reasoning="Must authenticate before exploring ERP modules — marwan QA user.",
      action="Focus login form",
      result="Ready to type credentials",
      monologue="Another login screen… let's get this over with.",
      affect="Neutral impatience",
      screenshot=frame.path,
      source_hint="src/Brqosh.Desktop/Views/LoginWindow.xaml",
    )
    self._set(login, auto_id="LoginUsername", value=user)
    try:
      rev = login.child_window(auto_id="LoginPasswordReveal")
      if rev.exists(timeout=1):
        rev.click_input()
    except Exception:
      pass
    if not self._set(login, auto_id="LoginPasswordPlain", value=passwd):
      self._set_password(login, passwd)
    self._click(login, "تسجيل الدخول") or self._click(login, auto_id="LoginSubmit")
    time.sleep(4)
    self._dismiss_modals()
    self._password_change(passwd)
    self.main = self._wait_main(120)
    self.vision.perceive_window(self.main, "main_shell")
    self.chronicle.entry(
      observation=f"Main shell: {self.main.window_text()}",
      reasoning="Navigation sidebar should expose profile-specific modules.",
      action="Login complete",
      result=f"Nav ready — profile context {self.profile}",
      monologue="We're in. Time to poke everything.",
      affect="Cautious optimism",
    )

  def _wait_window(self, parts: list[str], timeout: int):
    deadline = time.time() + timeout
    d = Desktop(backend="uia")
    while time.time() < deadline:
      for p in parts:
        try:
          w = d.window(title_re=f".*{p}.*")
          if w.exists(timeout=1):
            return w
        except Exception:
          pass
      time.sleep(0.5)
    raise TimeoutError(f"Window not found: {parts}")

  def _wait_main(self, timeout: int):
    deadline = time.time() + timeout
    d = Desktop(backend="uia")
    while time.time() < deadline:
      for p in ["BRQOSH ERP", "BRQOSH"]:
        try:
          w = d.window(title_re=f".*{p}.*")
          if w.exists(timeout=1) and "تسجيل" not in (w.window_text() or ""):
            return w
        except Exception:
          pass
      self._dismiss_modals()
      time.sleep(0.8)
    raise TimeoutError("Main window missing")

  def _set(self, parent, *, title: str | None = None, auto_id: str | None = None, value: str = "") -> bool:
    try:
      kw = {}
      if title:
        kw["title"] = title
      if auto_id:
        kw["auto_id"] = auto_id
      kw["control_type"] = "Edit"
      c = parent.child_window(**kw)
      if c.exists(timeout=2):
        c.set_edit_text(value)
        return True
    except Exception:
      pass
    return False

  def _set_password(self, parent, value: str) -> None:
    for e in parent.descendants(control_type="Edit"):
      try:
        e.set_edit_text(value)
        return
      except Exception:
        try:
          e.type_keys(value, with_spaces=True)
          return
        except Exception:
          continue

  def _click(self, parent, title: str | None = None, auto_id: str | None = None) -> bool:
    try:
      kw = {"control_type": "Button"}
      if title:
        kw["title"] = title
      if auto_id:
        kw["auto_id"] = auto_id
      b = parent.child_window(**kw)
      if b.exists(timeout=2):
        b.click_input()
        time.sleep(self._delay)
        return True
    except Exception:
      pass
    return False

  def _dismiss_modals(self) -> None:
    d = Desktop(backend="uia")
    for label in ["موافق", "OK", "إغلاق", "Close"]:
      try:
        for w in d.windows():
          try:
            b = w.child_window(title=label, control_type="Button")
            if b.exists(timeout=0.3):
              b.click_input()
          except Exception:
            pass
      except Exception:
        pass

  def _password_change(self, passwd: str) -> None:
    d = Desktop(backend="uia")
    try:
      w = d.window(title_re=".*تغيير كلمة المرور.*")
      if w.exists(timeout=2):
        self._set(w, title="كلمة المرور الجديدة", value=passwd)
        self._set(w, title="تأكيد", value=passwd)
        self._click(w, "حفظ")
        time.sleep(2)
    except Exception:
      pass

  def find_nav(self, title: str):
    """Find nav button with scroll-into-view — fixes post-audit nav blindness."""
    if not self.main:
      return None
    key = key_for_title(title)
    if key:
      try:
        b = self.main.child_window(auto_id=f"Nav_{key}", control_type="Button")
        if b.exists(timeout=2):
          try:
            b.scroll_into_view()
          except Exception:
            pass
          return b
      except Exception:
        pass
    # Scroll nav host and scan
    for _ in range(28):
      for b in self.main.descendants(control_type="Button"):
        try:
          aid = b.element_info.automation_id or ""
          txt = b.window_text() or ""
          if aid.startswith("Nav_") and (title in txt or txt in title):
            try:
              b.scroll_into_view()
            except Exception:
              pass
            return b
        except Exception:
          continue
      self._scroll_nav_down()
    return None

  def _scroll_nav_down(self) -> None:
    try:
      for pane in self.main.descendants(control_type="Pane"):
        try:
          if pane.is_scrollable():
            pane.scroll("down", "line", count=3)
            return
        except Exception:
          continue
    except Exception:
      pass
    try:
      send_keys("{PGDN}")
    except Exception:
      pass
    time.sleep(0.15)

  def navigate(self, title: str) -> bool:
    btn = self.find_nav(title)
    if not btn:
      self.chronicle.entry(
        observation=f"Nav item '{title}' not visible",
        reasoning=f"May be outside {self.profile} module matrix or below scroll fold.",
        action=f"Search Nav_{key_for_title(title)}",
        result="NOT FOUND",
        monologue="Where did you hide, little menu item?",
        affect="Confusion",
        source_hint="src/Brqosh.Desktop/Navigation/NavigationCatalog.cs",
      )
      return False
    prev = self.last_screen or self.state.screen_key(self.profile, "shell")
    btn.click_input()
    time.sleep(self._delay * 2)
    frame = self.vision.perceive_window(self.main, f"page_{title[:30]}")
    self.state.record_transition(prev, self.state.screen_key(self.profile, title), "nav_click")
    self.last_screen = self.state.screen_key(self.profile, title)
    scr = self.state.get_or_create_screen(self.profile, title)
    for el in frame.elements:
      scr.elements.append(__import__("app_state_model").UiElementNode(
        identity=f"{el.control_type}:{el.name}:{el.automation_id}",
        control_type=el.control_type,
        name=el.name,
        automation_id=el.automation_id,
      ))
    self.chronicle.entry(
      observation=self.vision.read_visible_text(frame)[:400],
      reasoning=f"Opened '{title}' — scanning all fields/buttons before chaos.",
      action=f"Click nav → {title}",
      result=f"{len(frame.elements)} interactive elements perceived",
      monologue="New room unlocked. What's breakable here?",
      affect="Curious focus",
      screenshot=frame.path,
    )
    if random.random() < self.chaos_p:
      self._bored_user_burst()
    return True

  def _bored_user_burst(self) -> None:
    if not self.config.get("audit", {}).get("sabotage_enabled", True):
      return
    acts = [
      ("{TAB}" * 5, "Tab-hopping through fields like a caffeinated accountant"),
      ("{ESC}", "Escape mid-flow — does it lose data?"),
      ("{ENTER}", "Enter spam — double-submit test"),
    ]
    keys, thought = random.choice(acts)
    try:
      send_keys(keys)
      self.chronicle.entry(
        observation="Idle moment on page",
        reasoning="Bored-user syndrome — erratic keyboard.",
        action=keys,
        result="Observed response",
        monologue=thought,
        affect="Mischievous boredom",
      )
    except Exception:
      pass

  def resize_stress(self) -> None:
    if not self.config.get("audit", {}).get("resize_stress", True) or not self.main:
      return
    try:
      r = self.main.rectangle()
      self.main.move_window(x=r.left, y=r.top, width=900, height=600)
      time.sleep(0.5)
      self.main.move_window(width=1600, height=900)
      frame = self.vision.perceive_window(self.main, "resize_stress")
      self.chronicle.entry(
        observation="Window resized to postage-stamp then large",
        reasoning="Visual sabotage — RTL dense ERP may clip/overlap.",
        action="resize 900x600 → 1600x900",
        result=frame.ocr_text[:200],
        monologue="Let's see if the UI survives my vandalism.",
        affect="Playful cruelty",
        screenshot=frame.path,
      )
    except Exception:
      pass

  def find_page_button(self, label: str):
    roots = []
    try:
      h = self.main.child_window(auto_id="PageHost")
      if h.exists(timeout=1):
        roots.append(h)
    except Exception:
      pass
    roots.append(self.main)
    for root in roots:
      for b in root.descendants(control_type="Button"):
        try:
          t = b.window_text() or ""
          if label in t:
            return b
          for tx in b.descendants(control_type="Text"):
            if label in (tx.window_text() or ""):
              return b
        except Exception:
          continue
    return None

  def count_nav_buttons(self) -> int:
    try:
      return sum(1 for b in self.main.descendants(control_type="Button")
                 if (getattr(b.element_info, "automation_id", None) or "").startswith("Nav_"))
    except Exception:
      return 0
