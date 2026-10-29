#!/usr/bin/env bash
set -euo pipefail
export MARIADB_ROOT_PASSWORD_FILE=/realm/mariadb-root.secret
exec docker-entrypoint.sh mariadbd --skip-name-resolve --bind-address=0.0.0.0 \
  --plugin-load-add=auth_gssapi --gssapi-keytab-path=/realm/mariadb.keytab \
  --gssapi-principal-name=mariadb/mariadb.fixture.test@DUBURL.TEST \
  --ssl-ca=/realm/ca.crt --ssl-cert=/realm/mariadb.crt --ssl-key=/realm/mariadb-tls.key
