# Apache Drill QA infrastructure

The existing PowerShell deployment script retains its Docker-based local path. On GitHub Actions it starts a pinned Apache Drill distribution directly on Windows, installs the existing MapR ODBC driver, and runs this provider-owned suite.
