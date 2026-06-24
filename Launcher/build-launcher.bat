@echo off
setlocal
cd /d "%~dp0"
dotnet publish -c Release
echo.
echo Publish folder:
echo %cd%\bin\Release\net8.0-windows\win-x64\publish\
echo Copy launcher-config.json into that folder before sending to friends.
pause
