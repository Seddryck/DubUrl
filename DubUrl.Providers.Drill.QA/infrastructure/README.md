# Apache Drill QA infrastructure

The existing PowerShell deployment script retains its Docker-based local path. The Drill ODBC driver now requires authenticated HPE distribution, so public GitHub runners compile and discover this contract but explicitly skip its live tests.
