// Generates the two extra boss sprites (Black Mage, Evil Warrior) via Pollinations (free),
// matching the Dragon's full-body-on-black style so remove_bg can cut them out for billboards.
// Usage: node Tools/gen_boss_sprites.mjs
import fs from "fs";

const OUT = "Assets/_Project/Art/Sprites/Combatants";
fs.mkdirSync(OUT, { recursive: true });

const STYLE =
  "full body character, facing forward, dramatic rim light, isolated on solid pure black background, " +
  "centered, fantasy RPG game boss, painterly, high detail, single character";

const SPRITES = [
  ["BlackMage", "a sinister towering black mage archlich, tattered dark purple and black robes, glowing violet eyes and hands, swirling dark magic, ornate skull staff, world-ending sorcerer boss"],
  ["EvilWarrior", "a menacing fallen dark knight, jagged blackened spiked full plate armor, glowing red cracks, huge cursed greatsword, torn dark cape, brutal evil warrior boss"],
];

const sleep = (ms) => new Promise((r) => setTimeout(r, ms));
let ok = 0, fail = 0, skip = 0;
for (let i = 0; i < SPRITES.length; i++) {
  const [name, subject] = SPRITES[i];
  const file = `${OUT}/${name}.png`;
  if (fs.existsSync(file) && fs.statSync(file).size > 3000) { skip++; continue; }
  const prompt = `${subject}, ${STYLE}`;
  const url = `https://image.pollinations.ai/prompt/${encodeURIComponent(prompt)}?model=flux&width=512&height=768&seed=${70 + i}&nologo=true`;
  let done = false;
  for (let attempt = 0; attempt < 4 && !done; attempt++) {
    try {
      const ctrl = new AbortController();
      const t = setTimeout(() => ctrl.abort(), 45000);
      const res = await fetch(url, { redirect: "follow", signal: ctrl.signal });
      clearTimeout(t);
      if (!res.ok) { await sleep(2000); continue; }
      const buf = Buffer.from(await res.arrayBuffer());
      if (buf.length < 3000) { await sleep(2000); continue; }
      fs.writeFileSync(file, buf);
      ok++; done = true;
      console.log(`(${i + 1}/${SPRITES.length}) ${name}  ${buf.length}b`);
    } catch (e) { await sleep(2000); }
  }
  if (!done) { fail++; console.error(`FAIL ${name}`); }
  await sleep(600);
}
console.log(`Done: ${ok} new, ${skip} skipped, ${fail} failed -> ${OUT}`);
