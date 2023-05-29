@ECHO OFF
ECHO Starting docker container for Microsoft SQL Server

docker run -d ^
          --name=mssql ^
          -e "ACCEPT_EULA=Y" ^
          -e "MSSQL_SA_PASSWORD=Password12!" ^
          -p 1433:1433 ^
          mcr.microsoft.com/mssql/server:2019-latest