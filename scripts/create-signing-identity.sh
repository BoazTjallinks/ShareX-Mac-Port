#!/bin/bash
# Creates a self-signed CODE SIGNING certificate in the user's LOGIN keychain so
# ShareX-Mac.app keeps a stable code identity across rebuilds. macOS binds the
# Screen Recording / Microphone (TCC) grant to that identity; with ad-hoc signing
# every rebuild changes the hash and macOS re-prompts.
#
# Scope of change: ONE keypair + certificate in ~/Library/Keychains/login.keychain-db,
# trusted for code signing in the USER trust domain only. No system keychain, no
# admin rights, no change to SIP/Gatekeeper/TCC. Nothing leaves this machine.
#
# To undo: open Keychain Access → login → delete "ShareX-Mac Local"
#          (or: security delete-certificate -c "ShareX-Mac Local" ~/Library/Keychains/login.keychain-db)
set -euo pipefail

CN="${SXM_SIGN_CN:-ShareX-Mac Local}"
KEYCHAIN="$HOME/Library/Keychains/login.keychain-db"
WORK="$(mktemp -d)"
trap 'rm -rf "$WORK"' EXIT

if security find-identity -v -p codesigning 2>/dev/null | grep -q "$CN"; then
  echo "Identity \"$CN\" already exists; nothing to do."
  exit 0
fi

cat > "$WORK/openssl.cnf" <<CNF
[req]
distinguished_name = dn
x509_extensions    = v3
prompt             = no
[dn]
CN = $CN
O  = ShareX-Mac (personal local build)
[v3]
basicConstraints     = critical,CA:false
keyUsage             = critical,digitalSignature
extendedKeyUsage     = critical,codeSigning
subjectKeyIdentifier = hash
CNF

echo "==> generating keypair and self-signed code-signing certificate"
openssl req -x509 -newkey rsa:2048 -nodes -sha256 -days 3650 \
  -keyout "$WORK/key.pem" -out "$WORK/cert.pem" -config "$WORK/openssl.cnf" 2>/dev/null

# macOS "security import" rejects an empty-password PKCS#12 MAC, so the transfer
# bundle gets a random one-shot passphrase. It lives only in this temp dir, which
# is removed on exit; the private key itself ends up protected by the login keychain.
P12PASS="$(openssl rand -hex 20)"
openssl pkcs12 -export -inkey "$WORK/key.pem" -in "$WORK/cert.pem" \
  -name "$CN" -out "$WORK/identity.p12" -passout "pass:$P12PASS"

echo "==> importing into the login keychain"
# -T /usr/bin/codesign lets codesign use the key without a per-signature prompt.
security import "$WORK/identity.p12" -k "$KEYCHAIN" -P "$P12PASS" \
  -T /usr/bin/codesign -T /usr/bin/security >/dev/null

echo "==> trusting it for code signing (user trust domain only)"
security add-trusted-cert -p codeSign -k "$KEYCHAIN" "$WORK/cert.pem"

# Allow codesign to use the imported key non-interactively from now on.
security set-key-partition-list -S apple-tool:,apple:,codesign: -s -k "" "$KEYCHAIN" >/dev/null 2>&1 || true

echo "==> result"
security find-identity -v -p codesigning
