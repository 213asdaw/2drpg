#!/usr/bin/env bash
# Stage source assets and run Godot once so .import + .ctex exist.
set -euo pipefail
ROOT="$(cd "$(dirname "$0")/.." && pwd)"
PROJ="$ROOT/godot_pack/import_project"
GODOT_EXE="${GODOT_EXE:-/tmp/godot/Godot_v4.5.1-stable_linux.x86_64}"

rm -rf "$PROJ"
mkdir -p \
  "$PROJ/images/packed/character_select" \
  "$PROJ/JinwooNecrobinderSkin/images/osty" \
  "$PROJ/JinwooNecrobinderSkin/localization/kor" \
  "$PROJ/JinwooNecrobinderSkin/localization/eng" \
  "$PROJ/animations/characters/necrobinder" \
  "$PROJ/animations/characters/osty"

cp "$ROOT/assets/character_select/char_select_necrobinder.png" \
  "$PROJ/images/packed/character_select/"
cp "$ROOT/assets/portraits/necrobinder_portrait.png" \
  "$PROJ/JinwooNecrobinderSkin/images/portrait.png"
cp "$ROOT/assets/igris/igris_portrait.png" \
  "$PROJ/JinwooNecrobinderSkin/images/osty/portrait.png"
cp "$ROOT/assets/igris/igris_fullbody.png" \
  "$PROJ/JinwooNecrobinderSkin/images/osty/fullbody.png"
cp "$ROOT/assets/preview/preview.png" "$PROJ/JinwooNecrobinderSkin/mod_image.png"
cp "$ROOT/assets/preview/preview.png" "$PROJ/mod_image.png"
cp "$ROOT/localization/kor/characters.json" "$PROJ/JinwooNecrobinderSkin/localization/kor/"
cp "$ROOT/localization/eng/characters.json" "$PROJ/JinwooNecrobinderSkin/localization/eng/"
cp "$ROOT/JinwooNecrobinderSkin.json" "$PROJ/mod_manifest.json"
cp "$ROOT/ReplaceResources/necrobinder/README.md" "$PROJ/animations/characters/necrobinder/README_SPINE.txt"
cp "$ROOT/ReplaceResources/osty/README.md" "$PROJ/animations/characters/osty/README_SPINE.txt"

cat > "$PROJ/project.godot" << 'EOF'
config_version=5
[application]
config/name="JinwooImport"
config/features=PackedStringArray("4.5")
EOF

"$GODOT_EXE" --headless --editor --quit --path "$PROJ" >/tmp/jinwoo_godot_import.log 2>&1 || true
# Ensure ctex exists
if [[ ! -f "$PROJ/.godot/imported/char_select_necrobinder.png-05afee48adb7cc146ffabe70c902ec95.ctex" ]] \
   && ! ls "$PROJ/.godot/imported"/char_select_necrobinder.png-*.ctex >/dev/null 2>&1; then
  echo "Godot import failed to produce char_select ctex" >&2
  tail -40 /tmp/jinwoo_godot_import.log >&2 || true
  exit 1
fi
echo "Import project ready: $PROJ"
ls "$PROJ/.godot/imported"/*.ctex
