#!/usr/bin/env bash
# Pack JinwooNecrobinderSkin.pck with PNG + Godot .import + .ctex (required for STS2 overrides).
set -euo pipefail
ROOT="$(cd "$(dirname "$0")/.." && pwd)"
OUT_DIR="$ROOT/release/mods/JinwooNecrobinderSkin"
IMPORT_PROJ="$ROOT/godot_pack/import_project"
PCK="$OUT_DIR/JinwooNecrobinderSkin.pck"
GODOT_PCKTOOL="${GODOT_PCKTOOL:-/tmp/tools/godotpcktool}"
GODOT_EXE="${GODOT_EXE:-/tmp/godot/Godot_v4.5.1-stable_linux.x86_64}"

mkdir -p "$OUT_DIR"
cp "$ROOT/JinwooNecrobinderSkin.json" "$OUT_DIR/JinwooNecrobinderSkin.json"
cp "$ROOT/assets/preview/preview.png" "$OUT_DIR/preview.png"

# Rebuild import project assets + run Godot importer
bash "$ROOT/scripts/prepare_import_project.sh"

rm -f "$PCK"
CMD_JSON="$ROOT/godot_pack/commands.json"
python3 - << PY
import json
from pathlib import Path
root = Path("$IMPORT_PROJ")
cmds = []
# Pack everything except editor caches / md5
skip_parts = {".gdignore", "uid_cache.bin", "filesystem_cache10", "project_metadata.cfg", "global_script_class_cache.cfg", "editor"}
for path in sorted(root.rglob("*")):
    if not path.is_file():
        continue
    if path.name.endswith(".md5"):
        continue
    if path.name == "project.godot":
        continue
    rel = path.relative_to(root).as_posix()
    if any(p in skip_parts for p in Path(rel).parts):
        continue
    # Keep .godot/imported/*.ctex and source png/.import/json
    cmds.append({"file": str(path), "target": rel})
Path("$CMD_JSON").write_text(json.dumps(cmds, indent=2), encoding="utf-8")
print(f"queued {len(cmds)} files")
for c in cmds:
    print(" ", c["target"])
PY

if [[ ! -x "$GODOT_PCKTOOL" ]]; then
  echo "godotpcktool missing" >&2
  exit 1
fi

"$GODOT_PCKTOOL" "$PCK" --action add --command-file "$CMD_JSON" --set-godot-version 4.5.1 -q
echo "Wrote $PCK"
"$GODOT_PCKTOOL" "$PCK" --action list | head -60
