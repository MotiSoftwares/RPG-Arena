# 3D Model Generation Guide — Tripo Web Studio

Generate the 7 combatant models in the **Tripo web studio** (where your 500 credits work — the
API is a separate paid pool, so use the website). I'll do 100% of the Unity side: import,
Humanoid rig setup, Animator Controllers (Idle/Attack/Hit/Die), swap the placeholder sprites,
materials, lighting, and polish.

---

## 0. Setup
1. Go to **https://www.tripo3d.ai** and log in to the account that shows **500 credits**.
2. Open **"Text to 3D"** (the prompt box).
3. Use the **same settings for every model** so they look like one cohesive game:
   - **Model Version:** the newest available (v3.0 / v2.5).
   - **Style:** pick ONE and keep it for all 7 (e.g. *Original/Realistic*, or a stylized preset — your call, just be consistent).
   - **Quality / Geometry:** **Standard** for the 4 heroes; **Detailed/HD** for the **Dragon** (it's the star) and optionally the two bosses.
   - **Texture / PBR:** **On**.
   - **Quad topology:** **On** if offered (cleaner rig).

---

## 1. Per-model workflow (do this for each of the 7)
1. Paste the prompt → **Generate**.
2. When it finishes → **Texture** (adds PBR colour).
3. → **Rig** (auto-rig for animation). For the **humanoids** this gives a skeleton I can drive.
   The **Dragon** may not rig (quadruped) — if it refuses, just skip rigging for it; I'll animate it procedurally.
4. (Optional) → **Animate** → if an **"Idle"** preset is offered, apply it. Don't worry about
   attack/hit anims — Tripo's are locomotion-only; I'll add combat motion in Unity.
5. → **Download** → **FBX** (with rig/animation if available; GLB is fine as a fallback).
6. Save the file named **exactly** as listed below into:
   `Assets/_Project/Art/Models/`

---

## 2. The prompts (copy-paste) + file names

**Heroes**

`Warrior.fbx`
> A heroic fantasy warrior knight, full body, standing heroic pose, heavy ornate steel plate armor, red tabard and cape, holding a sword and a shield, clean readable silhouette, stylized game character, T-pose friendly

`Mage.fbx`
> A fantasy mage hero, full body, standing, flowing deep-blue and gold robes with a pointed hood, holding a tall glowing wooden staff, mystical, clean readable silhouette, stylized game character

`Thief.fbx`
> A fantasy rogue thief, full body, standing, dark leather armor with a hood and short cloak, twin daggers, purple accents, agile, clean readable silhouette, stylized game character

`Archer.fbx`
> A fantasy archer ranger, full body, standing, green and brown leather ranger outfit with a hood, holding a longbow, a quiver of arrows on the back, clean readable silhouette, stylized game character

**Bosses**

`Dragon.fbx`  *(use Detailed/HD)*
> A massive fierce ancient fire dragon, full body, standing on four legs with large leathery wings folded, dark crimson and charcoal scales with glowing molten orange cracks, horned head, sharp teeth, long tail, fantasy boss monster, highly detailed

`BlackMage.fbx`
> A sinister towering archlich sorcerer, full body, standing, tattered dark purple and black robes, glowing violet eyes, skeletal hands wreathed in dark energy, an ornate skull-topped staff, menacing fantasy boss, clean silhouette

`EvilWarrior.fbx`
> A menacing fallen dark knight, full body, standing, jagged blackened spiked full plate armor with glowing red cracks, a huge cursed greatsword, a torn dark cape, brutal evil fantasy boss, clean silhouette

---

## 3. Credit budget & priority
Each model (generate + texture + rig) is roughly ~30–60 credits, so 7 fits in ~300–400 of your
500, leaving room for re-rolls. If you want to spread it out, do them in this order and I'll wire
each batch as it arrives:
1. **Dragon** (the showcase) + the **4 heroes** → a complete, great-looking first boss fight.
2. **Black Mage** + **Evil Warrior** later.

You can re-generate any model you don't like before downloading (re-rolls cost credits, so pick
the best of a couple).

---

## 4. What happens after you drop the files in
Tell me when files are in `Assets/_Project/Art/Models/` (even just a few). I will:
- Import each FBX, set the rig to **Humanoid** (heroes/bosses) and fix scale/orientation.
- Build **Animator Controllers** (Idle ⇄ Attack ⇄ Hit ⇄ Die) and an `AnimationDriver` that
  reacts to the combat event channels.
- Swap the 2D billboards → the 3D models (one line; the architecture already supports it).
- Set up URP materials, 3-point + ambient lighting, and per-ability VFX on the models.

Meanwhile I'm doing the model-agnostic polish (lighting, Cinemachine Impulse, post-FX, crisp TMP
UI + styled panels + a real menu background, VFX) so the moment the models land, it all looks top-tier.
