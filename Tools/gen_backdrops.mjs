// Generates a cinematic background for each boss arena via Pollinations (free, keyless flux).
// These billboard behind the combatants so the stage is a place, not a void. 16:9 landscape.
// Usage: node Tools/gen_backdrops.mjs
import fs from "fs";

const OUT = "Assets/_Project/Art/Backdrops";
fs.mkdirSync(OUT, { recursive: true });

const STYLE = "fantasy RPG battle arena background, cinematic, atmospheric depth, dramatic lighting, highly detailed digital painting, no characters, no text, wide establishing shot";

const SHOTS = [
  ["Arena_Dragon", "a vast volcanic dragon lair cavern, rivers of glowing molten lava, jagged black basalt rock, drifting embers and heat haze, deep orange and red glow"],
  ["Arena_BlackMage", "an eerie ancient void temple of dark sorcery, swirling violet and indigo magical energy, floating shattered stone arches, cold purple light, ominous fog"],
  ["Arena_EvilWarrior", "a ruined dark fortress throne hall, broken obsidian pillars, a blood-red stormy sky through shattered gothic windows, grim cold steel tones, scattered rubble"],
];

const sleep = (ms) => new Promise((r) => setTimeout(r, ms));
let ok = 0, fail = 0, skip = 0;
for (let i = 0; i < SHOTS.length; i++) {
  const [name, subject] = SHOTS[i];
  const file = `${OUT}/${name}.png`;
  if (fs.existsSync(file) && fs.statSync(file).size > 5000) { skip++; continue; }
  const prompt = `${subject}, ${STYLE}`;
  const url = `https://image.pollinations.ai/prompt/${encodeURIComponent(prompt)}?model=flux&width=1280&height=720&seed=${200 + i}&nologo=true`;
  let done = false;
  for (let attempt = 0; attempt < 4 && !done; attempt++) {
    try {
      const ctrl = new AbortController();
      const t = setTimeout(() => ctrl.abort(), 60000);
      const res = await fetch(url, { redirect: "follow", signal: ctrl.signal });
      clearTimeout(t);
      if (!res.ok) { await sleep(2500); continue; }
      const buf = Buffer.from(await res.arrayBuffer());
      if (buf.length < 5000) { await sleep(2500); continue; }
      fs.writeFileSync(file, buf);
      ok++; done = true;
      console.log(`(${i + 1}/${SHOTS.length}) ${name}  ${buf.length}b`);
    } catch (e) { await sleep(2500); }
  }
  if (!done) { fail++; console.error(`FAIL ${name}`); }
  await sleep(800);
}
console.log(`Done: ${ok} new, ${skip} skipped, ${fail} failed -> ${OUT}`);
