@echo off
setlocal
"%~dp0loader-setup.exe" --uninstall %*
exit /b %errorlevel%
