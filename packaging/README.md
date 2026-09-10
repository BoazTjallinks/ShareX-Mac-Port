# Packaging ShareX-Mac

## Build

```sh
./packaging/make-icns.sh          # produces packaging/ShareX-Mac.icns (idempotent)
./scripts/build-app.sh            # -> build/ShareX-Mac.app (default: Release)
./scripts/build-app.sh --config Debug --out build-debug
./scripts/build-app.sh --no-native   # skip step 1, reuse existing native/ShareXMacNative/build/libShareXMacNative.dylib
```

`build-app.sh` requires `src/ShareX.Mac.App/ShareX.Mac.App.csproj` to exist
(the Avalonia executable host project). Until that project lands, the script
fails fast with the exact missing path rather than producing an empty or
broken bundle — this is verified behavior, not a guess.

## Install

```sh
./scripts/install-app.sh              # copies build/ShareX-Mac.app -> /Applications
./scripts/install-app.sh --uninstall  # removes it, tells you how to revoke TCC grants yourself
```

Never runs `sudo`. If `/Applications` is not writable, it prints the exact
`sudo ditto ...` (and, if a previous install exists, `sudo mv ... .backup-...`)
commands for you to run by hand.

## What gets signed, in what order, and why

Apple's codesign model requires nested code to be signed *before* the outer
bundle that embeds it — the outer signature seals a manifest of the nested
code's hashes, so signing outside-in produces an outer signature that is
immediately invalid. `scripts/build-app.sh` therefore signs, in order:

1. every `.dylib`/`.so` under `Contents/MacOS` (this currently means just
   `libShareXMacNative.dylib`, but the loop is generic),
2. the apphost executable (`Contents/MacOS/ShareX-Mac`),
3. the `.app` bundle itself, last.

Each `codesign` call passes `--options runtime` (hardened runtime — required
before entitlements can even be attached) and
`--entitlements packaging/entitlements.plist`.

Verification (`codesign --verify --deep --strict --verbose=2`) then walks
the whole bundle and would fail if signing order, or any signature, were
wrong.

## Signing identity

Defaults to `ShareX-Mac Local` (a self-signed local keychain identity — see
`scripts/create-signing-identity.sh`), overridable via `SXM_SIGN_IDENTITY`.
If that identity is not found in the keychain, the script falls back to
ad-hoc signing (`-`) with a loud warning. **A rebuild only keeps its TCC
permission grants (Screen Recording, Microphone) if the signing identity
used is stable across builds** — TCC ties the grant to the code signature.
Ad-hoc (`-`) signatures are derived from the binary's own hash, so every
rebuild changes the "identity" and macOS re-prompts. `ShareX-Mac Local`,
being a real keychain identity reused build after build, does not have this
problem.

## Entitlements

`packaging/entitlements.plist` is intentionally minimal because the app is
**not** App Sandboxed (see PROJECT-SPEC.md section 12):

- `com.apple.security.cs.allow-jit` — required for the .NET 10 JIT to write
  executable memory under the hardened runtime.

Two commonly-cargo-culted entitlements are deliberately **not** requested,
with the reasoning recorded inline in the plist itself:
`com.apple.security.cs.allow-unsigned-executable-memory` (.NET's JIT uses
the standard `MAP_JIT` pattern, which `allow-jit` already covers) and
`com.apple.security.cs.disable-library-validation` (the only bundled dylib
is signed with the same identity as the app in the same build, so library
validation already passes without disabling it).

Screen Recording has no Info.plist usage-description key or entitlement of
its own on macOS — it is gated purely by the system TCC prompt — so nothing
extra is needed for `ScreenCaptureKit`. Microphone access does need an
Info.plist string, which `packaging/Info.plist.template` sets via
`NSMicrophoneUsageDescription`. No `NSAppleEventsUsageDescription` is
declared: nothing in `native/ShareXMacNative` sends Apple Events to other
applications.

## Rollback

`install-app.sh` never deletes an existing install. It renames it to
`/Applications/ShareX-Mac.app.backup-<timestamp>` and prints the exact
`mv` command to restore it:

```sh
mv "/Applications/ShareX-Mac.app.backup-<timestamp>" "/Applications/ShareX-Mac.app"
```

`--uninstall` removes the currently-installed app only (not any
`.backup-*` copies) and reminds you that TCC grants (Screen Recording,
Microphone) are not revoked automatically — do that yourself in
System Settings > Privacy & Security.
