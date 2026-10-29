#!/usr/bin/env bash
set -euo pipefail
if [[ ! -f /data/nodes.hcl ]]; then tar -xzf /server/data.tgz -C /data; fi
for key in gssapi_keytab_path gssapi_principal_name ssl_ca ssl_cert ssl_key; do
  case "$key" in
    gssapi_keytab_path) value=/realm/singlestore.keytab;;
    gssapi_principal_name) value=memsql/singlestore.fixture.test@DUBURL.TEST;;
    ssl_ca) value=/realm/ca.crt;; ssl_cert) value=/realm/singlestore.crt;; ssl_key) value=/realm/singlestore-tls.key;;
  esac
  memsqlctl -jy update-config --all --key "$key" --value "$value" >/dev/null
done
export ROOT_PASSWORD="$(cat /realm/singlestore-root.secret)"
export ENABLE_STUDIO=0
exec /scripts/start.sh
