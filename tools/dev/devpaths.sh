# Sourced by the dev scripts: PROFILE = the BepInEx dev profile folder, read from the repo's (untracked)
# Directory.Build.props.user, so no machine-specific path lives in tracked files.
ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
PROPS="$ROOT/Directory.Build.props.user"
[ -f "$PROPS" ] || { echo "Copy Directory.Build.props.user.example to Directory.Build.props.user and set DevProfile." >&2; exit 1; }
PROFILE_WIN="$(sed -n 's:.*<DevProfile>\(.*\)</DevProfile>.*:\1:p' "$PROPS" | head -1)"
PROFILE="$(cygpath -u "$PROFILE_WIN" 2>/dev/null || echo "$PROFILE_WIN")"
CFG="$PROFILE/BepInEx/config/dev.riley.vholtat.cfg"
