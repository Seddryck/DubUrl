@ECHO OFF
IF "%~1" == "" (SET "directory=/../bin/release/net6.0/.bigdata") ELSE SET "directory=%1%"
ECHO Starting docker container for Apache Drill with mounted directory %directory%:/mnt

docker run -it --name drill -p 8047:8047 -p 31010:31010 -v %directory%:/mnt apache/drill