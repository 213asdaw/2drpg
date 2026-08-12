#!/usr/bin/env bash
set -euo pipefail
ROOT="$(cd "$(dirname "$0")/.." && pwd)"
OUT_DIR="$ROOT/release/mods/JinwooNecrobinderSkin"
STAGE="$ROOT/godot_pack/stage"
PCK="$OUT_DIR/JinwooNecrobinderSkin.pck"
GODOT_PCKTOOL="${GODOT_PCKTOOL:-/tmp/tools/godotpcktool}"
GODOT_EXE="${GODOT_EXE:-/tmp/godot/Godot_v4.5.1-stable_linux.x86_64}"

mkdir -p "$OUT_DIR"
rm -rf "$STAGE"
mkdir -p "$STAGE"

cp "$ROOT/JinwooNecrobinderSkin.json" "$OUT_DIR/JinwooNecrobinderSkin.json"
cp "$ROOT/assets/preview/preview.png" "$OUT_DIR/preview.png"

mkdir -p \
  "$STAGE/images/packed/character_select" \
  "$STAGE/JinwooNecrobinderSkin/localization/kor" \
  "$STAGE/JinwooNecrobinderSkin/localization/eng" \
  "$STAGE/JinwooNecrobinderSkin/images" \
  "$STAGE/animations/characters/necrobinder" \
  "$STAGE/animations/characters/osty" \
  "$STAGE/JinwooNecrobinderSkin/images/osty"

cp "$ROOT/assets/character_select/char_select_necrobinder.png" \
  "$STAGE/images/packed/character_select/char_select_necrobinder.png"
cp "$ROOT/assets/portraits/necrobinder_portrait.png" \
  "$STAGE/JinwooNecrobinderSkin/images/portrait.png"
cp "$ROOT/assets/igris/igris_portrait.png" \
  "$STAGE/JinwooNecrobinderSkin/images/osty/portrait.png"
cp "$ROOT/assets/igris/igris_fullbody.png" \
  "$STAGE/JinwooNecrobinderSkin/images/osty/fullbody.png"
cp "$ROOT/localization/kor/characters.json" \
  "$STAGE/JinwooNecrobinderSkin/localization/kor/characters.json"
cp "$ROOT/localization/eng/characters.json" \
  "$STAGE/JinwooNecrobinderSkin/localization/eng/characters.json"
cp "$ROOT/JinwooNecrobinderSkin.json" "$STAGE/mod_manifest.json"
cp "$ROOT/assets/preview/preview.png" "$STAGE/JinwooNecrobinderSkin/mod_image.png"
cp "$ROOT/assets/preview/preview.png" "$STAGE/mod_image.png"
cp "$ROOT/ReplaceResources/necrobinder/README.md" \
  "$STAGE/animations/characters/necrobinder/README_SPINE.txt"
cp "$ROOT/ReplaceResources/osty/README.md" \
  "$STAGE/animations/characters/osty/README_SPINE.txt"

rm -f "$PCK"

if [[ -x "$GODOT_PCKTOOL" ]]; then
  echo "Packing with godotpcktool..."
  CMD_JSON="$ROOT/godot_pack/commands.json"
  python3 - << PY
import json
from pathlib import Path
stage = Path("$STAGE")
cmds = []
for path in sorted(stage.rglob("*")):
    if path.is_file():
        rel = path.relative_to(stage).as_posix()
        cmds.append({"file": str(path), "target": rel})
Path("$CMD_JSON").write_text(json.dumps(cmds, indent=2), encoding="utf-8")
print(f"queued {len(cmds)} files")
PY
  "$GODOT_PCKTOOL" "$PCK" --action add --command-file "$CMD_JSON" --set-godot-version 4.5.0 -q
  echo "Wrote $PCK"
  "$GODOT_PCKTOOL" "$PCK" --action list | head -50
  exit 0
fi

if [[ -x "$GODOT_EXE" ]]; then
  echo "Packing with Godot..."
  cat > "$ROOT/godot_pack/pack_jinwoo.gd" << 'GDS'
extends SceneTree
func _init() -> void:
	var args := OS.get_cmdline_user_args()
	var out_pck := args[0]
	var stage := args[1]
	var packer := PCKPacker.new()
	if packer.pck_start(out_pck) != OK:
		printerr("pck_start failed"); quit(1); return
	_add_dir(packer, stage, stage)
	if packer.flush() != OK:
		printerr("flush failed"); quit(2); return
	print("Packed ", out_pck)
	quit(0)
func _add_dir(packer: PCKPacker, root: String, dir_path: String) -> void:
	var d := DirAccess.open(dir_path)
	if d == null: return
	d.list_dir_begin()
	var name := d.get_next()
	while name != "":
		if name.begins_with("."):
			name = d.get_next(); continue
		var full := dir_path.path_join(name)
		if d.current_is_dir():
			_add_dir(packer, root, full)
		else:
			var rel := full.trim_prefix(root).trim_prefix("/").trim_prefix("\\")
			packer.add_file("res://" + rel.replace("\\", "/"), full)
		name = d.get_next()
GDS
  "$GODOT_EXE" --headless --path "$ROOT/godot_pack" --script "$ROOT/godot_pack/pack_jinwoo.gd" -- "$PCK" "$STAGE"
  exit 0
fi

echo "ERROR: Need godotpcktool or Godot" >&2
exit 1
