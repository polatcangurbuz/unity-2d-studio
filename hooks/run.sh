#!/bin/sh
# python3 first (macOS/Linux often lack `python`); test each, since Windows' python3 Store stub exists but fails.
for py in python3 python py; do
  if "$py" -c "import sys; sys.exit(sys.version_info < (3, 8))" >/dev/null 2>&1; then
    exec "$py" "$(dirname "$0")/../skills/unity-2d-studio/scripts/hooks.py" "$@"
  fi
done
echo "unity-2d-studio: Python 3.8+ not found (python3/python/py); hooks skipped" >&2
exit 0
