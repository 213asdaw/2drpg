#!/usr/bin/env bash
set -euo pipefail
cd "$(dirname "$0")"

for cand in ./SinkPlan ./sink-plan-3d ./싱크플랜; do
  if [[ -x "$cand" ]]; then
    exec "$cand"
  fi
done

APPIMAGE=$(ls -1 SinkPlan-*.AppImage 2>/dev/null | head -n1 || true)
if [[ -n "${APPIMAGE}" ]]; then
  chmod +x "$APPIMAGE"
  exec "./$APPIMAGE"
fi

echo "실행 파일을 찾을 수 없습니다."
echo "SinkPlan-*-portable.exe (Windows) 또는 AppImage/압축본을 확인하세요."
exit 1
