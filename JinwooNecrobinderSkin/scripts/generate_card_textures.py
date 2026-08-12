#!/usr/bin/env python3
from pathlib import Path
from PIL import Image, ImageEnhance

ROOT = Path(__file__).resolve().parents[1]
cards_src = ROOT / "assets" / "cards"
out = ROOT / "release" / "mods" / "CustomCardTextures" / "necrobinder"
out.mkdir(parents=True, exist_ok=True)

strike = Image.open(cards_src / "strike_necrobinder.png").convert("RGBA")
defend = Image.open(cards_src / "defend_necrobinder.png").convert("RGBA")
army = Image.open(cards_src / "shadow_army.png").convert("RGBA")

def tint(im, rgba, a=0.15, sat=1.1, bright=1.0):
    out_im = Image.blend(im.copy(), Image.new("RGBA", im.size, rgba), a)
    out_im = ImageEnhance.Color(out_im).enhance(sat)
    out_im = ImageEnhance.Brightness(out_im).enhance(bright)
    return out_im

assignments = {
    "strike_necrobinder.png": strike,
    "defend_necrobinder.png": defend,
    "bodyguard.png": tint(army, (40, 0, 90, 255), 0.2, 1.2, 0.9),
    "unleash.png": tint(strike, (90, 0, 120, 255), 0.22, 1.3, 1.05),
    "reanimate.png": tint(army, (20, 40, 80, 255), 0.18, 1.1, 0.95),
    "soul_storm.png": tint(army, (80, 0, 100, 255), 0.25, 1.35, 0.9),
    "negative_pulse.png": tint(defend, (60, 0, 40, 255), 0.2, 1.15, 0.85),
    "capture_spirit.png": tint(strike, (30, 0, 70, 255), 0.18, 1.2, 1.0),
    "calcify.png": tint(defend, (50, 50, 80, 255), 0.2, 0.85, 0.9),
    "sacrifice.png": tint(army, (100, 0, 40, 255), 0.22, 1.2, 0.88),
    "afterlife.png": tint(army, (20, 0, 60, 255), 0.25, 1.1, 0.8),
    "scourge.png": tint(strike, (70, 0, 50, 255), 0.2, 1.25, 1.0),
    "countdown.png": tint(defend, (40, 20, 70, 255), 0.18, 1.0, 0.92),
    "oblivion.png": tint(army, (10, 0, 30, 255), 0.3, 0.9, 0.75),
    "deathbringer.png": tint(strike, (80, 0, 80, 255), 0.22, 1.3, 0.95),
    "reaper_form.png": tint(army, (50, 0, 70, 255), 0.28, 1.2, 0.85),
    "haunt.png": tint(defend, (40, 0, 60, 255), 0.2, 1.1, 0.88),
    "bone_shards.png": tint(strike, (70, 70, 90, 255), 0.15, 0.95, 1.05),
    "graveblast.png": tint(army, (60, 20, 40, 255), 0.2, 1.15, 0.95),
    "legion_of_bone.png": tint(army, (30, 30, 50, 255), 0.22, 1.0, 0.9),
    "forbidden_grimoire.png": tint(defend, (50, 0, 80, 255), 0.25, 1.2, 0.85),
    "protector.png": tint(defend, (20, 40, 70, 255), 0.18, 1.0, 1.0),
    "blight_strike.png": tint(strike, (40, 80, 40, 255), 0.2, 1.1, 0.95),
    "sculpting_strike.png": tint(strike, (70, 40, 90, 255), 0.18, 1.2, 1.0),
    "banshees_cry.png": tint(army, (90, 40, 100, 255), 0.22, 1.25, 0.9),
    "pull_from_below.png": tint(army, (20, 10, 50, 255), 0.25, 1.1, 0.85),
    "sic_em.png": tint(strike, (100, 20, 40, 255), 0.2, 1.2, 1.05),
    "shroud.png": tint(defend, (10, 10, 40, 255), 0.28, 0.9, 0.8),
    "invoke.png": tint(army, (60, 0, 90, 255), 0.2, 1.25, 0.95),
    "seance.png": tint(defend, (70, 0, 100, 255), 0.22, 1.15, 0.9),
}

for name, im in assignments.items():
    # keep ~900px wide for size
    w, h = im.size
    if w > 900:
        nh = int(h * 900 / w)
        im = im.resize((900, nh), Image.Resampling.LANCZOS)
    try:
        im.quantize(colors=256, method=Image.Quantize.MEDIANCUT).save(out / name, optimize=True)
    except Exception:
        im.save(out / name, optimize=True)
print(f"wrote {len(assignments)} textures -> {out}")
