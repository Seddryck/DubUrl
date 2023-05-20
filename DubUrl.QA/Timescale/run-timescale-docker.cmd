@ECHO OFF
ECHO Starting docker container for CockRoachDB

docker run -d ^
          --name=timescale ^
          -e POSTGRES_PASSWORD=Password12! ^
          -p 5432:5432 ^
          timescale/timescaledb:latest-pg15