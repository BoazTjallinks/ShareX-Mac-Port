# Entitlements rationale

`packaging/entitlements.plist` must stay **comment-free**: macOS's AMFI
entitlement parser (`AMFIUnserializeXML`) rejects XML comments and `codesign`
fails with "Failed to parse entitlements: syntax error". The justification that
used to live inline in that file is therefore kept here.

Entitlements are applied to the **main executable only**, not to nested dylibs.
Nested libraries are signed (inner-out, before the bundle) but carry no
entitlements of their own; entitlements are a property of the process, and
attaching them to a library is meaningless.

## Granted

### `com.apple.security.cs.allow-jit`

.NET's JIT (RyuJIT) writes executable machine code into memory at run time.
Under the hardened runtime this is blocked unless the process holds `allow-jit`.
Without it the self-contained .NET 10 apphost fails at startup, because it needs
to JIT immediately. This is the one entitlement essentially every
hardened-runtime .NET application needs.

### `com.apple.security.cs.disable-library-validation`

**Required here, and the reason is specific to a self-signed identity.**

This was initially omitted, on the reasoning that library validation only
rejects dylibs signed by a *different* Team ID, and every bundled dylib is
signed in the same build with the same identity as the app. Launching the built
bundle disproved that:

```
Failed to load .../Contents/MacOS/libhostfxr.dylib, error: dlopen(...):
  code signature ... not valid for use in process:
  mapping process and mapped file (non-platform) have different Team IDs
```

The cause: `ShareX-Mac Local` is a self-signed certificate, so the signature has
**no Team ID** (`codesign -dvv` reports `TeamIdentifier=not set`). Library
validation compares Team IDs, and "absent" does not match "absent" — it fails
closed. Every bundled dylib is affected, starting with .NET's own
`libhostfxr.dylib`, so the app cannot start at all without this entitlement.

A real Apple Developer ID would carry a Team ID and would not need this. That is
the trade for not requiring a paid certificate for a personal build, and it is
recorded here rather than left as an unexplained entitlement.

## Considered and deliberately omitted

### `com.apple.security.cs.allow-unsigned-executable-memory`

Permits executing memory that was never signed at all. .NET's JIT on Apple
Silicon uses the standard `mmap(MAP_JIT)` / `pthread_jit_write_protect_np`
pattern, which `allow-jit` already covers. Granting the broader escape hatch
would give up a real part of the hardened runtime's code-integrity guarantee for
no evidenced benefit.

## Not an entitlement

Screen and window capture go through ScreenCaptureKit
(`native/ShareXMacNative/swift/SXMCapture.swift`), which is gated by the system
**Screen Recording** privacy (TCC) permission, not by an entitlement. There is
no screen-recording entitlement to request. Microphone access is likewise a TCC
permission, declared through `NSMicrophoneUsageDescription` in `Info.plist`.

## App Sandbox

Deliberately not enabled (PROJECT-SPEC.md section 12): arbitrary external
actions, watch folders and full filesystem integration are central to ShareX's
behaviour. This does not remove any macOS privacy control.
