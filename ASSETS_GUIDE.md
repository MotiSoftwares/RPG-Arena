# Asset Acquisition Guide — grab everything (it's all free)

Drop everything into the folders in §8. Name the **characters** and **animations** exactly as shown
so my editor auto-wires them. I handle ALL the Unity side (import, rig, Animator, materials,
lighting, VFX hookup, audio, TMP fonts). Priority order is in §9 if you want the essentials first.

---

## 1. CHARACTERS — Mixamo (free, rigged humanoids)
mixamo.com → sign in (free Adobe account) → **Characters** tab → pick one → **Download**:
**Format = FBX for Unity (.fbx)**, **Pose = T-pose**, with skin. Save into `Art/Models/` named EXACTLY:

| File name | Search terms on Mixamo | Pick something that reads as… |
|---|---|---|
| `Warrior.fbx` | knight, paladin, crusader | heavy plate armor + cape, a sword/shield silhouette |
| `Mage.fbx` | mage, sorcerer, wizard, robe | long hooded robe, staff vibe (e.g. "Mremireh O Desbiens") |
| `Thief.fbx` | rogue, ninja, assassin, hooded | lean, hooded, agile (e.g. "Ninja") |
| `Archer.fbx` | archer | a bow character (e.g. "Erika Archer With Bow Arrow") |
| `BlackMage.fbx` | warlock, lich, dark sorcerer | a DARK robed caster (visually distinct from Mage) |
| `EvilWarrior.fbx` | dark knight, warzombie, mutant | a menacing armored brute (distinct from Warrior) |

**Cohesion tip:** pick characters in a similar style (all stylized OR all realistic) so the party looks like one game. Exact names vary — the closest visual match is fine; they all auto-rig to Humanoid.

## 2. ANIMATIONS — Mixamo (free; download ONCE, I retarget to everyone)
With any character loaded → **Animations** tab → search → select → **Download**:
**FBX for Unity**, **Without Skin**, 30 fps, no keyframe reduction. Save into `Art/Animations/` named with an `@`:

| File name | Search terms | Used for |
|---|---|---|
| `Anim@Idle.fbx` | breathing idle, sword and shield idle | everyone's resting pose |
| `Anim@Attack.fbx` | sword and shield slash, great sword slash | Warrior / generic melee |
| `Anim@Cast.fbx` | standing 1H magic attack 01, spell casting | Mage / Black Mage |
| `Anim@Stab.fbx` | stabbing, knife attack | Thief |
| `Anim@Bow.fbx` | standing draw arrow, shoot arrow | Archer |
| `Anim@Hit.fbx` | standing react small from front, hit reaction | taking a hit |
| `Anim@Die.fbx` | standing death forward 01, dying | KO |
| `Anim@Channel.fbx` | standing 2H magic attack 01, praying | boss telegraph wind-up (optional) |
| `Anim@Victory.fbx` | victory, cheering | win screen (optional) |

(8–9 clips total covers all 7 characters — I retarget them via Humanoid.)

