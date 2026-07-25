#!/usr/bin/env python3
"""
BRQOSH Desktop Omni-Audit — main orchestrator.
Static map → live pywinauto UIA → payloads → crisis recovery → AUDIT_REPORT.md
"""

from __future__ import annotations

import argparse
import logging
import sys
import time
from pathlib import Path

import yaml

from crisis_handler import CrisisHandler
from report_builder import build_audit_report
from ui_map_builder import build_ui_map, load_ui_map, save_ui_map
from vision_capture import VisionCapture

logging.basicConfig(
  level=logging.INFO,
  format="%(asctime)s [%(levelname)s] %(name)s: %(message)s",
)
logger = logging.getLogger("audit_orchestrator")


def load_config(config_path: Path) -> dict:
  with config_path.open(encoding="utf-8") as f:
    return yaml.safe_load(f)


def resolve_repo_root(tool_dir: Path, config: dict) -> Path:
  rel = config.get("repo_root", "../..")
  return (tool_dir / rel).resolve()


def main(argv: list[str] | None = None) -> int:
  parser = argparse.ArgumentParser(description="BRQOSH Desktop Omni-Audit")
  parser.add_argument("--config", default="config.yaml", help="Path to config.yaml")
  parser.add_argument("--static-only", action="store_true", help="Build UI map only, skip live audit")
  parser.add_argument("--max-pages", type=int, default=0, help="Limit nav pages (0=all)")
  parser.add_argument("--no-payloads", action="store_true", help="Skip payload injection")
  parser.add_argument("--no-seed", action="store_true", help="Skip FullScenarioAudit seed")
  parser.add_argument("--comprehensive", action="store_true", default=True, help="Deep PageAuditCatalog audit (default)")
  parser.add_argument("--nav-only", action="store_true", help="Sidebar navigation only (skip comprehensive)")
  args = parser.parse_args(argv)

  tool_dir = Path(__file__).resolve().parent
  config_path = (tool_dir / args.config).resolve()
  config = load_config(config_path)
  repo_root = resolve_repo_root(tool_dir, config)

  if args.max_pages > 0:
    config.setdefault("audit", {})["max_nav_pages"] = args.max_pages
  if args.no_payloads:
    config.setdefault("audit", {})["payload_injection"] = False

  if args.nav_only:
    config.setdefault("audit", {})["comprehensive"] = False
  elif args.comprehensive:
    config.setdefault("audit", {})["comprehensive"] = True

  out_base = tool_dir / config.get("output", {}).get("base_dir", "output")
  screenshots_dir = tool_dir / config["output"]["screenshots_dir"]
  ui_map_path = tool_dir / config["output"]["ui_map_file"]
  report_path = tool_dir / config["output"]["audit_report"]
  crisis_log = tool_dir / config["output"]["crisis_log"]

  desktop_source = repo_root / config["desktop_source"]
  logger.info("Repo root: %s", repo_root)

  # Phase 1: Static UI map
  logger.info("Phase 1: Building static UI map from %s", desktop_source)
  ui_map_obj = build_ui_map(
    desktop_source,
    repo_root,
    config.get("navigation_pages"),
  )
  save_ui_map(ui_map_obj, ui_map_path)
  ui_map = load_ui_map(ui_map_path)
  logger.info("UI map stats: %s", ui_map.get("stats"))

  if args.static_only:
    logger.info("Static-only mode — done.")
    return 0

  # Phase 2: Prepare environment
  vision = VisionCapture(
    screenshots_dir,
    ocr_enabled=config.get("automation", {}).get("ocr_enabled", True),
    ocr_lang=config.get("automation", {}).get("ocr_lang", "ara+eng"),
  )
  crisis = CrisisHandler(
    config["app"]["process_name"],
    crisis_log,
    vision=vision,
  )

  seed_project = repo_root / config.get("full_scenario_audit", "tools/FullScenarioAudit/FullScenarioAudit.csproj")
  desktop_project = repo_root / config["desktop_project"]

  if config.get("audit", {}).get("seed_data", True) and not args.no_seed:
    logger.info("Phase 2a: Seeding QA data (profile=%s)", config["audit"].get("profile", "Manufacturing"))
    crisis.run_seed(repo_root, seed_project, config["audit"].get("profile", "Manufacturing"))

  logger.info("Phase 2b: Building Desktop Release")
  crisis.build_dotnet(desktop_project, repo_root)

  navigator = None
  nav_results = []
  comprehensive_results = None
  t0 = time.time()

  try:
    from ui_navigator import UiNavigator

    navigator = UiNavigator(config, repo_root, vision, crisis, ui_map)

    logger.info("Phase 3: Starting application")
    crisis.kill_app()
    navigator.start_app()

    logger.info("Phase 4: Login")
    crisis.wrap("login", navigator.login, source_hint="src/Brqosh.Desktop/Views/LoginWindow.xaml")

    nav_count = navigator.count_nav_buttons()
    logger.info("Nav buttons detected: %s", nav_count)
    if nav_count == 0:
      logger.error("No navigation buttons — aborting live audit")
      return 1

    comprehensive_results = None
    if config.get("audit", {}).get("comprehensive", True):
      logger.info("Phase 5: COMPREHENSIVE audit (PageAuditCatalog — every button/dialog/grid)")
      from comprehensive_auditor import ComprehensiveAuditor
      auditor = ComprehensiveAuditor(navigator, repo_root, vision)
      comprehensive_results = auditor.run_all()
      logger.info(
        "Comprehensive: %s pass / %s fail / %s skip (total steps %s)",
        comprehensive_results.passed,
        comprehensive_results.failed,
        comprehensive_results.skipped,
        len(comprehensive_results.steps),
      )
      # Nav summary from comprehensive
      nav_pages = {s.page for s in comprehensive_results.steps if "تنقل/" in s.step}
      nav_results = [
        type("NR", (), {
          "page": p,
          "success": any(s.status == "pass" for s in comprehensive_results.steps if s.page == p and "تنقل/" in s.step),
          "message": "",
          "screenshot": None,
          "payloads_tested": 0,
          "buttons_clicked": sum(1 for s in comprehensive_results.steps if s.page == p and s.status == "pass" and "زر/" in s.step),
        })()
        for p in sorted(nav_pages)
      ]
    else:
      pages = config.get("navigation_pages", [])
      if config.get("audit", {}).get("max_nav_pages", 0) > 0:
        pages = pages[: config["audit"]["max_nav_pages"]]
      logger.info("Phase 5: Nav-only %s pages", len(pages))
      nav_results = navigator.run_navigation_audit(pages)

  except Exception as exc:
    logger.exception("Audit aborted: %s", exc)
    crisis.log_event(
      __import__("crisis_handler").CrisisEvent(
        timestamp=__import__("datetime").datetime.now(__import__("datetime").timezone.utc).isoformat(),
        phase="orchestrator",
        error=str(exc),
        traceback=__import__("traceback").format_exc(),
        recovered=False,
      )
    )
  finally:
    crisis.kill_app()

  duration = time.time() - t0

  # Phase 6: Report
  logger.info("Phase 6: Building AUDIT_REPORT.md")
  crisis_events = [e.__dict__ if hasattr(e, "__dict__") else e for e in crisis.state.events]
  build_audit_report(
    output_path=report_path,
    ui_map=ui_map,
    nav_results=nav_results,
    session=navigator.session if navigator else type("S", (), {"username": "", "failures": [], "payloads_injected": 0})(),
    crisis_events=crisis_events,
    vision_captures=vision.captures,
    config=config,
    repo_root=repo_root,
    duration_sec=duration,
    comprehensive_results=comprehensive_results,
  )
  logger.info("Report: %s", report_path)

  failed = sum(1 for r in nav_results if not getattr(r, "success", False))
  if comprehensive_results:
    failed = comprehensive_results.failed
  return 1 if failed > 0 else 0


if __name__ == "__main__":
  sys.exit(main())
