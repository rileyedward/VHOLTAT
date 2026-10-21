#!/usr/bin/env bash
# Dev only: host = Steam copy on the dev profile running the parkour self-test with --vholtat-lan host; client = the
# copy from make_client_instance.ps1 with --vholtat-lan client, which logs how the host's movement looks from its side.
#   bash tools/dev/lan_test.sh [seconds]
SECS="${1:-150}"; MODE="${2:-parkour}"
# Optional simulated network per copy, e.g. LAG="--vholtat-lag 60 --vholtat-loss 0.02" bash tools/dev/lan_test.sh
LAG="${LAG:-}"
source "$(dirname "$0")/devpaths.sh"
CLIENT="$PROFILE/client-instance"
taskkill //IM STRAFTAT.exe //F >/dev/null 2>&1; sleep 3
sed -i "s/^AutoTest = .*/AutoTest = true/; s/^TestMode = .*/TestMode = $MODE/" "$CFG"
rm -f "$PROFILE/BepInEx/LogOutput.log" "$CLIENT/BepInEx/LogOutput.log"
"/c/Program Files (x86)/Steam/steam.exe" -applaunch 2386720 --doorstop-enabled true \
  --doorstop-target-assembly "$(cygpath -w "$PROFILE")\BepInEx\core\BepInEx.Preloader.dll" --vholtat-lan host $LAG >/dev/null 2>&1 &
sleep 35
(cd "$CLIENT" && ./STRAFTAT.exe --vholtat-lan client $LAG -screen-fullscreen 0 -screen-width 1280 -screen-height 720 >/dev/null 2>&1 &)
sleep "$SECS"
sed -i 's/^AutoTest = true/AutoTest = false/; s/^TestMode = .*/TestMode = basic/' "$CFG"
echo "== host"; grep -aE "\[(lan|test|match)]|Exception" "$PROFILE/BepInEx/LogOutput.log" | cut -c1-200 | grep -vE "\[test\] (pk|wr|kb)" | tail -60
echo "== client"; grep -aE "\[lan\]|host's VHOLTAT|Exception" "$CLIENT/BepInEx/LogOutput.log" | cut -c1-260 | tail -80
taskkill //IM STRAFTAT.exe //F >/dev/null 2>&1
