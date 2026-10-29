FROM mcr.microsoft.com/dotnet/sdk:10.0
RUN apt-get update && apt-get install -y --no-install-recommends dbus gnome-keyring libsecret-1-0 \
    && rm -rf /var/lib/apt/lists/*
COPY run.sh Smoke.csproj Program.cs /smoke/
USER app
ENTRYPOINT ["sh", "/smoke/run.sh"]
