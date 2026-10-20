#!/usr/bin/env bash
# Dev only: run the in-game self-test on the dev profile and print matching log lines.
#   bash tools/dev/selftest.sh <basic|parkour> [grep-regex-for-test-lines]
MODE="${1:-basic}"; PAT="${2:-.}"
source "$(dirname "$0")/devpaths.sh"
LOG="$PROFILE/BepInEx/LogOutput.log"
taskkill //IM STRAFTAT.exe //F >/dev/null 2>&1; sleep 3
sed -i "s/^AutoTest = .*/AutoTest = true/; s/^TestMode = .*/TestMode = $MODE/" "$CFG"
rm -f "$LOG"
powershell -ExecutionPolicy Bypass -File "$(dirname "$0")/launch.ps1" >/dev/null 2>&1
for i in $(seq 1 90); do grep -qE "\[test\] done|no local" "$LOG" 2>/dev/null && break; sleep 3; done
sed -i "s/^AutoTest = .*/AutoTest = false/; s/^TestMode = .*/TestMode = basic/" "$CFG"
echo "exceptions: $(grep -c Exception "$LOG")"; grep -m3 -B1 -A3 Exception "$LOG"
grep -E "\[(test|perf)\]" "$LOG" | sed 's/.*\] \[/[/' | grep -E "$PAT"
taskkill //IM STRAFTAT.exe //F >/dev/null 2>&1
true
