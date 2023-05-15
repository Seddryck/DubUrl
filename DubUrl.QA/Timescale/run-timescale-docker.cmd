@ECHO OFF
ECHO Starting docker container for TimescaleDB

docker run -d ^
          -e POSTGRES_PASSWORD=Password12! ^
          --name=timescale ^
          -p 5432:5432 ^
          timescale/timescaledb:latest-pg15