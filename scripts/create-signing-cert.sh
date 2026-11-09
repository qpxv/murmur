#!/bin/bash
# one-time: creates a self-signed code signing certificate "Murmur Local Signing" in the
# login keychain. a stable signature lets macos remember mic + accessibility permissions
# across rebuilds. remove later with: security delete-identity -c "Murmur Local Signing"
set -euo pipefail

NAME="Murmur Local Signing"
VALID_DAYS=3650

if security find-certificate -c "$NAME" >/dev/null 2>&1; then
    echo "'$NAME' already exists"
    exit 0
fi

WORK="$(mktemp -d)"
trap 'rm -rf "$WORK"' EXIT

cat >"$WORK/cert.cnf" <<EOF
[req]
distinguished_name = dn
x509_extensions = ext
prompt = no
[dn]
CN = $NAME
[ext]
basicConstraints = critical,CA:false
keyUsage = critical,digitalSignature
extendedKeyUsage = critical,codeSigning
EOF

/usr/bin/openssl req -x509 -newkey rsa:2048 -nodes -days "$VALID_DAYS" \
    -keyout "$WORK/key.pem" -out "$WORK/cert.pem" -config "$WORK/cert.cnf"
/usr/bin/openssl pkcs12 -export -inkey "$WORK/key.pem" -in "$WORK/cert.pem" \
    -out "$WORK/cert.p12" -passout pass:murmur
security import "$WORK/cert.p12" -k ~/Library/Keychains/login.keychain-db \
    -P murmur -T /usr/bin/codesign

echo "created '$NAME'"
