#!/bin/bash
# Installs build/ShareX-Mac.app into /Applications. Never invokes sudo itself;
# if /Applications is not writable it prints the exact command for the
# operator to run. See packaging/README.md.
set -euo pipefail

HERE="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "$HERE/.." && pwd)"

SRC_APP="${SXM_BUILD_APP:-$REPO_ROOT/build/ShareX-Mac.app}"
DEST_DIR="${SXM_INSTALL_DIR:-/Applications}"
DEST_APP="$DEST_DIR/ShareX-Mac.app"
APP_NAME="ShareX-Mac.app"

UNINSTALL=0
for arg in "$@"; do
  case "$arg" in
    --uninstall) UNINSTALL=1 ;;
    -h|--help)
      cat <<USAGE
Usage: $(basename "$0") [--uninstall]

Env overrides:
  SXM_BUILD_APP   path to the built .app to install (default: build/ShareX-Mac.app)
  SXM_INSTALL_DIR install destination directory (default: /Applications)
USAGE
      exit 0 ;;
    *) echo "error: unknown argument: $arg" >&2; exit 1 ;;
  esac
done

# ---- uninstall --------------------------------------------------------
if [ "$UNINSTALL" -eq 1 ]; then
  if [ ! -e "$DEST_APP" ]; then
    echo "Nothing to uninstall: $DEST_APP does not exist."
    exit 0
  fi
  if [ -w "$DEST_DIR" ]; then
    rm -rf "$DEST_APP"
    echo "Removed $DEST_APP"
  else
    echo "error: $DEST_DIR is not writable by $(whoami); run this yourself:" >&2
    echo "  sudo rm -rf \"$DEST_APP\"" >&2
    exit 1
  fi
  cat <<EOF

ShareX-Mac has been removed, but macOS does not automatically revoke the
TCC (privacy) permission grants (Screen Recording, Microphone) tied to it.
To revoke them yourself:
  System Settings -> Privacy & Security -> Screen Recording -> remove "ShareX-Mac"
  System Settings -> Privacy & Security -> Microphone -> remove "ShareX-Mac"
This script does not and will not modify TCC itself.
EOF
  exit 0
fi

# ---- install ------------------------------------------------------------
if [ ! -e "$SRC_APP" ]; then
  echo "error: source bundle not found at $SRC_APP" >&2
  echo "       run scripts/build-app.sh first." >&2
  exit 1
fi

echo "==> verifying code signature of $SRC_APP"
if ! codesign --verify --deep --strict --verbose=2 "$SRC_APP"; then
  echo "error: $SRC_APP failed codesign verification; refusing to install an unsigned/broken bundle." >&2
  exit 1
fi

TIMESTAMP="$(date +%Y%m%d-%H%M%S)"
# Backups deliberately do NOT live in $DEST_DIR.
#
# A backup is a complete .app carrying the SAME CFBundleIdentifier as the
# installed one. Keeping copies beside the real install means several bundles
# claim one identity, which makes LaunchServices resolution ambiguous
# (`mdfind "kMDItemCFBundleIdentifier == ..."` returns all of them) and makes it
# genuinely hard to reason about which bundle a privacy grant belongs to.
# Repeated development installs multiply the copies quickly.
BACKUP_ROOT="${SXM_BACKUP_DIR:-$HOME/Library/Application Support/ShareX-Mac/backups}"
mkdir -p "$BACKUP_ROOT"
BACKUP="$BACKUP_ROOT/${APP_NAME}.backup-${TIMESTAMP}"
NEED_BACKUP=0
if [ -e "$DEST_APP" ]; then
  NEED_BACKUP=1
fi

if [ ! -w "$DEST_DIR" ]; then
  echo "error: $DEST_DIR is not writable by $(whoami). Run the following yourself, then re-run this script to see the confirmation output (it will skip straight past the now-empty backup/install steps):" >&2
  if [ "$NEED_BACKUP" -eq 1 ]; then
    echo "  sudo mv \"$DEST_APP\" \"$BACKUP\"" >&2
  fi
  echo "  sudo ditto \"$SRC_APP\" \"$DEST_APP\"" >&2
  exit 1
fi

if [ "$NEED_BACKUP" -eq 1 ]; then
  echo "==> existing install found at $DEST_APP; backing up to $BACKUP"
  mv "$DEST_APP" "$BACKUP"
  echo "    rollback command: mv \"$BACKUP\" \"$DEST_APP\""

  # Keep exactly ONE backup.
  #
  # This matters for more than tidiness: every backup is a full .app carrying the
  # SAME CFBundleIdentifier. macOS keys both LaunchServices resolution and the
  # TCC privacy grant on bundle identity, so accumulating copies makes the
  # Screen Recording permission attach to a bundle that is not the one being
  # launched. Symptom: the user grants permission and the app still reports
  # "permission required". Repeated installs during development hit this fast.
  OLD_BACKUP_COUNT=0
  while IFS= read -r stale; do
    [ -z "$stale" ] && continue
    [ "$stale" = "$BACKUP" ] && continue
    echo "    pruning older backup (duplicate bundle id): $stale"
    rm -rf "$stale"
    OLD_BACKUP_COUNT=$((OLD_BACKUP_COUNT + 1))
  done < <(find "$BACKUP_ROOT" -maxdepth 1 -name "${APP_NAME}.backup-*" 2>/dev/null | sort -r)

  if [ "$OLD_BACKUP_COUNT" -gt 0 ]; then
    echo "    pruned $OLD_BACKUP_COUNT older backup(s) so only one copy of the bundle id remains"
  fi
fi

echo "==> installing to $DEST_APP"
ditto "$SRC_APP" "$DEST_APP"

echo "==> installed: $DEST_APP"
echo "-- codesign -dv --"
codesign -dv "$DEST_APP" 2>&1 || true

cat <<EOF

Reminder: on first launch, macOS will prompt for Screen Recording permission
(System Settings -> Privacy & Security -> Screen Recording). This is a TCC
grant tied to the app's code signature; rebuilding with the same stable
signing identity ("ShareX-Mac Local" by default) keeps the grant across
rebuilds, but ad-hoc signing does not.
EOF
