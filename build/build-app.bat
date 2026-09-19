@echo off
setlocal

set "PROJECT=%~dp0..\app\RelayControlWPF\RelayControlWPF.csproj"
set "OUT_DIR=%~dp0..\out\app"

where dotnet >nul 2>nul
if not %errorlevel%==0 (
    echo [ERROR] dotnet SDK not found on PATH. Install .NET SDK from https://dotnet.microsoft.com/
    exit /b 1
)

dotnet publish "%PROJECT%" -c Release -r win-x64 --self-contained false -o "%OUT_DIR%"
exit /b %errorlevel%
