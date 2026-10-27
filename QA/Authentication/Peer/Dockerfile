FROM mcr.microsoft.com/dotnet/sdk:10.0 AS dotnet
FROM postgres:17-bookworm
COPY --from=dotnet /usr/share/dotnet /usr/share/dotnet
ENV PATH="/usr/share/dotnet:${PATH}"
RUN useradd --create-home alice
COPY --chown=alice:alice Directory.Build.props Directory.Build.targets README.md /src/
COPY --chown=alice:alice DubUrl.Core/ /src/DubUrl.Core/
COPY --chown=alice:alice DubUrl.Core.Testing/ /src/DubUrl.Core.Testing/
COPY --chown=alice:alice DubUrl.Application/ /src/DubUrl.Application/
COPY --chown=alice:alice DubUrl.Authentication.SqlServer/ /src/DubUrl.Authentication.SqlServer/
COPY --chown=alice:alice DubUrl.Authentication.Snowflake/ /src/DubUrl.Authentication.Snowflake/
COPY --chown=alice:alice DubUrl.ProviderPack.TestingModule/ /src/DubUrl.ProviderPack.TestingModule/
COPY --chown=alice:alice DubUrl.Adbc/ /src/DubUrl.Adbc/
COPY --chown=alice:alice DubUrl.Adomd/ /src/DubUrl.Adomd/
COPY --chown=alice:alice DubUrl.OleDb/ /src/DubUrl.OleDb/
COPY --chown=alice:alice DubUrl.BulkCopy/ /src/DubUrl.BulkCopy/
COPY --chown=alice:alice DubUrl.Schema/ /src/DubUrl.Schema/
COPY --chown=alice:alice DubUrl.Schema.Documents/ /src/DubUrl.Schema.Documents/
COPY --chown=alice:alice Providers/ /src/Providers/
COPY --chown=alice:alice connectors/ /src/connectors/
COPY --chown=alice:alice build/ /src/build/
COPY --chown=alice:alice misc/icon/ /src/misc/icon/
COPY QA/Authentication/Peer/run.sh /usr/local/libexec/duburl-peer-fixture.sh
WORKDIR /src
ENTRYPOINT ["bash", "/usr/local/libexec/duburl-peer-fixture.sh"]
