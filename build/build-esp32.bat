@echo off
setlocal

set "PROJECT_DIR=%~dp0..\firmware-esp32c3-supermini"
set "OUT_DIR=%~dp0..\out\firmware-esp32c3-supermini"
set "PIO_BUILD_DIR=%PROJECT_DIR%\.pio\build\esp32-c3-supermini"

call "%~dp0_find_pio.bat" || exit /b 1

"%PIO_EXE%" run -d "%PROJECT_DIR%"
if not %errorlevel%==0 exit /b %errorlevel%

if not exist "%OUT_DIR%" mkdir "%OUT_DIR%"
copy /y "%PIO_BUILD_DIR%\firmware.bin" "%OUT_DIR%\" >nul
copy /y "%PIO_BUILD_DIR%\bootloader.bin" "%OUT_DIR%\" >nul
copy /y "%PIO_BUILD_DIR%\partitions.bin" "%OUT_DIR%\" >nul
copy /y "%PIO_BUILD_DIR%\firmware.elf" "%OUT_DIR%\" >nul

echo Output copied to "%OUT_DIR%"
exit /b 0
