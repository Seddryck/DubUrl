@ECHO OFF
ECHO Starting docker container for QuestDB

docker run --name questdb -p 9000:9000 -p 9009:9009 -p 8812:8812 -p 9003:9003 questdb/questdb:latest