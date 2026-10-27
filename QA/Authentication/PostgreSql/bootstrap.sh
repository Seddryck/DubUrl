#!/usr/bin/env bash
set -euo pipefail
# The entrypoint initially runs as container root, so host keys can stay mode 600
# in their mode-700 temporary directory. Only server material enters this container.
cp /fixture/server.crt /var/lib/postgresql/server.crt
cp /fixture/server.key /var/lib/postgresql/server.key
cp /fixture/ca.crt /var/lib/postgresql/ca.crt
chown postgres:postgres /var/lib/postgresql/server.crt /var/lib/postgresql/server.key /var/lib/postgresql/ca.crt
chmod 600 /var/lib/postgresql/server.key
exec docker-entrypoint.sh "$@"
