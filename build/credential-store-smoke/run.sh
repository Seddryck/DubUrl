#!/bin/sh
set -eu
# This runner is exclusively for the disposable container image in this directory.
# Its default collection belongs to the container, never the host's desktop session.
mkdir -p /tmp/duburl-credential-smoke
cd /tmp/duburl-credential-smoke
cp /smoke/Smoke.csproj ./
dotnet build Smoke.csproj --verbosity quiet
dbus-run-session -- sh -eu -c '
    head -c 32 /dev/urandom | base64 | gnome-keyring-daemon --unlock --components=secrets >/dev/null
    export DUBURL_TEST_NATIVE_CREDENTIALS=1
    export DUBURL_TEST_ISOLATED_SECRET_SERVICE=1
    # BAMCIS/NReco requires a default environment username even before credentials are configured.
    export USER=duburl-smoke USERNAME=duburl-smoke
    dotnet bin/Debug/net10.0/Smoke.dll
'
# A fresh CLI process must report an unusable session as a store error, not absence.
set +e
DBUS_SESSION_BUS_ADDRESS=unix:path=/tmp/duburl-no-session \
    dotnet /workspace/DubUrl.Cli/bin/Debug/net10.0/DubUrl.Cli.dll credential exists test-absent \
    >/tmp/headless-output 2>/tmp/headless-error
result=$?
set -e
test "$result" -eq 10
test ! -s /tmp/headless-output
grep -q 'OS credential store unavailable' /tmp/headless-error
echo 'PASS MissingSessionReturnsStoreError'
