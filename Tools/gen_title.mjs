// Generates an epic title-screen background for the main menu (Pollinations, free). 16:9.
import fs from "fs";
const OUT = "Assets/_Project/Art/Backdrops";
fs.mkdirSync(OUT, { recursive: true });
const file = `${OUT}/TitleScreen.png`;
const prompt = "epic dark fantasy RPG title screen background, a colossal fire dragon and a looming dark sorcerer and a fallen dark knight rising from a grand ancient colosseum arena, dramatic god rays, fire and shadow, ominous storm sky, highly detailed cinematic digital painting, no text, atmospheric depth";
const url = `https://image.pollinations.ai/prompt/${encodeURIComponent(prompt)}?model=flux&width=1280&height=720&seed=777&nologo=true`;
const sleep = (ms) => new Promise((r) => setTimeout(r, ms));
let done = false;
for (let a = 0; a < 4 && !done; a++) {
  try {
    const ctrl = new AbortController();
    const t = setTimeout(() => ctrl.abort(), 60000);
    const res = await fetch(url, { redirect: "follow", signal: ctrl.signal });
    clearTimeout(t);
    if (!res.ok) { await sleep(2500); continue; }
    const buf = Buffer.from(await res.arrayBuffer());
    if (buf.length < 5000) { await sleep(2500); continue; }
    fs.writeFileSync(file, buf); done = true;
    console.log(`TitleScreen ${buf.length}b`);
  } catch (e) { await sleep(2500); }
}
console.log(done ? "Done -> " + file : "FAILED");
