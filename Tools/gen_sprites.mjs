// Generates full-body combatant sprites via Pollinations (free, keyless flux).
// These billboard onto the orthographic stage as a no-credit fallback for 3D models
// (the authentic MapleStory 2.5D look). Output names match the combatant defs so an
// editor assigner can map them. Usage: node Tools/gen_sprites.mjs
import fs from "fs";

const OUT = "Assets/_Project/Art/Sprites/Combatants";
fs.mkdirSync(OUT, { recursive: true });

// One cohesive style: full body, facing camera, isolated on pure black so the sprite
// blends into the dark lava arena when billboarded (clean enough without alpha cutout).
const STYLE =
  "full body character, facing forward, dramatic rim light, isolated on solid pure black background, " +
  "centered, fantasy RPG game character art, painterly, high detail, single character";

// [fileName, subject]
const SPRITES = [
  ["Dragon", "a colossal fierce ancient fire dragon standing on four legs, folded leathery wings, dark crimson and charcoal scales with glowing molten orange cracks, horned head, menacing, boss creature"],
  ["Warrior", "a stalwart armored warrior hero, heavy steel plate armor, large sword and shield, red cape, confident stance, MapleStory adventurer"],
  ["Mage", "a slender mage hero in blue and gold robes, holding a glowing arcane staff, hood, mystical aura, MapleStory adventurer"],
  ["Thief", "an agile thief hero in dark leather hood and cloak, twin daggers, purple accents, crouched ready stance, MapleStory adventurer"],
  ["Archer", "a keen archer hero in green and brown ranger leathers, holding a longbow with quiver, focused, MapleStory adventurer"],
];

const sleep = (ms) => new Promise((r) => setTimeout(r, ms));

let ok = 0, fail = 0, skip = 0;
for (let i = 0; i < SPRITES.length; i++) {
  const [name, subject] = SPRITES[i];
  const file = `${OUT}/${name}.png`;
  if (fs.existsSync(file) && fs.statSync(file).size > 3000) { skip++; continue; }

  const prompt = `${subject}, ${STYLE}`;
  // Taller than wide for a standing combatant; flux gives the best character art.
  const url = `https://image.pollinations.ai/prompt/${encodeURIComponent(prompt)}?model=flux&width=512&height=768&seed=${40 + i}&nologo=true`;

  let done = false;
  for (let attempt = 0; attempt < 4 && !done; attempt++) {
    try {
      const ctrl = new AbortController();
      const t = setTimeout(() => ctrl.abort(), 45000);   // larger images take longer
      const res = await fetch(url, { redirect: "follow", signal: ctrl.signal });
      clearTimeout(t);
      if (!res.ok) { await sleep(2000); continue; }
      const buf = Buffer.from(await res.arrayBuffer());
      if (buf.length < 3000) { await sleep(2000); continue; }
      fs.writeFileSync(file, buf);
      ok++; done = true;
      console.log(`(${i + 1}/${SPRITES.length}) ${name}  ${buf.length}b`);
    } catch (e) {
      await sleep(2000);
    }
  }
  if (!done) { fail++; console.error(`FAIL ${name}`); }
  await sleep(600);
}
console.log(`Done: ${ok} new, ${skip} skipped, ${fail} failed -> ${OUT}`);
