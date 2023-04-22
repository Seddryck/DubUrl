@ECHO OFF
ECHO Starting docker container for PostgreSQL

docker run --name postgresql -p 5432:5432 -e POSTGRES_PASSWORD=Password12! postgres