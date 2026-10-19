#!/usr/bin/env bash
# Dev only: host = Steam copy on the dev profile running the parkour self-test with --vholtat-lan host; client = the
# copy from make_client_instance.ps1 with --vholtat-lan client, which logs how the host's movement looks from its side.
#   bash tools/dev/lan_test.sh [seconds]
SECS="${1:-150}"
PROFILE="/c/Users/Riley/source/_vholtat-dev"
CLIENT="$PROFILE/client-instance"
CFG="$PROFILE/BepInEx/config/dev.riley.vholtat.cfg"
taskkill //IM STRAFTAT.exe //F >/dev/null 2>&1; sleep 3
sed -i 's/^AutoTest = false/AutoTest = true/; s/^TestMode = basic/TestMode = parkour/' "$CFG"
rm -f "$PROFILE/BepInEx/LogOutput.log" "$CLIENT/BepInEx/LogOutput.log"
"/c/Program Files (x86)/Steam/steam.exe" -applaunch 2386720 --doorstop-enabled true \
  --doorstop-target-assembly "$(cygpath -w "$PROFILE")\BepInEx\core\BepInEx.Preloader.dll" --vholtat-lan host >/dev/null 2>&1 &
sleep 35
(cd "$CLIENT" && ./STRAFTAT.exe --vholtat-lan client >/dev/null 2>&1 &)
sleep "$SECS"
sed -i 's/^AutoTest = true/AutoTest = false/; s/^TestMode = parkour/TestMode = basic/' "$CFG"
echo "== host"; grep -aE "\[(lan|test)\]|Exception" "$PROFILE/BepInEx/LogOutput.log" | cut -c1-200 | grep -vE "\[test\] (pk|wr|kb)" | tail -20
echo "== client"; grep -aE "\[lan\]|host's VHOLTAT|Exception" "$CLIENT/BepInEx/LogOutput.log" | cut -c1-260 | tail -80
taskkill //IM STRAFTAT.exe //F >/dev/null 2>&1
