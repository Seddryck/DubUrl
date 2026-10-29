FROM mariadb@sha256:d9fc02976678d4236145299c40dc267051681b33489c994f0ec4a097ce4a9521
RUN apt-get update \
    && DEBIAN_FRONTEND=noninteractive apt-get install -y --no-install-recommends \
        mariadb-plugin-gssapi-server \
    && rm -rf /var/lib/apt/lists/*
COPY krb5.conf /etc/krb5.conf
COPY mariadb.sh /fixture/
COPY mariadb.sql /docker-entrypoint-initdb.d/kerberos.sql
ENTRYPOINT ["bash", "/fixture/mariadb.sh"]
