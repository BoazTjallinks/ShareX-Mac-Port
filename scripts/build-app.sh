#!/bin/bash
# Assembles build/ShareX-Mac.app: native dylib -> dotnet publish -> layout ->
# codesign (inner-out) -> verify. See packaging/README.md for the full
# rationale. Do not silently produce an empty/bogus bundle: every stage that
# depends on something that does not exist yet must fail loudly and name the
# missing path.
set -euo pipefail

HERE="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "$HERE/.." && pwd)"

# ---- flags -----------------------------------------------------------------
CONFIG="Release"
DO_NATIVE=1
OUT_DIR="$REPO_ROOT/build"

while [ $# -gt 0 ]; do
  case "$1" in
    --config)
      CONFIG="$2"; shift 2 ;;
    --config=*)
      CONFIG="${1#*=}"; shift ;;
    --no-native)
      DO_NATIVE=0; shift ;;
    --out)
      OUT_DIR="$2"; shift 2 ;;
    --out=*)
      OUT_DIR="${1#*=}"; shift ;;
    -h|--help)
      cat <<USAGE
Usage: $(basename "$0") [--config Debug|Release] [--no-native] [--out <dir>]

Env overrides:
  SXM_APP_PROJECT   path to ShareX.Mac.App.csproj (default: src/ShareX.Mac.App/ShareX.Mac.App.csproj)
  SXM_SIGN_IDENTITY codesign identity (default: "ShareX-Mac Local"; falls back to ad-hoc "-" with a warning if absent)
  SXM_BUNDLE_ID     CFBundleIdentifier (default: com.tjallinks.sharexmac)
  SXM_VERSION       CFBundleShortVersionString (default: 21.0.0)
  SXM_BUILD         CFBundleVersion (default: 1)
  SXM_MIN_OS        LSMinimumSystemVersion (default: 15.0)
USAGE
      exit 0 ;;
    *)
      echo "error: unknown argument: $1" >&2
      exit 1 ;;
  esac
done

case "$CONFIG" in
  Debug|Release) ;;
  *) echo "error: --config must be Debug or Release, got '$CONFIG'" >&2; exit 1 ;;
esac

APP_PROJECT="${SXM_APP_PROJECT:-$REPO_ROOT/src/ShareX.Mac.App/ShareX.Mac.App.csproj}"
SIGN_IDENTITY="${SXM_SIGN_IDENTITY:-ShareX-Mac Local}"
BUNDLE_ID="${SXM_BUNDLE_ID:-com.tjallinks.sharexmac}"
VERSION="${SXM_VERSION:-21.0.0}"
BUILD_NUM="${SXM_BUILD:-1}"
MIN_OS="${SXM_MIN_OS:-15.0}"
RID="osx-arm64"
EXECUTABLE="ShareX-Mac"

APP_BUNDLE="$OUT_DIR/ShareX-Mac.app"
CONTENTS="$APP_BUNDLE/Contents"
MACOS_DIR="$CONTENTS/MacOS"
RESOURCES_DIR="$CONTENTS/Resources"
STAGING="$OUT_DIR/_publish-staging"
NATIVE_BUILD_DIR="$REPO_ROOT/native/ShareXMacNative/build"

echo "==> ShareX-Mac.app build (config=$CONFIG, out=$OUT_DIR)"

# ---- 1. native dylib ---------------------------------------------------
if [ "$DO_NATIVE" -eq 1 ]; then
  echo "==> [1/5] building native dylib via native/ShareXMacNative/build.sh"
  SXM_CONFIG="$(echo "$CONFIG" | tr '[:upper:]' '[:lower:]')" \
    "$REPO_ROOT/native/ShareXMacNative/build.sh" "$NATIVE_BUILD_DIR"
else
  echo "==> [1/5] skipping native build (--no-native)"
fi

DYLIB_SRC="$NATIVE_BUILD_DIR/libShareXMacNative.dylib"
if [ ! -f "$DYLIB_SRC" ]; then
  echo "error: native library not found at $DYLIB_SRC" >&2
  echo "       run native/ShareXMacNative/build.sh first, or drop --no-native." >&2
  exit 1
fi

# ---- 2. dotnet publish --------------------------------------------------
echo "==> [2/5] dotnet publish $APP_PROJECT"
if [ ! -f "$APP_PROJECT" ]; then
  cat >&2 <<EOF
error: the app project does not exist yet:
       $APP_PROJECT

This script assembles ShareX-Mac.app from an already-existing
ShareX.Mac.App project (the Avalonia executable host), but that project has
not been created in this repository yet. Nothing was published and no
bundle was produced.

To fix:
  - If another agent/session is adding src/ShareX.Mac.App, wait for that to land.
  - If the project lives at a different path, re-run with:
      SXM_APP_PROJECT=/path/to/ShareX.Mac.App.csproj $(basename "$0") ...
EOF
  exit 1
fi

rm -rf "$STAGING"
mkdir -p "$STAGING"

