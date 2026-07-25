#!/usr/bin/env python3
"""
BRQOSH Desktop QA Army — ten-role orchestrator.
Superintelligence core + cognitive loop + per-role static/live audits.
"""

from __future__ import annotations

import argparse
import logging
import sys
import time
from pathlib import Path

import yaml

# qa_army modules use flat imports (copied from cognitive-audit)
_ROOT = Path(__file__).resolve().parent
sys.path.insert(0, str(_ROOT))
sys.path.insert(0, str(_ROOT / "qa_army"))

from app_state_model import load_state, save_state  # noqa: E402
from chronicle_logger import ChronicleLogger  # noqa: E402
from cognitive_engine import CognitiveEngine  # noqa: E402
from forensic_handler import ForensicHandler  # noqa: E402
from human_navigator import HumanNavigator  # noqa: E402
from vision_brain import VisionBrain  # noqa: E402

from qa_army.accessibility_auditor import audit_rtl_flow, audit_uia_elements  # noqa: E402
from qa_army.compatibility_tester import build_matrix  # noqa: E402
from qa_army.copywrite_docs_auditor import audit_strings  # noqa: E402
from qa_army.dependency_scanner import scan_network_surface, scan_repo  # noqa: E402
from qa_army.design_auditor import audit_frames  # noqa: E402
from qa_army.l10n_i18n_tester import audit_source_i18n  # noqa: E402
from qa_army.oracles import infer_accounting_oracles  # noqa: E402
from qa_army.pentest_engine import run_injection_battery  # noqa: E402
from qa_army.performance_profiler import profile_process  # noqa: E402
from report_builder import build_all_deliverables  # noqa: E402

logging.basicConfig(level=logging.INFO, format="%(asctime)s [%(levelname)s] %(message)s")
log = logging.getLogger("qa_army")


def main(argv=None) -> int:
  parser = argparse.ArgumentParser(description="BRQOSH Desktop QA Army")
  parser.add_argument("--config", default="config.yaml")
  parser.add_argument("--profile", default="", help="Single profile override")
  parser.add_argument("--headless", action="store_true", help="CI mode — no interactive pause")
  args = parser.parse_args(argv)

  started = time.time()
  tool = _ROOT
  cfg = yaml.safe_load((tool / args.config).read_text(encoding="utf-8"))
  repo = (tool / cfg.get("repo_root", "../..")).resolve()
  out = tool / cfg["output"]["base_dir"]
  out.mkdir(parents=True, exist_ok=True)

  profiles = [args.profile] if args.profile else cfg["audit"].get("profiles", ["Retail"])
  desktop_src = repo / cfg.get("desktop_source", "src/Brqosh.Desktop")

  # --- Static ten-role passes (no app required) ---
  log.info("Static analysis — 10 roles (partial)")
  static = {
    "design": audit_frames([], repo / "src/Brqosh.Desktop/Themes/BrqoshTheme.xaml"),
    "i18n": audit_source_i18n(desktop_src),
    "copy": audit_strings(desktop_src),
    "deps": scan_repo(repo),
    "pentest_static": run_injection_battery(),
    "oracles": infer_accounting_oracles(),
    "network_notes": scan_network_surface(),
  }

  vision = VisionBrain(out / "screenshots", True)
  chronicle = ChronicleLogger(out / "CHRONICLE.md")
  state_path = out / "app_state_model.json"
  state = load_state(state_path)
  forensic = ForensicHandler(cfg["app"]["process_name"], out / "logs/crisis.jsonl", repo, vision)

  seed_proj = repo / cfg.get("full_scenario_audit", "tools/FullScenarioAudit/FullScenarioAudit.csproj")
  desktop_proj = repo / cfg["desktop_project"]
  live_runs = []
  all_elements = []
  resize_done = False

  for profile in profiles:
    log.info("=== LIVE Profile: %s ===", profile)
    if cfg["audit"].get("seed_data", True):
      forensic.run_seed(seed_proj, profile)
    forensic.build_desktop(desktop_proj)
    forensic.kill_app()

    nav = HumanNavigator(cfg, repo, vision, chronicle, state)
    engine = CognitiveEngine(nav, chronicle, state, repo, cfg)

    try:
      nav.start()
      nav.login()
      if cfg["audit"].get("resize_stress", True):
        nav.resize_stress()
        resize_done = True
      n = nav.count_nav_buttons()
      chronicle.entry(
        observation=f"{n} Nav_* buttons",
        reasoning=f"Profile {profile} module matrix",
        action="count_nav",
        result=str(n),
        monologue=f"Army deploying on {profile}…",
        affect="Battle-ready",
      )
      if n == 0:
        raise RuntimeError("No nav buttons")
      run = engine.audit_profile(profile)
      live_runs.append(run)
      if vision.frames:
        all_elements.extend(vision.frames[-1].elements)
      forensic.mark_safe(profile=profile)
    except Exception as exc:
      log.exception("Profile %s: %s", profile, exc)
      chronicle.entry(
        observation=str(exc)[:200],
        reasoning="Forensic recovery triggered",
        action="exception",
        result="logged",
        affect="Frustrated",
      )
    finally:
      forensic.kill_app()

  # Post-live analysis
  if vision.frames:
    static["design"] = audit_frames(vision.frames, desktop_src / "Themes/BrqoshTheme.xaml")
  perf = profile_process(cfg["app"]["process_name"], cfg["audit"].get("performance_loops", 20))
  a11y = audit_uia_elements(all_elements[:300])
  if vision.frames:
    a11y.findings.extend(audit_rtl_flow(vision.frames[-1].ocr_text))
  compat = build_matrix(resize_done, len(vision.frames))

  save_state(state, state_path)
  duration = time.time() - started

  build_all_deliverables(
    out_dir=out,
    repo=repo,
    cfg=cfg,
    static=static,
    live_runs=live_runs,
    vision=vision,
    chronicle_path=out / "CHRONICLE.md",
    state=state,
    perf=perf,
    a11y=a11y,
    compat=compat,
    forensic=forensic,
    duration_sec=duration,
  )

  crit = sum(r.failed for r in live_runs) + len([f for f in static["deps"].findings if f.severity == "Critical"])
  score = max(0, 100 - crit * 5 - sum(r.failed for r in live_runs))
  log.info("QA Army complete — score≈%s exit=%s", score, 1 if crit > 10 else 0)
  return 1 if crit > 10 else 0


if __name__ == "__main__":
  sys.exit(main())
