#!/usr/bin/env bash
set -euo pipefail
umask 077
mkdir -p /realm /var/lib/krb5kdc /etc/krb5kdc
chmod 755 /realm
cat > /etc/krb5kdc/kdc.conf <<'CONFIG'
[kdcdefaults]
 kdc_ports = 88
 kdc_tcp_ports = 88
[realms]
 DUBURL.TEST = {
  database_name = /var/lib/krb5kdc/principal
  key_stash_file = /var/lib/krb5kdc/stash
  max_life = 1h
  supported_enctypes = aes256-cts-hmac-sha1-96:normal aes128-cts-hmac-sha1-96:normal
 }
CONFIG
kdb5_util create -s -r DUBURL.TEST -P "$(openssl rand -base64 32)" >/dev/null 2>&1
for principal in alice bob; do
  kadmin.local -q "addprinc -randkey $principal@DUBURL.TEST" >/dev/null 2>&1
  kadmin.local -q "ktadd -k /realm/$principal.keytab $principal@DUBURL.TEST" >/dev/null 2>&1
  chown 10001:10001 "/realm/$principal.keytab"
  chmod 400 "/realm/$principal.keytab"
done
for service in mariadb memsql mysql; do
  case "$service" in
    mariadb) host=mariadb;;
    memsql) host=singlestore;;
    mysql) host=mysql;;
    *) echo 'Unexpected Kerberos service.' >&2; exit 1;;
  esac
  principal="$service/$host.fixture.test@DUBURL.TEST"
  kadmin.local -q "addprinc -randkey $principal" >/dev/null 2>&1
  kadmin.local -q "ktadd -k /realm/$host.keytab $principal" >/dev/null 2>&1
  chown 999 "/realm/$host.keytab"
  chmod 400 "/realm/$host.keytab"
  openssl rand -base64 32 > "/realm/$host-root.secret"
  if [[ "$host" = singlestore ]]; then chown 999 /realm/singlestore-root.secret; chmod 400 /realm/singlestore-root.secret; fi
done
openssl req -x509 -newkey rsa:2048 -nodes -keyout /realm/ca.key -out /realm/ca.crt -days 1 -subj /CN=DubUrlKerberosFixtureCA >/dev/null 2>&1
chmod 444 /realm/ca.crt
for host in mariadb singlestore mysql; do
  openssl req -newkey rsa:2048 -nodes -keyout "/realm/$host-tls.key" -out "/realm/$host.csr" -subj "/CN=$host.fixture.test" >/dev/null 2>&1
  printf 'subjectAltName=DNS:%s.fixture.test,DNS:localhost,IP:127.0.0.1\nextendedKeyUsage=serverAuth\n' "$host" > /realm/extensions
  openssl x509 -req -in "/realm/$host.csr" -CA /realm/ca.crt -CAkey /realm/ca.key -CAcreateserial -out "/realm/$host.crt" -days 1 -extfile /realm/extensions >/dev/null 2>&1
  chown 999 "/realm/$host-tls.key"
  chmod 400 "/realm/$host-tls.key"
  chmod 444 "/realm/$host.crt"
done
touch /realm/ready
chmod 444 /realm/ready
exec krb5kdc -n
