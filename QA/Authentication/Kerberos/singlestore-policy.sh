#!/usr/bin/env bash
set -euo pipefail
export MYSQL_PWD="$(cat /realm/singlestore-root.secret)"
singlestore -h127.0.0.1 -uroot --ssl-ca=/realm/ca.crt --ssl-mode=VERIFY_IDENTITY <<'SQL'
CREATE DATABASE app;
CREATE USER 'alice'@'%' IDENTIFIED WITH 'authentication_gss' AS 'alice@DUBURL.TEST' REQUIRE SSL;
GRANT SELECT ON app.* TO 'alice'@'%';
SQL
definition=$(singlestore -h127.0.0.1 -uroot --ssl-ca=/realm/ca.crt --ssl-mode=VERIFY_IDENTITY -Nse "SHOW GRANTS FOR 'alice'@'%'")
grep -q authentication_gss <<< "$definition"
printf 'SingleStore: authentication_gss account created with required TLS.\n'
