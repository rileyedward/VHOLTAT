#!/usr/bin/env bash
# Dev only: host a real match on a random map with an idle second player (the client copy), then leave it to you.
#   bash tools/dev/play_lobby.sh [map|random]
MAP="${1:-random}"
source "$(dirname "$0")/devpaths.sh"
CLIENT="$PROFILE/client-instance"
taskkill //IM STRAFTAT.exe //F >/dev/null 2>&1; sleep 3
sed -i "s/^AutoTest = .*/AutoTest = true/; s/^TestMode = .*/TestMode = lobby/; s/^TestMap = .*/TestMap = $MAP/" "$CFG"
rm -f "$PROFILE/BepInEx/LogOutput.log"
"/c/Program Files (x86)/Steam/steam.exe" -applaunch 2386720 --doorstop-enabled true \
  --doorstop-target-assembly "$(cygpath -w "$PROFILE")\BepInEx\core\BepInEx.Preloader.dll" --vholtat-lan host >/dev/null 2>&1 &
sleep 30
(cd "$CLIENT" && ./STRAFTAT.exe --vholtat-lan client -screen-fullscreen 0 -screen-width 640 -screen-height 360 >/dev/null 2>&1 &)
for i in $(seq 1 60); do grep -q "\[test\] done" "$PROFILE/BepInEx/LogOutput.log" 2>/dev/null && break; sleep 3; done
sed -i "s/^AutoTest = .*/AutoTest = false/; s/^TestMode = .*/TestMode = basic/; s/^TestMap = .*/TestMap = Arena_00/" "$CFG"
grep -aE "\[match\]|\[test\] done|Exception" "$PROFILE/BepInEx/LogOutput.log" | cut -c1-160
