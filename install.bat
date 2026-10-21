@echo off
setlocal
"%~dp0loader-setup.exe" --install %*
exit /b %errorlevel%
