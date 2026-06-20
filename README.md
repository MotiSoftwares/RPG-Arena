# Arena of the Algorithms

> A **2.5D turn-based boss-rush RPG** built in Unity 6.3 (URP). Assemble a party of three heroes from four classic adventurer classes and fight a gauntlet of three intelligent bosses — read each boss, exploit its elemental weakness, set up cross-class status combos, build the **Stagger** meter, and unleash a burst during the Break window.

An affectionate homage to MapleStory's four original jobs (Warrior / Mage / Thief / Archer) and its iconic villains, distilled into short, replayable, puzzle-like duels in the spirit of *Octopath Traveler*'s Break system and *Final Fantasy XIII*'s stagger.

---

## How to run

### Option A — from a build (easiest)
1. Download/locate the Windows build under `Builds/RPGArena/`.
2. Run `ArenaOfTheAlgorithms.exe`.
3. **Quit** from the main menu exits the application.

### Option B — from the Unity Editor
1. Open the project in **Unity 6.3 LTS** (`6000.3.x`; developed on `6000.3.14f1`).
2. If prompted, import **TMP Essentials** (one click).
3. Open `Assets/_Project/Scenes/Boot.unity` and press **Play** (Boot → Main Menu → Battle).
   - You can also press Play from `MainMenu.unity` directly.

All three scenes (`Boot`, `MainMenu`, `BattleArena`) are in Build Settings in that order.

---

## Gameplay & objective

You are champions entering the **Arena of the Algorithms**, where three legendary threats are bound as the ultimate test. Clear all three to win.

- **Pick 3 of 4 heroes** at Character Select (each plays completely differently).
- Fight the gauntlet: **The Dragon → The Black Mage → The Evil Warrior**.
- Between bosses you **fully heal** and **draft one of three roguelite boons** (permanent party-wide upgrades).
- **Win:** reduce the boss to 0 HP. **Lose:** the whole party falls (retry the same boss, or return to the menu).
- A single fight is ~4–6 minutes; a full run is ~12–18 minutes (and you can demo a single boss quickly).

