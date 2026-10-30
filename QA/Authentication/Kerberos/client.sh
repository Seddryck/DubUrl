#!/usr/bin/env bash
set -euo pipefail
umask 077
export HOME=/home/alice
export KRB5CCNAME=FILE:/tmp/duburl-alice.ccache
export DOTNET_CLI_TELEMETRY_OPTOUT=1
export DUBURL_KERBEROS_REQUIRE_TLS=1
export DUBURL_MARIADB_OS_QA_URL='maria://alice@mariadb.fixture.test/app?SSL%20Mode=VerifyFull&SSL%20CA=%2Frealm%2Fca.crt&ServerSPN=mariadb%2Fmariadb.fixture.test&Pooling=false&Connection%20Timeout=5'
export DUBURL_MARIADB_OS_QA_ACCOUNT='alice@%'
if [[ "${DUBURL_KERBEROS_SINGLESTORE_READY:-0}" = 1 ]]; then
  export DUBURL_SINGLESTORE_OS_QA_URL='singlestore://alice@singlestore.fixture.test/app?SSL%20Mode=VerifyFull&SSL%20CA=%2Frealm%2Fca.crt&ServerSPN=memsql%2Fsinglestore.fixture.test&Pooling=false&Connection%20Timeout=5'
  export DUBURL_SINGLESTORE_OS_QA_ACCOUNT='alice@%'
fi
scenario="${1:-positive}"
case "$scenario" in
  positive) kinit -kt /realm/alice.keytab alice@DUBURL.TEST; filter='FullyQualifiedName~ExistingTicketAuthenticatesConfiguredDatabaseAccount';;
  missing) export KRB5CCNAME=FILE:/tmp/absent-ticket-cache; export DUBURL_KERBEROS_NEGATIVE_SCENARIO=missing; filter='FullyQualifiedName~PluginOnlyAccountRejectsOtherOrMissingTicket';;
  wrong) kinit -kt /realm/bob.keytab bob@DUBURL.TEST; export DUBURL_KERBEROS_NEGATIVE_SCENARIO=wrong; filter='FullyQualifiedName~PluginOnlyAccountRejectsOtherOrMissingTicket';;
  *) exit 2;;
esac
console=$(find /usr/share/dotnet/sdk -path '*/vstest.console.dll' | sort -V | tail -1)
framework="${DUBURL_KERBEROS_TFM:-net10.0}"
case "$framework" in net8.0|net9.0|net10.0) ;; *) exit 2;; esac
dotnet "$console" /tests/DubUrl.Core.Testing.dll --TestCaseFilter:"$filter" \
  --ResultsDirectory:/results --logger:"trx;LogFileName=kerberos-$framework-$scenario.trx"
