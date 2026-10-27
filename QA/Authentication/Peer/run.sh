#!/usr/bin/env bash
set -euo pipefail
export PGDATA=/tmp/duburl-peer-postgres
mkdir -p "$PGDATA" /tmp/duburl-peer-socket
chown postgres:postgres "$PGDATA" /tmp/duburl-peer-socket
gosu postgres initdb --auth-local=trust --auth-host=reject >/dev/null
gosu postgres pg_ctl -o "-k /tmp/duburl-peer-socket -h ''" -w start >/dev/null
trap 'gosu postgres pg_ctl -m immediate stop >/dev/null' EXIT
gosu postgres psql -h /tmp/duburl-peer-socket -v ON_ERROR_STOP=1 <<'SQL'
CREATE ROLE alice LOGIN;
CREATE ROLE mapped_alice LOGIN;
CREATE ROLE bob LOGIN;
CREATE DATABASE app;
SQL
printf 'local all all peer map=fixture\nhost all all all reject\n' > "$PGDATA/pg_hba.conf"
printf 'fixture alice alice\nfixture alice mapped_alice\nfixture postgres postgres\n' > "$PGDATA/pg_ident.conf"
gosu postgres pg_ctl reload >/dev/null
export DUBURL_PEER_URL='postgres://alice@localhost/app?Host=%2Ftmp%2Fduburl-peer-socket&Pooling=false'
export DOTNET_CLI_TELEMETRY_OPTOUT=1
gosu alice dotnet test DubUrl.Core.Testing/DubUrl.Core.Testing.csproj -f net10.0 --filter FullyQualifiedName~UnixSocketPeerAuthenticatesMappedRole
