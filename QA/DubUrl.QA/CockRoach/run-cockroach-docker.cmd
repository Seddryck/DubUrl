@ECHO OFF
ECHO Starting docker container for CockRoachDB

docker run -d ^
          --env COCKROACH_DATABASE=DubUrl ^
          --name=roach-single ^
          -p 26257:26257 -p 8080:8080 ^
          cockroachdb/cockroach:latest start-single-node --insecure