FROM mcr.microsoft.com/dotnet/sdk@sha256:b7e3c0268942d9e80656c5ff9d0514c96741e940cf3acae9f9a8cc1fcc6991be AS runner
FROM mcr.microsoft.com/dotnet/runtime@sha256:b0642831def7f75d1686be15c3232fadd51ea479b12c9b140e4b7a03f9511474 AS runtime8
FROM mcr.microsoft.com/dotnet/runtime@sha256:3562dcf10791d9b1a20097500da37dc5390a1a6c59f56e40806f552cdd7c1666 AS runtime9
FROM mcr.microsoft.com/dotnet/sdk@sha256:e1ffd2a92ae84c1291bc1b6887501f8af98e6331e7af6d4c8d37168c5e87a64c
RUN apt-get update \
    && DEBIAN_FRONTEND=noninteractive apt-get install -y --no-install-recommends \
        krb5-kdc krb5-admin-server krb5-user openssl mariadb-client \
    && rm -rf /var/lib/apt/lists/* \
    && useradd --uid 10001 --create-home alice
COPY --from=runner /usr/share/dotnet/host /usr/share/dotnet/host
COPY --from=runner /usr/share/dotnet/shared /usr/share/dotnet/shared
COPY --from=runtime8 /usr/share/dotnet/shared /usr/share/dotnet/shared
COPY --from=runtime9 /usr/share/dotnet/shared /usr/share/dotnet/shared
COPY --from=runner /usr/share/dotnet/sdk /usr/share/dotnet/sdk
COPY --from=runner /usr/share/dotnet/dotnet /usr/share/dotnet/dotnet
COPY krb5.conf /etc/krb5.conf
COPY kdc.sh client.sh /fixture/
WORKDIR /results
