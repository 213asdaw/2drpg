#!/usr/bin/env bash
set -euo pipefail
ROOT="$(cd "$(dirname "$0")/.." && pwd)"
export PATH="${HOME}/.dotnet:${PATH}"
cd "$ROOT"

echo "==> Generating card textures"
python3 "$ROOT/scripts/generate_card_textures.py"

echo "==> Building JinwooNecrobinderSkin.dll (optional stubs / real STS2_DIR)"
dotnet build JinwooNecrobinderSkin.csproj -c Release || true
# Prefer not shipping stub DLL
rm -f "$ROOT/release/mods/JinwooNecrobinderSkin/JinwooNecrobinderSkin.dll"

echo "==> Packing PCK"
bash "$ROOT/scripts/pack_pck.sh"

echo "Done. Install from: $ROOT/release/mods/"
ls -la "$ROOT/release/mods/JinwooNecrobinderSkin"
ls "$ROOT/release/mods/CustomCardTextures/necrobinder" | wc -l
