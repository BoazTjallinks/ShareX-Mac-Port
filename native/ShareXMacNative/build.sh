#!/bin/bash
# Builds libShareXMacNative.dylib from the Swift implementation and the
# Objective-C ABI shim. Requires only the Command Line Tools (no full Xcode):
# the ScreenCaptureKit / Vision / AVFoundation frameworks are present in the
# CLT SDK. See planning/adr/0001-native-boundary.md.
set -euo pipefail

HERE="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
OUT="${1:-$HERE/build}"
CONFIG="${SXM_CONFIG:-release}"

SDK="$(xcrun --show-sdk-path)"
DEPLOY_TARGET="${SXM_DEPLOY_TARGET:-15.0}"
TRIPLE="arm64-apple-macos${DEPLOY_TARGET}"

GEN="$OUT/gen"
OBJ="$OUT/obj"
mkdir -p "$GEN" "$OBJ"

SWIFT_OPT="-O"
CLANG_OPT="-O2"
if [ "$CONFIG" = "debug" ]; then
  SWIFT_OPT="-Onone -g"
  CLANG_OPT="-O0 -g"
fi

echo "==> swiftc (${TRIPLE}, ${CONFIG})"
xcrun swiftc \
  -sdk "$SDK" \
  -target "$TRIPLE" \
  -module-name ShareXMacNative \
  -emit-objc-header-path "$GEN/ShareXMacNative-Swift.h" \
  -emit-object -wmo $SWIFT_OPT \
  -swift-version 5 \
  -o "$OBJ/swift.o" \
  "$HERE"/swift/*.swift

echo "==> clang (ABI shim)"
xcrun clang -c \
  -isysroot "$SDK" \
  -target "$TRIPLE" \
  -fobjc-arc -fmodules $CLANG_OPT \
  -Wall -Wextra -Wno-unused-parameter \
  -I "$HERE/include" -I "$GEN" \
  -o "$OBJ/sxm_abi.o" \
  "$HERE/src/sxm_abi.m"

echo "==> link libShareXMacNative.dylib"
xcrun clang -dynamiclib \
  -isysroot "$SDK" \
  -target "$TRIPLE" \
  -install_name "@rpath/libShareXMacNative.dylib" \
  -o "$OUT/libShareXMacNative.dylib" \
  "$OBJ/swift.o" "$OBJ/sxm_abi.o" \
  -L"$SDK/usr/lib/swift" \
  -Xlinker -rpath -Xlinker /usr/lib/swift \
  -framework Foundation \
  -framework AppKit \
  -framework CoreGraphics \
  -framework CoreMedia \
  -framework CoreImage \
  -framework CoreVideo \
  -framework ImageIO \
  -framework UniformTypeIdentifiers \
  -framework ScreenCaptureKit \
  -framework AVFoundation \
  -framework Vision \
  -framework ApplicationServices \
  -framework Carbon \
  -framework Security

echo "==> built $OUT/libShareXMacNative.dylib"
lipo -archs "$OUT/libShareXMacNative.dylib"
