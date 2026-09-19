@echo off
REM Resolves PIO_EXE to a usable PlatformIO executable.
where pio >nul 2>nul
if %errorlevel%==0 (
    set "PIO_EXE=pio"
    exit /b 0
)

if exist "%USERPROFILE%\.platformio\penv\Scripts\pio.exe" (
    set "PIO_EXE=%USERPROFILE%\.platformio\penv\Scripts\pio.exe"
    exit /b 0
)

echo [ERROR] Could not find pio.exe on PATH or in %%USERPROFILE%%\.platformio\penv\Scripts\
echo         Install PlatformIO Core, or install the PlatformIO VSCode extension.
exit /b 1
