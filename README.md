# Arena of the Algorithms

A turn-based boss-rush RPG built in Unity 6 (URP), where **spamming the basic attack is guaranteed to lose**.

You take a party of three heroes through a gauntlet of bosses. Every round asks for both a *tactical*
decision (which hero acts, which element, when to spend a Break window) and *execution* (timed strike
bars, timed blocks). The design contract is enforced by an automated test that **fails the build if a
basic-attack-only party can win**.

---

## 📺 Gameplay Video

> **▶ YouTube:** `<PASTE LINK HERE>`
>
> *(5–10 minute narrated walkthrough of gameplay and the project's inner workings.)*

## 🖼 Screenshots

| Main menu | Battle |
|---|---|
| ![Main menu](docs/screenshots/menu.png) | ![Battle](docs/screenshots/battle.png) |

| Skill selection & damage preview | Action command (timed strike) |
|---|---|
| ![Skills](docs/screenshots/skills.png) | ![Timing bar](docs/screenshots/timing.png) |

---

## 🚀 Setup & Running

**Requirements:** Unity **6000.3.14f1** (Unity 6). No paid Asset Store tools, no extra packages to install —
everything needed is committed in the repo.

```bash
git clone <this-repo>
```

1. Open the folder with Unity Hub (open the **repository root** — it is the Unity project folder).
2. Open `Assets/_Project/Scenes/Boot.unity` and press **Play**.

`Boot` creates the persistent services (audio, run state) and hands off to the main menu.

> You can also press Play directly on `MainMenu` or `BattleArena` — `GameBootstrap.EnsureRuntime()`
> spawns the services on demand, so direct-play still gets audio, gold and items.

### Building

Menu: **RPGArena ▸ Build ▸ Windows Player (Release)**, or headlessly:

```bash
Unity.exe -batchmode -quit -projectPath . \
  -executeMethod RPGArena.EditorTools.BuildScript.BuildWindows
```

Output: `Builds/RPGArena/RPGArena.exe`. Scenes are read from Build Settings, never hardcoded, so the
build cannot silently disagree with the Build Settings window.

---

## 🎮 Controls

The game is **mouse-driven with keyboard shortcuts** for the timed action commands.

| Input | Action |
|---|---|
| **Mouse — Left Click** | Select a hero, a skill, a target, or any menu button |
| **Space** *(or click anywhere)* | Hit the **timed strike bar**, the **block window**, and the **follow-up** prompt |
| **Esc** | Pause / resume |

The timed prompts accept **Space, left click anywhere on screen, or touch** — deliberately, because
requiring the click to land on the small moving bar turned a reaction test into an aiming test.

**How to Play** is also available in-game from the main menu.

---

## 🎯 The Game

- **Objective:** defeat the boss gauntlet with a party of 3 heroes.
- **Win:** the final boss falls.
- **Lose:** the whole party is wiped out. You can retry the fight or return to the menu.
- **Length:** ~5–15 minutes for a full run.

### A round, step by step

1. **Player Phase** — you choose *which* hero acts (any order, each once), then their skill and target,
   then hit the timing bar.
2. **Enemy Phase** — minions act, then the boss. **Every enemy acts every round** — no dead turns.
   Each incoming blow opens a block window.
3. Win/lose checks, then the next round.

---

## ⚙️ Gameplay Mechanics *(rubric: minimum 5 distinct, moderately complex)*

**1. Cross-class combo web (exclusive ladder).**
Statuses combine across heroes: `Wet → Ice = FREEZE → Physical = SHATTER (×2.3)`. Two further lines
(HUNT, COATING) exist at lower payoff. The physical lines are an *exclusive ladder*, not additive —
stacked, they collapsed fights to a third of their length. Requires coordinating two different heroes.

**2. Break & Fury (the pressure clock).**
Every hit builds a Break meter. The boss gains **Fury** each of its turns, escalating its damage — and
**Breaking it vents that to zero** and strips its elemental resistance. Ignoring the meter is a slow loss.

**3. Action commands — timed strike, timed block, follow-up.**
A needle sweeps out-and-back; hitting gold multiplies the blow. Incoming attacks open a block window
with a randomised needle speed so a metronome press can't cheese it. Landed blows can open a surprise
follow-up prompt. All three feed the *same* damage pipeline as a multiplier on base power.

**4. Risk-die ultimates.**
Each hero's fifth skill rolls a d20: Backfire / Whiff / Normal / Big / Jackpot. Ultimates require
**two** timing bars instead of one, so the biggest button is also the hardest to execute.

**5. Positioning (front row / back row).**
Heroes can swap rows mid-fight. The back row mitigates single-target physical damage but **not** area
attacks — so turtling the whole party is punished by the boss's AoE.

**6. Valor / Overdrive.**
A shared party meter charged by combos and support actions. A full meter is a *choice* of three
spends — Surge (damage), Sunder (instant Break progress), or Rally (party heal).

**7. Per-boss identity mechanics.**
Bosses are different *puzzles*, not bigger HP bars. The Dragon telegraphs a charge you race to Break
and never loses a turn to freezing. The Black Mage **devours** the setup statuses you place on him,
healing and gaining Fury — punishing the exact strategy that beats boss one.

**8. Run economy — gold, items, boons.**
Minions drop gold; a shop between fights sells consumables and rest. Boons change *rules*, not just
stats. HP/MP carry between fights with a floor so a run can never become mathematically unwinnable.

---

## 🗺 Scenes & Flow

| Scene | Role |
|---|---|
| `Boot` | Creates persistent services (audio, run state), then loads the menu |
| `MainMenu` | Play · How to Play · Settings · Credits · Quit, plus character select |
| `BattleArena` | The playable battle, pause menu, and end-state flow |

**Pause menu** (Esc): Resume · Restart Fight · **Settings** (volume sliders) · Exit to Main Menu.
Settings is a *page of the pause overlay*, so changing volume mid-fight never unloads the battle, and
it drives the same mixer buses as the main menu.

**Game over / end state:** a clear end screen with Retry, Main Menu and Quit.

---

## 🧩 External Framework Integration — **Ink** by Inkle Studios

*(Rubric bonus: External Framework / API Integration)*

### Why Ink
The bosses needed to feel like *characters*, and I wanted the player's pre-fight attitude to have
mechanical weight rather than being flavour text. Hand-rolling branching dialogue with state would have
meant writing a parser and a variable store; Ink provides both, and keeps the writing in `.ink` files
that can be edited without touching C#.

### How it is integrated
`NarrativeRunner` implements the project's own `IBattleIntro` interface. `BattleController` finds it
**by interface** and waits on `IsIntroDone` before round one — so the combat assembly never references
the narrative assembly, and the fight runs normally if no story is present.

Six stories ship: an intro and an outro for each boss.

### How it interacts with game systems — **two-way**

**Ink → game.** The story calls back into C# through `BindExternalFunction`:

```ink
* [Mock him — "All that power, and still bound to this arena."]
    ~ StartWithTelegraph()      // the boss opens the fight already charging
* [Study him — read the currents of his magic.]
    ~ RevealWeakness()          // the HUD reveals its elemental weakness
```

`BattleController` consumes those flags before the first round: `StartTelegraph` makes the boss open
mid-charge, and `RevealWeak` sets `Context.weaknessRevealed`, which the HUD reads to show the boss's
weakness instead of *"study the dragon to reveal its weakness"*.

**Game → Ink.** The outro pushes the battle result back into Ink's variable store:

```csharp
story.variablesState["won"]         = won;
story.variablesState["broke_boss"]  = brokeBoss;
story.variablesState["heroes_lost"] = heroesLost;
```

so the epilogue reacts to *how* you won — flawlessly, by Breaking the boss, or at a cost.

**Impact:** a dialogue choice is a real tactical decision. Taunting trades safety for tempo (the boss
opens charging); studying trades that tempo for information (you see the weakness immediately and can
plan the FROST line from round one).

---

## 🐞 Cheat Manager *(rubric bonus)*

`Assets/_Project/Scripts/Cheats/CheatManager.cs`, in its **own assembly** (`RPGArena.Cheats`) whose
`.asmdef` carries the constraint:

```json
"defineConstraints": [ "UNITY_EDITOR || DEVELOPMENT_BUILD" ]
```

The whole assembly is therefore **excluded from release builds by the compiler**, not merely by an
`#if` around the body — it cannot ship by accident. Available in the Editor and in development builds
for testing and demonstration.

---

## 🤖 AI Systems *(rubric bonus)*

Enemy brains are **ScriptableObject strategies** (`AIBehavior.DecideAction`), so a new brain is a new
asset, not a new branch in a switch:

- **DragonCycleAI** — a telegraphed rotation that shifts at HP thresholds.
- **DevourerAI** — eats the player's setup statuses; drives the Black Mage's anti-setup identity.
- **AggressiveAI** — threat-aware, respects Taunt, and executes low-HP targets.
- **SequenceAI** — minion rotation.

Each brain also implements `PreviewIntent`, which must be **side-effect free** — it powers the HUD's
"what the enemy will do next" panel, and any RNG draw inside it would make the preview lie.

---

## 🏗 Architecture

Eight assemblies with a strict **one-way** dependency rule:

```
RPGArena.Core  ←  RPGArena.Gameplay  ←  RPGArena.UI
                        ↑
        Audio ─┘   Narrative ─┘   Cheats ─┘   (Tests, EditorTools)
```

Gameplay may never name a UI type. Where combat genuinely needs presentation (camera focus, ability
VFX), it goes through small interfaces — `IActionCamera`, `IAbilityFx`, `IBattleIntro` — that the UI
layer implements. This is what keeps the combat logic **headless and testable**.

- **Logic** (`BattleManager`, `DamagePipeline`, `SynergyResolver`, `StaggerSystem`) is presentation-free
  and runs without a scene.
- **Live game** (`BattleController`) mirrors the same invariants for real-time play.
- **Tuning** lives in `BalanceConfig`, a single ScriptableObject.

## ✅ Testing

**54 EditMode tests**, runnable from the CLI:

```bash
Unity.exe -runTests -batchmode -projectPath . -testPlatform EditMode -testResults results.xml
```

They are a **design gate**, not just regression cover:

- `PartyTrioTests` — every party composition must be able to clear the boss.
- `SpamLosesTests` — **fails if a basic-attack-only party wins.** This is the design contract as code.
- `RiskDiceTests` — verifies no risk stake strictly dominates another.
- `InvariantFuzzTests`, `LogicSoundnessTests`, `BossRosterTests`, `BoonRulesTests`.

---

## 🎨 Assets & Credits

| Asset | Use | Source |
|---|---|---|
| Holotna environment | Arena terrain, foliage, rocks | Asset Store |
| FourEvilDragonsPBR | Dragon boss | Asset Store |
| Pro Sword and Shield Pack | Warrior | Asset Store |
| Pro Magic Pack | Mage | Asset Store |
| Pro Longbow Pack | Thief | Asset Store |
| Assassin Pack | Evil Warrior boss | Asset Store |
| WizardPolyArt | Black Mage boss | Asset Store |
| ErbGameArt Fantasy Effects | All ability VFX | Asset Store |
| SlimUI | UI fonts (Poppins, Rubik SDF) | Asset Store |
| Ink (Inkle Studios) | Narrative scripting | Open source |
| Title art | Generated locally, this project | — |
| SFX | Generated (ElevenLabs) | — |

Audio is routed through an **AudioMixer** (`GameMixer`) with exposed Master/Music/SFX buses driven by
the settings sliders — no hardcoded volumes. Characters use **Animator Controllers** with proper state
machines; all animation clips are Humanoid-retargeted.

---

## 📋 Project Management

Solo project (approved). Work is tracked as **small, single-purpose commits** on a feature branch,
merged to `main` — the history reads as a development log, with each commit message recording *what was
measured* and *why the change was made*, not just what changed.

Balance work is **measurement-driven**: changes are validated by sweeping many simulated fights across
seeds and reading the aggregate, rather than by playing one fight and guessing. Several findings in this
project only surfaced that way — for example, that the Black Mage's signature mechanic was firing in
only 4 of 24 fights.

`CLAUDE.md` at the repo root is the project's engineering log: architecture decisions, hard-won gotchas,
and root-caused bugs, kept so the same mistake is not made twice.

---

## ⚠️ Known Limitations

- The two music tracks are short placeholder loops.
- There is no melee slash-arc VFX: the only pack that provided them shipped materials referencing
  shader GUIDs absent from the project, so every one of those prefabs rendered as Unity's magenta error
  shader. They were removed and the abilities re-pointed at URP-safe effects; melee currently reads as
  an impact burst rather than a blade arc.
