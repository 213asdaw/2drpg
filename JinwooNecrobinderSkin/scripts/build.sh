#!/usr/bin/env bash
set -euo pipefail
ROOT="$(cd "$(dirname "$0")/.." && pwd)"
export PATH="${HOME}/.dotnet:${PATH}"
cd "$ROOT"

echo "==> Generating card textures"
python3 "$ROOT/scripts/generate_card_textures.py"

# Also drop char-select / companion art into CustomCardTextures for CustomCardTextureLoader(SG)
CCT="$ROOT/release/mods/CustomCardTextures"
mkdir -p "$CCT/necrobinder" "$CCT"
cp -f "$ROOT/assets/character_select/char_select_necrobinder.png" "$CCT/char_select_necrobinder.png"
cp -f "$ROOT/assets/igris/igris_portrait.png" "$CCT/necrobinder/osty.png" 2>/dev/null || true
cp -f "$ROOT/assets/igris/igris_fullbody.png" "$CCT/necrobinder/osty_full.png" 2>/dev/null || true

echo "==> Building DLL (stubs unless STS2_DIR set)"
dotnet build JinwooNecrobinderSkin.csproj -c Release || true
rm -f "$ROOT/release/mods/JinwooNecrobinderSkin/JinwooNecrobinderSkin.dll"

echo "==> Packing PCK with Godot .ctex imports"
bash "$ROOT/scripts/pack_pck.sh"

echo "Done."
ls -la "$ROOT/release/mods/JinwooNecrobinderSkin"
ls "$CCT/necrobinder" | wc -l
ls -la "$CCT/char_select_necrobinder.png"