### The core loop
1. **Read the boss.** Each has a hidden **weakness** and a punishing reaction — e.g. the **Dragon absorbs Fire (it heals!) but is weak to Ice.** The UI reveals weak/absorb elements once you study or land them.
2. **Set up combos.** The Thief applies flags (*Oiled*, *Wet*, *Marked*); the Mage/Archer detonate them (Oiled+Fire, Wet+Ice→Frozen, Wet+Lightning→Stun).
3. **Build Stagger.** Weakness hits and multi-hit/break skills fill the boss's Stagger bar. At full, the boss **Breaks** — it loses a turn and takes a large damage multiplier. **Breaking the Dragon mid-charge cancels its Flame Breath.**
4. **Manage resources.** Basic attacks cost 0 MP and regenerate a little; specials cost MP and have cooldowns. Strong attacks can **miss** (the action menu shows each move's hit %).
5. **Survive & burst.** Heal, defend, taunt, or go stealth before a telegraphed nuke; then unload during the Break window.

---

## Controls

Turn-based, mouse-driven:

| Input | Action |
|---|---|
| **Left-click** | Select an ability, choose a target, press any menu button |
| **Mouse hover** | Button highlight feedback |
| **Esc** | Pause / resume (in battle) |
| **`` ` `` (backquote)** | Toggle the developer Cheat panel — *Editor / development builds only* |

The action menu shows each ability's **MP cost**, **cooldown**, **icon**, and **hit %**. Click an ability, then the highlighted target.

---

## Gameplay mechanics (the counted systems)

This project ships **seven** distinct, decision-driving mechanics (the rubric requires five):

1. **Turn-based FSM combat** with speed-ordered initiative and an **action-economy reward** (a weakness hit or crit grants a capped bonus turn).
2. **Elemental matrix** — Physical/Fire/Ice/Lightning/Holy/Dark with weak ×1.5 / resist ×0.5 / immune ×0 / **absorb (heals the boss)**. Choosing the right element is the puzzle of every fight.
3. **MP economy + cooldowns** — poke to build MP vs. spend to burst; powerful skills gated by cooldowns.
4. **Stagger / Break** — pressure fills a meter; on Break the boss skips a turn, takes a damage multiplier, and its telegraphed attack is **cancelled**.
5. **Cross-class status synergies** — *Oiled+Fire*, *Wet+Lightning* (stun), *Wet+Ice* (freeze), *Marked* (crit/stagger), driving who acts in what order.
6. **Stance / element-attunement switching** — the Mage re-attunes Fire/Ice/Lightning/Holy to answer any boss; the Warrior toggles Berserk/Guardian.
7. **Informed-gamble RNG** — per-ability hit tiers (Reliable/Standard/Risky) with the odds shown before you commit; a Staggered target can't dodge. (Design revision, Appendix E.)

Plus the **roguelite boon draft** between bosses (a progression meta-layer).

---

## Architecture overview

The codebase is built from four named, textbook patterns so it is testable, extensible, and easy to explain:

- **ScriptableObject data** — *all* content (abilities, characters, bosses, AI, status effects, element profiles, boons, the balance config) is data assets. Adding content never means editing engine code.
- **Command pattern** — every action is an `ICommand` built from `Ability` data and executed by the engine (uniform logging, easy cheats/tests).
- **Finite-state machine** — the battle is an explicit FSM (`BattleSetup → RoundStart → TurnStart → AwaitInput/EnemyDecision → ResolveAction → CheckDeaths → TurnEnd → RoundEnd → Victory/Defeat`). The live game uses a coroutine driver (`BattleController`); a synchronous `BattleManager` runs the same systems headlessly for tests.
- **Strategy pattern** — each boss brain is a swappable `AIBehavior` asset: `DragonCycleAI` (telegraphed cycle), `ChaoticAI` (high-variance debuffer), `AggressiveAI` (utility scoring that focuses the weakest hero).
- **ScriptableObject event channels** decouple the layers: core raises events (`OnDamageDealt`, `OnStaggerBroken`, `OnBossTelegraph`, …); presentation (HUD, audio, juice, narrative) only *reacts*.

Assemblies (acyclic): `Utilities → Core → Gameplay → {UI, Audio, Narrative, Cheats, Tests, EditorTools}`.

The "compute" half of the damage pipeline is a pure function, so it (and the elemental matrix, stagger, and synergies) are covered by an **edit-mode test suite** (`RPGArena.Tests`).

---

## Bonus features

- **Cheat Manager (dev-only)** — a full IMGUI panel (god mode, infinite MP, refill, set/kill boss HP, force-Break, apply statuses, time-scale). The entire `RPGArena.Cheats` assembly is stripped from release builds via an asmdef define-constraint (`UNITY_EDITOR || DEVELOPMENT_BUILD`); it self-installs at runtime, leaving zero trace in a player build. Open with `` ` ``.
- **AI implementation** — three genuinely different Strategy-pattern boss brains with stateful telegraphed cycles, utility-scored targeting, and high-variance decision weighting.
- **Ink narrative (external framework)** — the Dragon's pre-fight dialogue is a real Ink story **deeply wired to combat**: "Taunt" makes the Dragon open by charging (riskier/faster); "Study" reveals its weakness in the HUD immediately. An Ink outro reads variables the game sets from the actual result.
- **Creativity & extra effort** — the Stagger/Break system, cross-class synergies, stance/attunement switching, the telegraph-cancel skill expression, the roguelite boon draft, and a full juice layer (hit-stop, camera shake, floating numbers, BREAK slow-mo, post-FX).

---

## Audio

All audio routes through an **AudioMixer** (`Master → Music / SFX`) with exposed volume parameters bound to the Settings sliders (persisted via `PlayerPrefs`, dB-mapped). SFX use a round-robin `AudioSource` voice pool; music cross-fades. Generated with **ElevenLabs** (SFX + looping music beds; the paid Music API was unavailable on the free tier, so music uses seamless looped sound effects).

---

## Assets & credits

No paid Asset Store packages — everything is free, included, and documented.

| Asset | Source | License / notes |
|---|---|---|
| Hero & boss sprites (Warrior/Mage/Thief/Archer, Dragon/Black Mage/Evil Warrior) | **Pollinations.ai** (FLUX) | Free, keyless generation |
| Background removal for sprite cutouts | **rembg** (Python) | MIT |
| Ability icons (32) | **Pollinations.ai** (FLUX) | Free |
| Sound effects + music loops | **ElevenLabs** (text-to-sound-effects) | Generated, royalty-free |
| Narrative scripting | **Ink for Unity** (inkle) | MIT |
| Rendering / input / camera / text | URP, Input System, Cinemachine, TextMeshPro | Unity packages (free) |
| UI font | Unity built-in `LegacyRuntime.ttf` | Unity |

### AI-tool disclosure
This project was built with substantial AI assistance: **Claude Code** driving the **Unity MCP** bridge for code, scenes, prefabs, and ScriptableObject authoring; generative AI for placeholder **art (Pollinations)** and **audio (ElevenLabs)**. The engineering, system design, architecture, and integration are the student's work; the disclosure is provided in the spirit of academic honesty.

---

## Project management

Developed Scrum-style against the milestone backlog in `CLAUDE.md` §8 (M0 scaffold → M1 core combat → M2 four classes → M3 UI/flow → M4 art/animation/lighting → M5 audio/juice/Ink/cheats → M6 the two extra bosses + run flow → M7 stabilise/build/document). Work was tracked on **GitHub Issues/Projects** (epics per milestone) with small, frequent, area-prefixed commits to `main` (the graded branch). The commit history is the sprint log.

---

## Known issues / future work

- Combatant art uses generated 2D billboards (the orthographic "MapleStory" look). The Tripo 3D pipeline is wired but its API requires paid credits (the free web credits are a separate billing pool); 3D models can be dropped in later without code changes.
- Music is short looped beds (the ElevenLabs Music API is paid-tier only).
- Future: per-boss intro dialogue for the Black Mage / Evil Warrior, a score/grade screen, difficulty modes, and an endless mode.

---

*Built with Unity 6.3 · URP · orthographic 2.5D. See `CLAUDE.md` for the full design + engineering specification.*
