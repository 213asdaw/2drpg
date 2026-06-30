#!/usr/bin/env bash
set -euo pipefail
ROOT="$(cd "$(dirname "$0")" && pwd)"
JAR="$ROOT/untitled-1.0-SNAPSHOT.jar"
OUT="$ROOT/decompiled"
CFR="${ROOT}/../tools/cfr.jar"

if [[ ! -f "$JAR" ]]; then
  echo "JAR 없음: $JAR"
  echo "서버 plugins/untitled-1.0-SNAPSHOT.jar 를 복사하거나 채팅에 첨부하세요."
  exit 1
fi

mkdir -p "$OUT"
java -jar "$CFR" "$JAR" --outputdir "$OUT" --silent true

echo "=== 검기 / 아이템 변경 관련 코드 ==="
rg -n "검기|setItemInMainHand|setItem|ItemStack|using_skill|불꽃" "$OUT" || true

echo "Decompiled -> $OUT"
