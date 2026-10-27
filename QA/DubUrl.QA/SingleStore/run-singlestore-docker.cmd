@echo off
pwsh -NoProfile -File "%~dp0deploy-singlestore-test-env.ps1" %*
exit /b %errorlevel%
