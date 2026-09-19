@echo off
setlocal

set "PROJECT_DIR=%~dp0..\firmware-nanoatmega328"
set "OUT_DIR=%~dp0..\out\firmware-nanoatmega328"
set "PIO_BUILD_DIR=%PROJECT_DIR%\.pio\build\nanoatmega328"

call "%~dp0_find_pio.bat" || exit /b 1

"%PIO_EXE%" run -d "%PROJECT_DIR%"
if not %errorlevel%==0 exit /b %errorlevel%

if not exist "%OUT_DIR%" mkdir "%OUT_DIR%"
copy /y "%PIO_BUILD_DIR%\firmware.hex" "%OUT_DIR%\" >nul
copy /y "%PIO_BUILD_DIR%\firmware.elf" "%OUT_DIR%\" >nul

echo Output copied to "%OUT_DIR%"
exit /b 0