dotnet publish "$APP_PROJECT" \
  -c "$CONFIG" \
  -r "$RID" \
  --self-contained true \
  -o "$STAGING"

# Find the apphost: dotnet publish produces an executable named after the
# project's AssemblyName (default: project file base name) with no extension
# on macOS.
PROJECT_BASENAME="$(basename "$APP_PROJECT" .csproj)"
APPHOST_SRC="$STAGING/$PROJECT_BASENAME"
if [ ! -x "$APPHOST_SRC" ]; then
  echo "error: expected apphost at $APPHOST_SRC after publish, but it is missing or not executable." >&2
  echo "       contents of $STAGING:" >&2
  ls -la "$STAGING" >&2 || true
  exit 1
fi

# ---- 3. layout ------------------------------------------------------------
echo "==> [3/5] laying out $APP_BUNDLE"
rm -rf "$APP_BUNDLE"
mkdir -p "$MACOS_DIR" "$RESOURCES_DIR"

# All published managed assemblies + native deps go into Contents/MacOS.
ditto "$STAGING" "$MACOS_DIR"
# The apphost itself must be named after CFBundleExecutable.
if [ "$PROJECT_BASENAME" != "$EXECUTABLE" ]; then
  mv "$MACOS_DIR/$PROJECT_BASENAME" "$MACOS_DIR/$EXECUTABLE"
fi
chmod +x "$MACOS_DIR/$EXECUTABLE"

# libShareXMacNative.dylib: make sure the freshly-built one is what ships,
# not a stale copy dotnet publish may have picked up as content.
cp -f "$DYLIB_SRC" "$MACOS_DIR/libShareXMacNative.dylib"

# Icon.
ICNS_SRC="$REPO_ROOT/packaging/ShareX-Mac.icns"
if [ -f "$ICNS_SRC" ]; then
  cp -f "$ICNS_SRC" "$RESOURCES_DIR/ShareX-Mac.icns"
  ICON_NAME="ShareX-Mac"
else
  echo "warning: $ICNS_SRC not found; run packaging/make-icns.sh first. Bundle will have no icon." >&2
  ICON_NAME=""
fi

# Info.plist: token substitution.
PLIST_TEMPLATE="$REPO_ROOT/packaging/Info.plist.template"
if [ ! -f "$PLIST_TEMPLATE" ]; then
  echo "error: $PLIST_TEMPLATE not found." >&2
  exit 1
fi
sed \
  -e "s/@@VERSION@@/$VERSION/g" \
  -e "s/@@BUILD@@/$BUILD_NUM/g" \
  -e "s/@@EXECUTABLE@@/$EXECUTABLE/g" \
  -e "s/@@BUNDLE_ID@@/$BUNDLE_ID/g" \
  -e "s/@@ICON@@/$ICON_NAME/g" \
  -e "s/@@MIN_OS@@/$MIN_OS/g" \
  "$PLIST_TEMPLATE" > "$CONTENTS/Info.plist"

printf 'APPL????' > "$CONTENTS/PkgInfo"

# ---- 4. codesign: inner binaries first, bundle last ------------------------
echo "==> [4/5] codesign"

if ! security find-identity -v -p codesigning 2>/dev/null | grep -q "$SIGN_IDENTITY"; then
  echo "warning: signing identity '$SIGN_IDENTITY' not found in keychain; falling back to ad-hoc signing (-)." >&2
  echo "         Ad-hoc signatures re-prompt for TCC permissions on every rebuild." >&2
  SIGN_IDENTITY="-"
fi

ENTITLEMENTS="$REPO_ROOT/packaging/entitlements.plist"

sign_one() {
  local target="$1"
  codesign --force --options runtime --entitlements "$ENTITLEMENTS" \
    --sign "$SIGN_IDENTITY" "$target"
}

# Nested code first: every .dylib/.so, then the native lib explicitly (it is
# already covered by the glob below, listed separately only for clarity),
# then the apphost, then finally the outer bundle.
while IFS= read -r -d '' lib; do
  echo "    signing $lib"
  sign_one "$lib"
done < <(find "$MACOS_DIR" \( -name '*.dylib' -o -name '*.so' \) -print0)

echo "    signing apphost $MACOS_DIR/$EXECUTABLE"
sign_one "$MACOS_DIR/$EXECUTABLE"

echo "    signing bundle $APP_BUNDLE"
sign_one "$APP_BUNDLE"

# ---- 5. verify --------------------------------------------------------
echo "==> [5/5] verify"
codesign --verify --deep --strict --verbose=2 "$APP_BUNDLE"

echo "-- lipo -archs (apphost) --"
lipo -archs "$MACOS_DIR/$EXECUTABLE" || true

echo "-- lipo -archs (native dylib) --"
lipo -archs "$MACOS_DIR/libShareXMacNative.dylib" || true

echo "-- otool -L (native dylib resolves) --"
otool -L "$MACOS_DIR/libShareXMacNative.dylib"

echo "==> built and signed $APP_BUNDLE"
