"""Performance & memory profiling for desktop process."""

from __future__ import annotations

import time
from dataclasses import dataclass, field

import psutil


@dataclass
class PerfSample:
  t: float
  rss_mb: float
  cpu_pct: float


@dataclass
class PerformanceResult:
  samples: list[PerfSample] = field(default_factory=list)
  baseline_rss: float = 0
  peak_rss: float = 0
  leak_suspected: bool = False
  findings: list[str] = field(default_factory=list)

  def summary_md(self) -> str:
    lines = [
      f"- Baseline RSS: {self.baseline_rss:.1f} MB",
      f"- Peak RSS: {self.peak_rss:.1f} MB",
      f"- Leak suspected: {self.leak_suspected}",
    ]
    lines.extend(f"- {f}" for f in self.findings)
    return "\n".join(lines)


def profile_process(process_name: str, loops: int = 20, delay: float = 0.3) -> PerformanceResult:
  res = PerformanceResult()
  proc = None
  for p in psutil.process_iter(["name", "pid"]):
    if p.info["name"] and process_name.lower() in p.info["name"].lower():
      proc = psutil.Process(p.info["pid"])
      break
  if not proc:
    res.findings.append("Process not found for profiling")
    return res

  for i in range(loops):
    try:
      mem = proc.memory_info().rss / (1024 * 1024)
      cpu = proc.cpu_percent(interval=None)
      res.samples.append(PerfSample(time.time(), mem, cpu))
      if i == 0:
        res.baseline_rss = mem
      res.peak_rss = max(res.peak_rss, mem)
    except (psutil.NoSuchProcess, psutil.AccessDenied):
      break
    time.sleep(delay)

  if res.samples and res.peak_rss > res.baseline_rss * 1.35 and loops >= 10:
    res.leak_suspected = True
    res.findings.append(f"RSS grew {res.baseline_rss:.0f}→{res.peak_rss:.0f} MB over {loops} samples")
  if res.peak_rss > 800:
    res.findings.append(f"High working set {res.peak_rss:.0f} MB — monitor on 8h accountant sessions")
  return res
