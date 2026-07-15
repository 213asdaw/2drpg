@echo off
chcp 65001 >nul
title 싱크플랜
cd /d "%~dp0"

if exist "싱크플랜.exe" (
  start "" "싱크플랜.exe"
  exit /b 0
)

if exist "SinkPlan.exe" (
  start "" "SinkPlan.exe"
  exit /b 0
)

REM zip 압축 해제본(win-unpacked) 지원
if exist "싱크플랜.exe" (
  start "" "싱크플랜.exe"
  exit /b 0
)

for %%F in (*portable.exe) do (
  start "" "%%F"
  exit /b 0
)

echo 실행 파일을 찾을 수 없습니다.
echo SinkPlan-*-portable.exe 또는 압축을 해제한 폴더에서 이 파일을 실행하세요.
pause
