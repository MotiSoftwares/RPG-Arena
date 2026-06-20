// Batch-generates ability icons for the game via Pollinations (free, keyless flux).
// Output file names match the Ability asset names so the editor IconAssigner can map them.
// Usage: node Tools/gen_icons.mjs
import fs from "fs";

const OUT = "Assets/_Project/Art/UI/icons";
fs.mkdirSync(OUT, { recursive: true });

// A shared style so the whole set looks cohesive (one consistent seed-style suffix).
const STYLE = "fantasy RPG ability icon, flat vector emblem, centered, glowing, dark slate background, crisp, game UI icon";

// [assetName, short subject]
const ICONS = [
  ["Warrior_PowerStrike", "a heavy glowing sword smashing down, red energy"],
  ["Warrior_SlashBlast", "two crossed slashing sword arcs, red"],
  ["Warrior_Rage", "a roaring fist wreathed in red fury aura"],
  ["Warrior_GuardianTaunt", "a sturdy tower shield with a taunt glyph, steel blue"],
  ["Warrior_BerserkStance", "a yin-yang of red rage and blue guard, dual stance"],
  ["Warrior_CrushingBlow", "a giant warhammer impact shockwave, crimson"],
  ["Mage_MagicBolt", "a small swirling arcane bolt orb, prismatic"],
  ["Mage_Attunement", "four elemental runes ring fire ice lightning holy"],
  ["Mage_Fireball", "a blazing fireball, orange flames"],
  ["Mage_IceLance", "a sharp blue ice crystal spear"],
  ["Mage_Spark", "a crackling lightning bolt, yellow"],
  ["Mage_Heal", "a radiant green healing cross with sparkles"],
  ["Mage_Bless", "a golden holy blessing sigil with light rays"],
  ["Mage_Blizzard", "a swirling blizzard storm of ice shards, deep blue"],
  ["Thief_LuckySeven", "a spinning throwing star with a lucky 7, silver"],
  ["Thief_OilBomb", "a thrown oil flask splashing black liquid"],
  ["Thief_WaterBomb", "a thrown water flask splashing blue"],
  ["Thief_ShadowMark", "a purple target crosshair shadow mark glyph"],
  ["Thief_DarkSight", "a cloaked figure fading into shadow, stealth"],
  ["Thief_SmokeBomb", "a grey smoke cloud burst"],
  ["Thief_Assassinate", "a glowing dagger striking a weak point, crimson crit"],
  ["Archer_DoubleShot", "two parallel arrows in flight, green fletching"],
  ["Archer_SoulArrow", "a glowing spectral arrow piercing armor, teal"],
  ["Archer_MarkTarget", "an eye over a target reticle, marking"],
  ["Archer_Puppet", "a small wooden decoy puppet on strings"],
  ["Archer_EyeOfAmazon", "a keen golden eagle eye with focus lines"],
  ["Archer_ArrowRain", "a volley of arrows raining down from above"],
  ["Dragon_ClawSwipe", "three slashing dragon claw marks, dark red"],
  ["Dragon_TailSweep", "a sweeping armored dragon tail, dust"],
  ["Dragon_TailGuard", "armored dragon scales hardening, defensive"],
  ["Dragon_FlameBreath", "a dragon breathing a cone of fire"],
  ["Dragon_ChargingBreath", "a dragon inhaling, fire gathering at its maw, warning"],
];

const sleep = (ms) => new Promise((r) => setTimeout(r, ms));

let ok = 0, fail = 0, skip = 0;
for (let i = 0; i < ICONS.length; i++) {
  const [name, subject] = ICONS[i];
  const file = `${OUT}/${name}.png`;
  // Resume: skip already-generated icons so re-runs only fill the gaps.
  if (fs.existsSync(file) && fs.statSync(file).size > 1500) { skip++; continue; }

  const prompt = `${subject}, ${STYLE}`;
  const url = `https://image.pollinations.ai/prompt/${encodeURIComponent(prompt)}?model=flux&width=256&height=256&seed=${100 + i}&nologo=true`;

  let done = false;
  for (let attempt = 0; attempt < 3 && !done; attempt++) {
    try {
      const ctrl = new AbortController();
      const t = setTimeout(() => ctrl.abort(), 25000);   // per-fetch timeout so a stall can't hang the batch
      const res = await fetch(url, { redirect: "follow", signal: ctrl.signal });
      clearTimeout(t);
      if (!res.ok) { await sleep(1500); continue; }
      const buf = Buffer.from(await res.arrayBuffer());
      if (buf.length < 1500) { await sleep(1500); continue; }
      fs.writeFileSync(file, buf);
      ok++; done = true;
      console.log(`(${i + 1}/${ICONS.length}) ${name}  ${buf.length}b`);
    } catch (e) {
      await sleep(1500);   // retry after a backoff
    }
  }
  if (!done) { fail++; console.error(`FAIL ${name}`); }
  await sleep(400);
}
console.log(`Done: ${ok} new, ${skip} skipped, ${fail} failed -> ${OUT}`);