## 3. THE DRAGON + non-humanoids — Asset Store / Sketchfab (Mixamo has no dragons)
Pick ONE source for `Dragon.fbx`:
- **Unity Asset Store** → search **dragon** → filter **Free** → choose one tagged **rigged + animated**
  (good picks: "Dragon for Boss Monster : HP", "Unka The Dragon", "Dragon (Free)"). Add to your
  account → it lands in **Package Manager → My Assets**; just import it and tell me the folder — I'll
  extract the prefab. (Asset Store dragons don't need renaming.)
- **OR Sketchfab** → search **dragon animated** → filter **Downloadable + Animated** + a CC license →
  download **FBX/GLB** → drop into `Art/Models/Dragon.fbx`.

## 4. VFX — Asset Store (free particle packs; grab a couple for full coverage)
Search the Asset Store, filter **Free**, and grab:
- **Cartoon FX Remaster Free** (Jean Moreno / JMO) — fire, ice, magic, buffs, impacts. The workhorse.
- **War FX** (Jean Moreno) — explosions / heavy impacts (great for crits & Flame Breath).
- **Unity Particle Pack** (Unity Technologies) — fire, smoke, sparks, embers (also good arena ambience).
- (optional extra) **Epic Toon FX (Free)** or any "free magic spell" pack for more elemental variety.

I need these elements covered (the packs above do): **fire, ice/frost, lightning, holy/light,
dark/shadow, heal, generic impact, buff aura, slash**. Just import the packs — I'll pick the prefabs.

## 5. AUDIO — I'll generate this (you can skip it)
I can generate **royalty-free music + every SFX** tailored to the game via ElevenLabs (already wired):
menu/battle/phase-2/victory/defeat music, and hit/fire/ice/lightning/holy/dark/heal/crit/miss/BREAK/
telegraph/UI-hover/UI-click/victory/defeat SFX. **No action needed** — but if you stumble on a great
free music loop (incompetech.com Kevin MacLeod CC, or an Asset Store "Free Music" pack), drop it into
`Audio/Music/` and I'll use it instead.

## 6. FONTS — free (big polish bump; we currently use Unity's built-in font)
Grab `.ttf` files into `Art/UI/Fonts/`:
- **Title/display font:** Cinzel, Cinzel Decorative, or MedievalSharp (Google Fonts → Download family).
- **Body/UI font:** Inter, Roboto, or Barlow (Google Fonts).
I'll build TMP font assets and restyle the menus/HUD with them.

## 7. UI KIT + ICONS — free (optional, makes the UI premium)
- **UI kit** (panels/buttons/bars/frames): Asset Store **Free** → "GUI Pro Free", "Fantasy Wooden GUI:
  Free", or **Kenney UI Pack** (kenney.nl, CC0). Drop into `Art/UI/`.
- **Ability icons:** I already generate these (Pollinations). Optional upgrade: game-icons.net (CC BY)
  or a free "RPG icons" Asset Store pack → `Art/UI/Icons/` and I'll swap them in.
- **Skybox** (adds depth behind the arena): Asset Store **Free** → "Fantasy Skybox FREE" or "Free
  Skybox" → import; I'll wire it per arena.
- **Arena props / ground** (optional): "Free fantasy props", Kenney/Quaternius (CC0) dungeon/pillars/
  braziers, and Poly Haven (CC0) rock/lava ground textures → `Art/Props/` and `Art/Materials/`.

## 8. WHERE TO PUT IT (folders — make any that don't exist)
```
Assets/_Project/Art/
  Models/        Warrior.fbx Mage.fbx Thief.fbx Archer.fbx BlackMage.fbx EvilWarrior.fbx Dragon.fbx
  Animations/    Anim@Idle.fbx Anim@Attack.fbx Anim@Cast.fbx Anim@Stab.fbx Anim@Bow.fbx Anim@Hit.fbx Anim@Die.fbx ...
  VFX/           (the particle packs — leave their own folders; I'll pick prefabs)
  UI/Fonts/      Cinzel.ttf  Inter.ttf  ...
  UI/            (UI kit)
  Skybox/        (skybox materials/textures)
  Props/         (optional arena props)
  Materials/     (optional ground textures)
Assets/_Project/Audio/Music/   (only if you grab music)
Assets/_Project/Audio/SFX/     (only if you grab sfx)
```
Asset-Store packages often import to `Assets/<PackName>/` — that's fine, leave them there, I'll locate them. **Only the characters + animations need the exact names above.**

## 9. PRIORITY (if you want the essentials first → a polished slice fast)
1. **6 Mixamo characters** + the **animation set** (§1–§2) — the combatants.
2. **Dragon** (§3) — the star boss.
3. **Cartoon FX Remaster Free** (§4) — instant juice on every skill.
4. **A title font + a body font** (§6) — crisp modern UI.
5. **Fantasy Skybox FREE** (§7) — depth behind the arena.
Then the rest (more VFX, UI kit, props) whenever.

## 10. WHAT I DO (zero action from you, once files are in)
Import config + Humanoid rig + scale/orientation fix · Animator Controllers (Idle⇄Attack⇄Cast⇄Hit⇄Die)
· `AnimationDriver` that reacts to combat event channels · swap the 2D billboards → the 3D models
(one assigner) · per-ability VFX/SFX assignment · URP materials + 3-point & APV lighting + skybox +
post-FX + Cinemachine framing · TMP fonts + UI restyle · generate + mix all audio. Tell me when a
batch is in `Art/Models/` and I'll wire it immediately.
