#!/usr/bin/env bash
# Dev only: stuck-spot sweep (TestMode = sweep) on several maps, one launch per map.
#   bash tools/dev/map_sweep.sh [map ...]
MAPS=("$@"); [ ${#MAPS[@]} -eq 0 ] && MAPS=(Corridor_00 Scaffoldingue_00 Chateaux_01 Neo_Arena_03 Backrooms_00)
CFG="/c/Users/Riley/source/_vholtat-dev/BepInEx/config/dev.riley.vholtat.cfg"
for MAP in "${MAPS[@]}"; do
  sed -i "s/^TestMap = .*/TestMap = $MAP/" "$CFG"
  echo "=== $MAP"
  bash "$(dirname "$0")/selftest.sh" sweep "STALL|sweep .*stalls|exceptions" | grep -E "STALL|stalls|exceptions"
done
sed -i "s/^TestMap = .*/TestMap = Arena_00/" "$CFG"
