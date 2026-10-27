FROM mcr.microsoft.com/dotnet/sdk:10.0 AS dotnet
FROM postgres:17-bookworm
COPY --from=dotnet /usr/share/dotnet /usr/share/dotnet
ENV PATH="/usr/share/dotnet:${PATH}"
RUN useradd --create-home alice
COPY --chown=alice:alice . /src
WORKDIR /src
ENTRYPOINT ["bash", "QA/Authentication/Peer/run.sh"]
