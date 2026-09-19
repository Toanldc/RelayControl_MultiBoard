@echo off
setlocal

echo === Building firmware-nanoatmega328 ===
call "%~dp0build-nano.bat" || goto :fail

echo.
echo === Building firmware-esp32c3-supermini ===
call "%~dp0build-esp32.bat" || goto :fail

echo.
echo === Building app (RelayControlWPF) ===
call "%~dp0build-app.bat" || goto :fail

echo.
echo === All builds succeeded ===
exit /b 0

:fail
echo.
echo === Build failed ===
exit /b 1
