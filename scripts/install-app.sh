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
BACKUP="$DEST_DIR/${APP_NAME}.backup-${TIMESTAMP}"
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
