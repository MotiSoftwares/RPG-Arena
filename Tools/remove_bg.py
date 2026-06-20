# Removes the solid background from the generated combatant sprites so they billboard
# cleanly on the orthographic stage (alpha cutout). Falls back silently if rembg is
# unavailable; the raw sprites still serve as character-select portraits.
# Usage: python Tools/remove_bg.py
import os, sys, glob

SRC = "Assets/_Project/Art/Sprites/Combatants"
OUT = "Assets/_Project/Art/Sprites/Combatants/cutout"
os.makedirs(OUT, exist_ok=True)

try:
    from rembg import remove
    from PIL import Image
except Exception as e:
    print("rembg/Pillow not available:", e)
    sys.exit(1)

count = 0
for path in glob.glob(os.path.join(SRC, "*.png")):
    name = os.path.basename(path)
    dst = os.path.join(OUT, name)
    if os.path.exists(dst) and os.path.getsize(dst) > 3000:
        continue
    try:
        with Image.open(path) as im:
            im = im.convert("RGBA")
            cut = remove(im)
            cut.save(dst)
            count += 1
            print("cutout:", name)
    except Exception as e:
        print("FAIL", name, e)
print(f"Done: {count} cutouts -> {OUT}")
