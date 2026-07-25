#!/usr/bin/env bash
set -euo pipefail
ROOT="$(cd "$(dirname "$0")/../.." && pwd)"
DIR="$ROOT/tools/desktop-qa-army"
cd "$DIR"
VENV="$DIR/.venv/bin/python"
if [[ ! -x "$VENV" ]]; then
  python3 -m venv .venv
  .venv/bin/pip install -r requirements.txt
fi
pkill -f Brqosh.Desktop 2>/dev/null || true
echo "=== BRQOSH Desktop QA Army ==="
exec "$VENV" audit_orchestrator.py "$@"
