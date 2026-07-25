#!/usr/bin/env python3
"""
BRQOSH Desktop Cognitive Audit — orchestrator.
Human-like vision · learning state machine · sabotage · forensic recovery.
"""

from __future__ import annotations

import argparse
import logging
import sys
import time
from pathlib import Path

import yaml

from app_state_model import load_state, save_state
from chronicle_logger import ChronicleLogger
from cognitive_engine import CognitiveEngine
from forensic_handler import ForensicHandler
from human_navigator import HumanNavigator
from report_builder import build_master_report
from vision_brain import VisionBrain

logging.basicConfig(level=logging.INFO, format="%(asctime)s [%(levelname)s] %(message)s")
log = logging.getLogger("cognitive_audit")


def main(argv=None) -> int:
  parser = argparse.ArgumentParser(description="BRQOSH Cognitive Desktop Audit")
  parser.add_argument("--config", default="config.yaml")
  parser.add_argument("--profile", default="", help="Single profile override")
  args = parser.parse_args(argv)

  tool = Path(__file__).resolve().parent
  cfg = yaml.safe_load((tool / args.config).read_text(encoding="utf-8"))
  root = (tool / cfg.get("repo_root", "../..")).resolve()

  out = tool / cfg["output"]["base_dir"]
  vision = VisionBrain(tool / cfg["output"]["screenshots_dir"], cfg["automation"].get("ocr_enabled", True))
  chronicle = ChronicleLogger(tool / cfg["output"]["diary_file"])
  state_path = tool / cfg["output"]["state_model"]
  state = load_state(state_path)
  forensic = ForensicHandler(cfg["app"]["process_name"], tool / cfg["output"]["crisis_log"], root, vision)

  profiles = [args.profile] if args.profile else cfg["audit"].get("profiles", ["Retail"])
  seed_proj = root / cfg["full_scenario_audit"]
  desktop_proj = root / cfg["desktop_project"]

  all_runs = []
  t0 = time.time()

  for profile in profiles:
    log.info("=== Profile: %s ===", profile)
    if cfg["audit"].get("seed_data", True):
      forensic.run_seed(seed_proj, profile)
    forensic.build_desktop(desktop_proj)
    forensic.kill_app()

    nav = HumanNavigator(cfg, root, vision, chronicle, state)
    engine = CognitiveEngine(nav, chronicle, state, root, cfg)

    def _session():
      nav.start()
      nav.login()
      nav.resize_stress()
      n = nav.count_nav_buttons()
      chronicle.entry(
        observation=f"{n} Nav_* buttons visible",
        reasoning="Profile module matrix determines reachable pages.",
        action="count_nav",
        result=str(n),
        monologue=f"Profile {profile} — let's map what we can reach.",
        affect="Focused",
      )
      if n == 0:
        raise RuntimeError("No navigation buttons")
      forensic.mark_safe(profile=profile)
      return engine.audit_profile(profile)

    try:
      run = forensic.wrap(
        f"profile_{profile}",
        _session,
        restart_fn=lambda: None,
        source_hint="src/Brqosh.Desktop/MainWindow.xaml.cs",
        log_patterns=cfg.get("forensics", {}).get("log_paths", []),
      )
      if run:
        all_runs.append(run)
    except Exception as exc:
      log.exception("Profile %s failed: %s", profile, exc)
    finally:
      forensic.kill_app()

  save_state(state, state_path)
  duration = time.time() - t0

  report = build_master_report(
    tool / cfg["output"]["audit_report"],
    tool / cfg["output"].get("security_report", "output/SECURITY_SABOTAGE.md"),
    chronicle_path=tool / cfg["output"]["diary_file"],
    state=state,
    runs=all_runs,
    vision=vision,
    forensic=forensic,
    duration=duration,
    root=root,
  )
  log.info("Report: %s", report)
  failed = sum(r.failed for r in all_runs)
  return 1 if failed > 0 else 0


if __name__ == "__main__":
  sys.exit(main())
