@ECHO OFF
IF "%~1" == "" (SET "directory=%~dp0.catalog") ELSE SET "directory=%~f1"
ECHO Starting docker container for Trino with mounted directory %directory%

docker run --name trino -p 8080:8080 -v %directory%:/etc/trino/catalog trinodb/trino:latest