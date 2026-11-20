@echo off
REM Development launcher: points apphost to the per-user .NET 10 installation.
set "DOTNET_ROOT=%LOCALAPPDATA%\Microsoft\dotnet"
set "PATH=%DOTNET_ROOT%;%PATH%"
cd /d "%~dp0"
start "Skorski Tools" "%~dp0src\SkorskiTools.App\bin\Debug\net10.0-windows\SkorskiTools.App.exe"
