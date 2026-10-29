#!/usr/bin/env bash
set -euo pipefail
export MYSQL_PWD="$(cat /realm/mariadb-root.secret)"
plugin=$(mariadb -uroot -Nse "SELECT plugin FROM mysql.user WHERE User='alice' AND Host='%'")
[[ "$plugin" = gssapi ]]
definition=$(mariadb -uroot -Nse "SHOW CREATE USER 'alice'@'%'")
! grep -q ' OR ' <<< "$definition"
grep -q 'REQUIRE SSL' <<< "$definition"
printf 'MariaDB: plugin-only gssapi account and required TLS verified.\n'
