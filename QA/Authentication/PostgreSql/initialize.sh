#!/usr/bin/env bash
set -euo pipefail
psql --username postgres --dbname postgres -v ON_ERROR_STOP=1 <<'SQL'
CREATE ROLE alice LOGIN;
CREATE DATABASE app OWNER alice;
SQL
cat > "$PGDATA/pg_hba.conf" <<'HBA'
local all all peer
hostssl app alice all cert
host all all all reject
HBA
