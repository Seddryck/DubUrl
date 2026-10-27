#!/usr/bin/env bash
set -euo pipefail
# Generated private keys stay in a new temporary directory outside the repository.
fixture_dir=$(mktemp -d "${TMPDIR:-/tmp}/duburl-certificate-fixture.XXXXXXXX")
chmod 700 "$fixture_dir"
umask 077
openssl req -x509 -newkey rsa:2048 -nodes -keyout "$fixture_dir/ca.key" -out "$fixture_dir/ca.crt" -days 2 -subj /CN=DubUrlFixtureCA >/dev/null 2>&1
for identity in server alice wrongidentity; do
  openssl req -newkey rsa:2048 -nodes -keyout "$fixture_dir/$identity.key" -out "$fixture_dir/$identity.csr" -subj "/CN=$identity" >/dev/null 2>&1
  if [ "$identity" = server ]; then
    printf 'subjectAltName=DNS:localhost,DNS:postgres\nextendedKeyUsage=serverAuth\n' > "$fixture_dir/extensions"
  else
    printf 'extendedKeyUsage=clientAuth\n' > "$fixture_dir/extensions"
  fi
  openssl x509 -req -in "$fixture_dir/$identity.csr" -CA "$fixture_dir/ca.crt" -CAkey "$fixture_dir/ca.key" -CAcreateserial -out "$fixture_dir/$identity.crt" -days 1 -extfile "$fixture_dir/extensions" >/dev/null 2>&1
done
printf '%s\n' "$fixture_dir"
