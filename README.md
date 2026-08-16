# Arena of the Algorithms

Welcome. This document provides essential information about the game, including how to play, game
mechanics, controls, and additional notes.

---

### Team — Solo Project *(approved by the instructor)*
- **Game Name**: Arena of the Algorithms
- **Participants**:
  - Moti Yeshayahu

---

### Table of Contents
1. [Game Overview](#game-overview)
2. [Credits](#credits)
3. [How to Play](#how-to-play)
4. [Controls](#controls)
5. [Objectives & Goals](#objectives--goals)
6. [Game Mechanics & Features](#game-mechanics--features)
7. [System Requirements](#system-requirements)
8. [Known Issues & Troubleshooting](#known-issues--troubleshooting)
9. [Additional Notes](#additional-notes)
10. [Media](#media)

**Additional technical sections** *(for the bonus criteria)*
11. [External Framework Integration — Ink](#external-framework-integration--ink)
12. [Cheat Manager](#cheat-manager)
13. [AI Systems](#ai-systems)
14. [Architecture & Code Organisation](#architecture--code-organisation)
15. [Testing](#testing)
16. [Project Management](#project-management)

---

## Game Overview

- **Genre**: Turn-based tactical RPG / boss rush
- **Platform**: Windows (Unity 6, Universal Render Pipeline)
- **Team**: Moti Yeshayahu (solo)

You lead a party of three heroes through a gauntlet of bosses in an arena built by an order of
mages — each boss a different *puzzle* rather than a bigger health bar.

The game is built around a single design contract, decided before any code was written:
**spamming the basic attack must lose.** Not merely be weaker — actually lose. Every system in the
game exists to serve that rule, and the rule is enforced automatically by a unit test that fails if a
basic-attack-only party can win the fight.

Winning therefore means *coordinating*: chaining elemental status effects across different heroes,
racing the boss's Break meter before its Fury escalates, spending a shared party resource at the right
moment, and executing timed inputs on every strike and every block.

---

## Credits

All third-party assets are listed with their use.

| Asset | Used for | Source |
|---|---|---|
| Holotna Environment | Arena terrain, foliage, rocks, skybox | Unity Asset Store |
| FourEvilDragonsPBR | Dragon boss model + animations | Unity Asset Store |
| Pro Sword and Shield Pack | Warrior hero (model, animations, weapons) | Unity Asset Store |
| Pro Magic Pack | Mage hero (model, animations) | Unity Asset Store |
| Pro Longbow Pack | Thief and Archer heroes (model, animations) | Unity Asset Store |
| Assassin Pack | Evil Warrior boss | Unity Asset Store |
| WizardPolyArt | Black Mage boss (model, animations, staff) | Unity Asset Store |
| ErbGameArt — Fantasy Effects | All ability particle effects | Unity Asset Store |
| URP GanzSe Free Weapons Pack | Weapon meshes | Unity Asset Store |
| SlimUI | UI fonts (Poppins-Bold SDF, Rubik-Medium SDF) | Unity Asset Store |
| TextMeshPro | All in-game text | Unity (built-in package) |
| **Ink** by Inkle Studios | Narrative scripting (see section 11) | Open source (MIT) |
| Sound effects | Elemental hits, dragon roar, UI | Generated for this project (ElevenLabs) |
| Music | Menu and battle loops | Generated for this project |
| Title / menu art | Main menu background and logo | Generated locally for this project |

Everything the project needs is committed to this repository. **No Asset Store purchase, package
install, or editor extension is required to open, run, build or evaluate the game.**

---

## How to Play

### 1. Starting the Game

**Quickest — download the ready-made build**

A packaged Windows build is attached to the repository's
[**Releases**](../../releases) page. Download `ArenaOfTheAlgorithms-Windows-x64.zip`, extract it, and
run `RPGArena.exe`. No Unity install required. (Windows SmartScreen will warn that the executable is
unsigned — choose *More info ▸ Run anyway*.)

**From source (Unity Editor)**

Requires **Unity 6000.3.14f1**. No additional packages to install.

```bash
git clone https://github.com/Azrieli-College-of-Engineering/student-choice-project-moti.git
```

1. Open the **repository root** as the project folder in Unity Hub — the root *is* the Unity project.
2. Open `Assets/_Project/Scenes/Boot.unity` and press **Play**.

`Boot` creates the persistent services (audio mixer, run state) and hands off to the main menu.

> You may also press Play directly on `MainMenu` or `BattleArena`. `GameBootstrap.EnsureRuntime()`
> spawns the persistent services on demand, so direct-play still has working audio, gold and items.
> This is guarded so it can never double-spawn or bounce you back to the menu.

**Building a player**

Menu: **RPGArena ▸ Build ▸ Windows Player (Release)**, or from the command line:

```bash
Unity.exe -batchmode -quit -projectPath . \
  -executeMethod RPGArena.EditorTools.BuildScript.BuildWindows
```

Output: `Builds/RPGArena/RPGArena.exe`. The build script reads the scene list from **Build Settings**
rather than hardcoding it, so a build can never silently disagree with the Build Settings window.

### 2. Gameplay Basics

1. From the main menu choose **Play**, then pick **3 heroes** and a difficulty.
2. A short dialogue plays before each boss. **Your choice here has a mechanical effect** — see section 11.
3. Each round has two phases:
   - **Player Phase** — you choose *which* hero acts (any order, each hero once), then their skill and
     target, then hit the **timing bar**.
   - **Enemy Phase** — minions act, then the boss. Every enemy acts every round; nothing is ever
     silently skipped. Each incoming blow opens a **block window** you can react to.
4. Defeat the boss to earn gold and pick a **boon**, then advance to the next boss.

**How to Play** is also available in-game from the main menu.

### 3. Levels / Stages

Progression is a boss gauntlet rather than levels: **Dragon → Black Mage → Evil Warrior**, in rising
difficulty. Between fights you visit a **Supply Camp** to spend gold and choose a run-wide boon.
Health and mana carry over between fights (with a floor, so a run can never become mathematically
unwinnable). A full run is roughly **5–15 minutes**.

---

## Controls

The game is **mouse-driven**, with a keyboard shortcut for the timed action commands.

| Input | Action |
|---|---|
| **Left Mouse Button** | Select a hero, a skill, a target, an item, or any menu button |
| **Spacebar** *(or left click anywhere)* | Hit the **timed strike bar**, the **block window**, and the **follow-up** prompt |
| **Esc** | Pause / Resume |
| **Mouse hover** | Preview a skill's damage, cost, hit chance and active combo tags before committing |

The timed prompts accept **Spacebar, a left click anywhere on screen, or a touch** — deliberately so.
Requiring the click to land on the small moving bar turned a reaction test into an aiming test.

**Quit behaviour** is handled correctly for both contexts: in a built player the Exit button calls
`Application.Quit()`; in the Editor it stops Play mode instead.

---

## Objectives & Goals

- **Main Objective**: defeat the boss gauntlet with a party of three heroes.
- **Win condition**: the final boss is defeated. A run-complete screen is shown.
- **Lose condition**: the entire party is knocked out. A clear Game Over screen offers **Retry**,
  **Main Menu** or **Quit**.
- **Secondary goals**:
  - Reach a higher **fight grade** (S / A / B / C) — the grade seeds your starting Valor in the next fight.
  - Break the boss before its Fury escalates.
  - Accumulate gold and assemble a boon set that changes how the run plays.

---

## Game Mechanics & Features

Eight distinct gameplay mechanics, each affecting player decisions and game state. *(Minimum required: 5.)*

**1. Cross-class elemental combo web (an exclusive ladder).**
Status effects combine *across different heroes*: `Wet → Ice = FREEZE → Physical = SHATTER (×2.3)`.
Two further lines exist at lower payoff (HUNT, COATING). The physical lines are an **exclusive
ladder, not additive** — when they were additive, a fully loaded target took ~5.8× damage and fights
collapsed to under four rounds, so only the highest line applies. Reaching the top line requires
coordinating two different heroes across two rounds, which is the core decision of the game.

**2. Break & Fury — the pressure clock.**
Every hit builds a **Break** meter on the boss. Meanwhile the boss accumulates **Fury** on each of its
turns, escalating its damage. **Breaking** it vents that Fury to zero, strips its elemental resistance,
and costs it turns. Ignoring the meter is a slow, certain loss.

**3. Action commands — timed strike, timed block, and follow-up.**
Every damaging skill opens a needle bar that sweeps out-and-back; hitting the gold band multiplies the
blow (PERFECT / GOOD / miss). Every *incoming* attack opens a **block window** whose needle speed and
phase are randomised, so a memorised rhythm cannot beat it. A landed blow has a chance to open a
surprise **follow-up** prompt with a very short window. All three feed the same damage pipeline as a
multiplier on base power — they are mechanics, not animations.

**4. Risk-die ultimates.**
Each hero's fifth skill rolls a d20 across five outcome bands: Backfire / Whiff / Normal / Big /
Jackpot. Ultimates require **two** timing bars rather than one, so the strongest button in the game is
also the hardest to execute, and a hero charges up visibly before it fires.

**5. Positioning — front row / back row.**
Heroes can swap rows mid-fight. The back row mitigates single-target melee damage but **not** area
attacks, so turtling the whole party is punished by the boss's AoE. A live trade-off, not a stat.

**6. Valor / Overdrive — a shared party resource.**
A party-wide meter charged by combos, support actions and taking risks. A full meter is a **choice of
three spends** — Surge (burst damage), Sunder (instant Break progress), or Rally (party heal) — so
spending it is a tactical decision rather than a single button.

**7. Per-boss identity mechanics.**
Each boss changes the rules rather than the numbers. The **Dragon** telegraphs a charged attack you
race to Break, and never loses a turn to control effects. The **Black Mage** *devours* the setup
statuses you place on him, healing and gaining Fury — specifically punishing the strategy that beats
boss one. The **Evil Warrior** bleeds off unconverted Break progress every turn, so chipping and
turtling can never bank a Break.

**8. Run economy — gold, shop items, and rule-changing boons.**
Minions drop gold; a Supply Camp between fights sells consumables and a rest whose price climbs each
time. Boons are written to change **rules**, not just stats (e.g. control effects lasting longer,
Break persisting between turns) — a stat card makes the same fight easier, a rule card makes it a
different fight. HP and MP carry between fights as fractions with a floor.

### Supporting systems

- **Inventory / items**: consumables bought with gold and used as a hero's action in battle.
- **Progress tracking**: HP/MP bars, Break and Fury meters, Valor meter, gold, bosses cleared, and a
  per-fight grade.
- **Enemy intent preview**: the HUD shows what each enemy will do next turn.
- **Difficulty setting**: Easy / Hard, chosen at character select, scaling enemy damage and health.

---

## System Requirements

Developed and tested on Windows 10 Pro (64-bit). Figures below are for the Windows player build.

- **Minimum Requirements**:
  - OS: Windows 10 (64-bit)
  - Processor: any modern x64 CPU (dual-core or better)
  - Memory: 4 GB RAM
  - Graphics: DirectX 11 capable GPU
  - Storage: ~1 GB free (the build is ~390 MB)

- **Recommended Requirements**:
  - OS: Windows 10 / 11 (64-bit)
  - Memory: 8 GB RAM
  - Graphics: dedicated GPU
  - Display: 1920×1080

The UI is resolution-responsive (Canvas Scaler, scale-with-screen-size) and was verified at several
aspect ratios.

---

## Known Issues & Troubleshooting

- **Music loops are short.** The two generated music tracks are brief loops and repeat noticeably.
  *Workaround*: music volume is adjustable in Settings (main menu and pause menu).
- **No melee slash-arc VFX.** The only asset pack that provided slash arcs shipped materials
  referencing shader GUIDs that are absent from the pack itself, so every one of those prefabs
  rendered as Unity's magenta error shader. All 45 affected materials were traced and the pack was
  removed; the abilities were re-pointed at URP-safe effects. Melee therefore reads as an impact burst
  rather than a blade arc. This was a deliberate correctness-over-appearance decision.
- **A brief flat-pink disc can appear during the Black Mage and Evil Warrior fights.** A cosmetic
  particle artifact lasting about two seconds. It is *not* a broken asset: a full sweep found no
  material with a missing or unsupported shader, no renderer with an empty material slot, and no
  ability pointing at the built-in-render-pipeline projector variants, and a live watcher scanning
  every renderer each frame through both fights never caught the error shader. Left documented rather
  than guessed at.
- **First launch may pause briefly** while shaders warm up.
- **Text appears as empty boxes**: means TextMeshPro Essentials are not imported.
  *Fix*: **Window ▸ TextMeshPro ▸ Import TMP Essential Resources**. (They are committed here, so this
  should not occur from a clean clone.)
- **Nothing happens on pressing Play in a mid-game scene**: open `Assets/_Project/Scenes/Boot.unity`.
  Direct-play is supported, but `Boot` is the intended entry point.

---

## Additional Notes

- **Game Version**: 1.0 (submission build)
- **Last Update**: August 2026
- **Contact**: Moti Yeshayahu — motisoftwares@gmail.com
- **Scenes**: `Boot` → `MainMenu` → `BattleArena` (three scenes, with proper transitions; the menu and
  the gameplay are never in the same scene).
- **Audio** is routed through an **AudioMixer** (`GameMixer`) with exposed Master / Music / SFX buses
  driven by the settings sliders — no hardcoded volumes. A lowpass snapshot ducks the mix while paused.
- **Animation** uses **Animator Controllers** with proper state machines per character (locomotion
  blend trees, attack variants, hit reactions, death). All clips are Humanoid-retargeted.

---

## Media

> ### ▶ Gameplay Video
> **YouTube:** `<PASTE LINK HERE>`
>
> *(5–10 minute narrated walkthrough of the gameplay and the project's inner workings.)*

### Screenshots

| Main menu | Battle |
|---|---|
| ![Main menu](docs/screenshots/menu.png) | ![Battle](docs/screenshots/battle.png) |

| Skill selection & damage preview | Action command (timed strike) |
|---|---|
| ![Skills](docs/screenshots/skills.png) | ![Timing bar](docs/screenshots/timing.png) |

---
---

## External Framework Integration — **Ink**

*(Bonus criterion: External Framework / API Integration)*
**Framework:** [Ink](https://www.inklestudios.com/ink/) by Inkle Studios — a narrative scripting
language with its own runtime, variable store and branching model. Open source (MIT), non-Unity.

### Why this framework was chosen

The bosses needed to read as *characters*, and I wanted the player's pre-fight attitude to carry
mechanical weight instead of being decoration. Doing that by hand would have meant writing a dialogue
parser, a branching model and a persistent variable store — three systems that are not the subject of
this project. Ink provides all three, and keeps the writing in plain `.ink` files that can be edited
and re-compiled without touching any C#.

### How it was integrated

`NarrativeRunner` implements the project's **own** `IBattleIntro` interface. `BattleController` locates
it *by interface* and waits on `IsIntroDone` before round one. Consequences:

- The combat assembly never references the narrative assembly — the dependency runs one way only.
- If no story is present, the fight simply starts normally. The integration is optional at runtime and
  cannot break combat.
- Six stories ship: an intro and an outro for each of the three bosses.

### How it interacts with game systems — **two-way**

**Ink → game.** The story calls into C# through `BindExternalFunction`:

```ink
* [Mock him — "All that power, and still bound to this arena."]
    ~ StartWithTelegraph()      // the boss opens the fight already charging
* [Study him — read the currents of his magic.]
    ~ RevealWeakness()          // the HUD reveals its elemental weakness
```

`BattleController` consumes those flags before the first round: `StartTelegraph` makes the boss open
mid-charge, and `RevealWeak` sets `Context.weaknessRevealed`, which the HUD reads to display the boss's
actual elemental weakness instead of the *"study the boss to reveal its weakness"* placeholder.

**Game → Ink.** The outro pushes the battle result back into Ink's variable store:

```csharp
story.variablesState["won"]         = won;
story.variablesState["broke_boss"]  = brokeBoss;
story.variablesState["heroes_lost"] = heroesLost;
```

so the epilogue branches on *how* you won — flawlessly, by Breaking the boss, or at a cost.

### Demonstrated impact

A dialogue choice is a real tactical decision with opposed costs. **Taunting** trades safety for
tempo: the boss opens already charging, which is dangerous but hands you an immediate Break window.
**Studying** trades that tempo for information: you see the elemental weakness from round one and can
plan the FROST combo line instead of discovering it by trial. Persistent variables, conditional
branching, story flags affecting combat state, and combat state affecting the story are all present.

---

## Cheat Manager

*(Bonus criterion: Cheat Manager)*

`Assets/_Project/Scripts/Cheats/CheatManager.cs`, in its **own assembly** (`RPGArena.Cheats`) whose
`.asmdef` carries a define constraint:

```json
"defineConstraints": [ "UNITY_EDITOR || DEVELOPMENT_BUILD" ]
```

The whole assembly is therefore excluded from release builds **by the compiler**, not merely by an
`#if` wrapped around a method body that someone could forget to add. It cannot ship by accident.

It self-installs through `RuntimeInitializeOnLoadMethod`, so no scene wiring is needed and the shipped
scenes contain no dev-only references that could dangle. **Press the backquote key (`` ` ``) in-game**
to toggle the panel.

Available functions, chosen so a grader can reach and demonstrate any part of the game quickly:

| Group | Functions |
|---|---|
| Party | God Mode (invincible), Infinite MP, refill HP + MP, wipe party (force the lose state) |
| Boss | −25% HP, set to 1 HP, kill (force the win state), force an instant Break, heal to full |
| Statuses | Apply Wet / Oiled / Marked / Frozen directly, to test any combo line instantly |
| Valor | Fill or empty the party Overdrive meter |
| Run | +500 gold, grant every shop item, **skip to the next boss** |
| Time | Time-scale slider (slow-motion / fast-forward) and reset |
| Info | Live boss AI cycle index and currently telegraphed ability |

---

## AI Systems

*(Bonus criterion: AI Implementation)*

Enemy brains are **ScriptableObject strategies** implementing `AIBehavior.DecideAction`, so adding a
new brain is a new asset rather than a new branch in a switch statement:

- **DragonCycleAI** — a telegraphed attack rotation that shifts at 50% and 15% health thresholds, so
  the fight has readable phases.
- **DevourerAI** — consumes the player's setup statuses to heal and gain Fury; this drives the Black
  Mage's anti-setup identity.
- **AggressiveAI** — threat-aware: it respects Taunt, and switches to a deterministic execute branch
  against low-health targets.
- **SequenceAI** — minion rotation behaviour.

Each brain also implements `PreviewIntent`, which powers the HUD's "what the enemy will do next" panel.
That method is required to be **side-effect free** — any RNG draw or state write inside it would make
the preview lie to the player, so the contract is deliberate and documented.

---

## Architecture & Code Organisation

The project is split into **eight assembly definitions** with a strict one-way dependency rule:

```
RPGArena.Core  ←  RPGArena.Gameplay  ←  RPGArena.UI
                        ↑
        Audio ─┘   Narrative ─┘   Cheats ─┘      (+ Tests, EditorTools)
```

`Gameplay` may never name a UI type. Where combat genuinely needs presentation — camera focus, ability
VFX, the intro dialogue — it goes through small interfaces (`IActionCamera`, `IAbilityFx`,
`IBattleIntro`) that the UI and narrative layers implement.

The payoff is that the combat logic is **headless**: `BattleManager` and `DamagePipeline` run a
complete fight with no scene, no camera and no prefabs, which is what makes the game testable and
balanceable (see below).

- **Logic** (`BattleManager`, `DamagePipeline`, `SynergyResolver`, `StaggerSystem`, `ChargeSystem`) —
  presentation-free.
- **Live game** (`BattleController`) — mirrors the same invariants for real-time play.
- **Data** — every ability, boss, boon, item and enemy brain is a ScriptableObject; every tuning
  constant lives in a single `BalanceConfig` asset.
- **Events** — ScriptableObject event channels decouple systems; all raisers are null-safe so the
  headless logic runs silently.

---

## Testing

**54 EditMode tests**, runnable from the Test Runner window or the command line:

```bash
Unity.exe -runTests -batchmode -projectPath . -testPlatform EditMode -testResults results.xml
```

They function as a **design gate**, not only as regression cover:

- `SpamLosesTests` — **fails if a basic-attack-only party wins.** The design contract stated at the top
  of this README is enforced as a build failure rather than living in a document I could drift away from.
- `PartyTrioTests` — every party composition must still be able to clear the boss, so the game cannot be
  balanced around one favourite team.
- `RiskDiceTests` — verifies that no risk stake strictly dominates another, i.e. that the safe option and
  the greedy option are a genuine trade-off.
- `BossRosterTests`, `BoonRulesTests`, `InvariantFuzzTests`, `LogicSoundnessTests`.

---

## Project Management

Solo project (approved). Work is tracked as **small, single-purpose commits** with an `area: what`
message convention, so the history reads as a development log — each message records *what was
measured* and *why* the change was made, not merely what changed.

Balance work is **measurement-driven** rather than intuition-driven: changes are validated by running
dozens of simulated fights headlessly across many random seeds and reading the aggregate result, then
comparing against the design gate. Several findings surfaced only that way and would have been invisible
from playing a single fight — for example, that the Black Mage's signature Devour mechanic was firing in
only 4 fights out of 24, and that the "Hard" difficulty setting was raising a base stat that barely fed
into the final damage number, making it about 3% harder instead of 15% (and leaving the Black Mage
entirely immune to it, because he deals magic damage).

`CLAUDE.md` in the repository root is the project's engineering log: architecture decisions, root-caused
bugs, and hard-won gotchas, kept so the same mistake is not made twice.
