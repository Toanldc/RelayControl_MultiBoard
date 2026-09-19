@echo off
setlocal

set "OUT_DIR=%~dp0..\out"
set "RELEASE_ROOT=%~dp0..\release"

if not exist "%OUT_DIR%\firmware-nanoatmega328\firmware.hex" (
    echo [ERROR] out\firmware-nanoatmega328 build output not found. Run build-all.bat first.
    exit /b 1
)
if not exist "%OUT_DIR%\firmware-esp32c3-supermini\firmware.bin" (
    echo [ERROR] out\firmware-esp32c3-supermini build output not found. Run build-all.bat first.
    exit /b 1
)
if not exist "%OUT_DIR%\app\RelayControlWPF.exe" (
    echo [ERROR] out\app build output not found. Run build-all.bat first.
    exit /b 1
)

set "VERSION="
set /p VERSION=Enter release version (e.g. 1.0.0):
if "%VERSION%"=="" (
    echo [ERROR] Version cannot be empty.
    exit /b 1
)

set "RELEASE_DIR=%RELEASE_ROOT%\%VERSION%"
if exist "%RELEASE_DIR%" (
    echo [ERROR] Release "%VERSION%" already exists at "%RELEASE_DIR%".
    exit /b 1
)
mkdir "%RELEASE_DIR%"

echo.
echo Packaging firmware-nanoatmega328.zip ...
powershell -NoProfile -Command "Compress-Archive -Path '%OUT_DIR%\firmware-nanoatmega328\*' -DestinationPath '%RELEASE_DIR%\firmware-nanoatmega328.zip'"
if not %errorlevel%==0 exit /b 1

echo Packaging firmware-esp32c3-supermini.zip ...
powershell -NoProfile -Command "Compress-Archive -Path '%OUT_DIR%\firmware-esp32c3-supermini\*' -DestinationPath '%RELEASE_DIR%\firmware-esp32c3-supermini.zip'"
if not %errorlevel%==0 exit /b 1

echo Packaging RelayControlWPF.zip ...
powershell -NoProfile -Command "Compress-Archive -Path '%OUT_DIR%\app\*' -DestinationPath '%RELEASE_DIR%\RelayControlWPF.zip'"
if not %errorlevel%==0 exit /b 1

echo.
echo === Release %VERSION% created at "%RELEASE_DIR%" ===
exit /b 0
