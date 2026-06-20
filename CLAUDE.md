# CLAUDE.md — Arena of the Algorithms

> **This is the master design + engineering specification for the entire project.**
> Place this file at the **repository root** (the folder containing `Assets/`, `Packages/`, `ProjectSettings/`). Claude Code reads it at the start of every session. It is the single source of truth for *what* to build, *how* to build it, and *to what quality bar*. When anything in a prompt conflicts with this file, this file wins unless the user explicitly overrides it.

**Table of contents**
0. How to use this document
1. Project overview & vision
2. Hard requirements checklist
3. Technical stack & environment
4. Architecture
5. Combat design specification
6. Content — the four classes
7. Content — the bosses
8. The Dragon-first build plan (milestones)
9. UI / UX specification
10. Art & asset specification
11. Audio specification
12. Visual polish / game feel specification
13. Narrative specification (Ink bonus)
14. Bonus features specification
15. Project management & process
16. Testing & QA
17. Submission deliverables
18. Working agreement for Claude Code (operational rules)
19. Glossary & quick-reference data tables

---

# 0. HOW TO USE THIS DOCUMENT (read first, every session)

**You are the primary engineer on this project.** The human owner is a software-engineering student whose passion is ML/AI, not game development. The explicit goal is **maximum automation**: you generate code, scenes, prefabs, ScriptableObject assets, Animator Controllers, materials, UI, wiring, and configuration. The human will only do things a tool genuinely cannot — pulling a model from Mixamo, recording a video, clicking a download button. **Default to doing the work yourself through the Unity MCP bridge rather than instructing the human to do it.**

The non-negotiable target is a **complete, visually polished, fun-to-play, and architecturally sophisticated game.** "Automated" must never become "compromised." Every system below is specified to a level where you can implement it without further questions. When details are missing, choose the option that (a) satisfies the grading rubric in §2, (b) matches the architecture in §4, and (c) maximizes player-facing polish — then note the assumption in your summary.

### Operating rules (summary; full version in §18)
1. **Compile after every meaningful change.** Use the MCP bridge to read the Unity console. Never end a task with the project in a non-compiling state. If there are errors, fix them before moving on.
2. **Work in small, verifiable increments.** Implement → compile → quick logic test → commit. Prefer many small commits over one large one.
3. **Commit and push to `main` after each completed step**, with a clear message. `main` is the only graded branch.
4. **Data-driven first.** New content (an ability, a boss, an AI) should be a new ScriptableObject asset, not new branching code. If you find yourself writing a `switch` over content types, stop and make it data.
5. **Keep the low-level English comment style** established in the codebase (see §3.4). Code quality and conceptual clarity are graded.
6. **Never add paid Asset Store packages or dependencies the instructor would have to buy/install** to open, run, build, or evaluate the project. Any dependency must be free, essential, included/configured, and documented.
7. **Build the Dragon fight first** (see §1.5 and §8). Establish every core system against one boss, make it excellent, then expand. Do not spread effort thin across all three bosses early.
8. **When ambiguous, make the rubric-satisfying, polish-maximizing choice and proceed.** Surface the assumption; don't block on it.
9. **Respect the milestone order in §8.** Do not skip ahead to assets before the systems they decorate are working with primitives.
10. **At the end of every session**, output: what changed, current compile/play status, what's next, and any decisions made.

---

# 1. PROJECT OVERVIEW & VISION

## 1.1 Elevator pitch
**Arena of the Algorithms** (working title; repo codename: *RPG Arena*) is a **2.5D turn-based boss-rush RPG**. There is no overworld and no trash mobs — the entire game is a gauntlet of **three set-piece boss battles**, each a tense, puzzle-like duel against a distinct, intelligent enemy. The player assembles a party of **three heroes drawn from four classic adventurer classes** and must read each boss's patterns, exploit elemental weaknesses, build up a **Stagger** break, and chain cross-class **status synergies** to win. Think *Octopath Traveler*'s Break system and *Final Fantasy XIII*'s stagger, distilled into short, replayable, single-encounter fights, wearing the skin of classic *MapleStory*.

The design deliberately concentrates engineering effort into **systems and logic** rather than content volume or bespoke art — which is what earns marks in a software-engineering course, and what suits an owner who wants to show off clean architecture, not 3D modeling.

## 1.2 Design pillars (every decision serves these)
1. **Readable depth.** Each fight is a solvable puzzle. The player always has enough information (telegraphs, weakness hints, clear numbers) to make a smart decision, and smart decisions are strongly rewarded (extra turns, big damage on Stagger). Depth comes from *interactions between simple systems*, not a wall of stats.
2. **Every turn matters.** No filler turns. Resource tension (MP, cooldowns, Stagger timing) and a meaningful choice on every action. Rewarding weakness-hits with **action-economy advantage** is the spine of the combat (see §5.5).
3. **Class identity is sacred.** The four classes — Warrior, Mage, Thief, Archer — must feel *completely different* to pilot, echoing their MapleStory roots (see §6). A player picks a party for synergy, not raw power.
4. **Game feel over fidelity.** The game must feel *juicy* and alive — hit-stop, screen shake, punchy numbers, satisfying VFX and audio on every impact (see §12) — even though the underlying assets are off-the-shelf. Polish is cheap and high-impact; we lean on it hard.
5. **Architectural cleanliness.** The codebase is the deliverable as much as the game. Named, textbook patterns (Command, State, Strategy, ScriptableObject-driven data and events) so the architecture is easy to explain in the required demo video and viva.

## 1.3 The MapleStory homage
The four hero classes are a direct, affectionate reference to **MapleStory's original four adventurer jobs**, and the bosses echo MapleStory's iconography (the **Black Mage** is MapleStory's legendary antagonist; dragons are signature MapleStory bosses). The stat system mirrors MapleStory's four base stats, mapping one primary stat to each class:

| Class | MapleStory primary stat | Combat identity (MapleStory roots) |
|---|---|---|
| **Warrior** | **STR** (Strength) | High HP/defense melee bruiser. Power Strike (heavy single hit), Slash Blast (multi-hit), Rage (party attack buff), Iron Body / Hyper Body (defense / HP). The frontline anchor. |
| **Mage** | **INT** (Intelligence) | Elemental nuker **and** healer. Splits across Fire/Poison (DoT burn), Ice/Lightning (freeze/stun + the elemental matrix), and Cleric (Heal, Bless, holy damage). Glass cannon with party sustain. Magic Guard converts MP to a damage shield. |
| **Thief** | **LUK** (Luck) | High-crit, high-evasion burst & utility. Lucky Seven (luck-scaling multi-hit), Dark Sight (untargetable stealth turn), Haste (party speed buff), debuffs and status-flag setup (apply *Oiled* / *Wet* for synergies). The combo enabler. |
| **Archer** | **DEX** (Dexterity) | Ranged precision dealer. Highest accuracy and crit. Double Shot / Arrow Blow (reliable ranged hits), Soul Arrow (ignores some defense), Puppet (summon a decoy that draws boss aggro), Eye of Amazon (party accuracy). Consistent, safe damage from range. |

This homage informs flavor, ability names, and class fantasy. It does **not** require any MapleStory assets, IP, or art — use original/generic equivalents. Treat MapleStory as *inspiration and naming flavor only*; never ship copyrighted MapleStory content.

## 1.4 Genre & references (study these for design intent)
- **Break / Weakness system** → *Octopath Traveler*, *Bravely Default* ("Boost/Break"). Hitting a shielded enemy's weakness depletes shield points; at zero the enemy is **Broken** (our **Stagger**): loses a turn and takes massively increased damage.
- **Stagger / Chain gauge** → *Final Fantasy XIII*, *Final Fantasy VII Remake*, *Mobius Final Fantasy* (the rare turn-based break-meter focus). Sustained pressure fills a gauge; on break, a damage-multiplier window opens.
- **"Press Turn" / extra-turn economy** → *Shin Megami Tensei: Nocturne / Persona*. Exploiting weakness or landing a crit grants action-economy advantage.
- **Data-driven action system** → *Slay the Spire* (each action is a Command whose data lives in a ScriptableObject; the turn loop is a state machine). This is the architectural model we follow (see §4.3).
- **Boss-rush structure & telegraphed boss patterns** → *Furi*, *Shadow of the Colossus*, *Cuphead*. Each boss is a learnable pattern with clear tells.

## 1.5 Scope & the Dragon-first strategy
The course expects **2–3 scenes, ~5–15 minutes of play, ≥5 distinct moderately-complex mechanics, ≥10 assets, ≥2 animated characters**. Our design comfortably exceeds this. But to avoid thin, broken breadth, we build **depth-first around a single boss**:

> **Build order: get the full game *complete and excellent* for ONE boss — The Dragon — including all four playable classes, all core systems, full UI, juice, audio, and narrative. Only then clone the boss-specific layer to add the Black Mage and Evil Warrior.**

The Dragon is the anchor boss because its **cyclic, telegraphed pattern** (attack → defend → charge → unleash; see §7.2) is the clearest showcase for every core system: the elemental matrix, Stagger timing (punish the charge window), defensive play, healing, and party synergy. Once the Dragon fight is a polished vertical slice, the other two bosses are *content*, not *engineering*.

The three bosses (full specs in §7):
1. **The Dragon** — *first build target.* Cyclic, telegraphed; tests timing and defense.
2. **The Black Mage** — high-variance chaos caster; tests adaptability and status cleansing. (MapleStory's iconic villain.)
3. **The Evil Warrior** — relentless aggressor that targets your weakest hero; tests protection and tempo.

## 1.6 What "done" and "excellent" mean
- **Done** = the §16 sanity check passes: opens from a clean clone, all scenes in Build Settings, playable start→finish, no console error spam, Exit quits the build, README complete.
- **Excellent** (the actual target) = the 100% column of every rubric row in §2: fully functional and bug-free, 5+ genuinely distinct moderately-complex mechanics, clean readable pattern-based code, thorough documentation, clear conceptual demonstration, and as many of the +35 bonus points as we can bank (Ink, AI, Cheat Manager, Creativity).

---

# 2. HARD REQUIREMENTS CHECKLIST (from the assignment)

Everything here is a grading requirement. Treat each as a definition-of-done item. Point values are from the rubric (total 100 + up to 35 bonus).

## 2.1 Graded rubric rows
| Row | Points | What "Excellent" requires | Where we satisfy it |
|---|---|---|---|
| **Functionality** | 30 | Fully functional, meets all criteria, no bugs. | All systems §4–§5; QA §16. |
| **Gameplay Mechanics** | 20 | **5 distinct, moderately-complex** mechanics clearly impacting interactions. | §5: FSM combat, elemental matrix, MP economy, Stagger/Break, status-flag synergies, stance-switching (6 — one to spare). |
| **Code Quality & Readability** | 15 | Organized, readable, consistent style. | §3 conventions; §4 patterns. |
| **Documentation** | 15 | Thorough README: setup, gameplay, controls, assets, management. | §17 + README template. |
| **Conceptual Understanding** | 10 | Clear demonstration of Unity concepts (assets, interactions, animation, transitions). | Demonstrated across build; explained in video §17. |
| **Project Management** | 10 | Effective PM tools, clear roles, consistent task tracking. | Scrum/Trello §15. |
| **Creativity & Extra Effort** *(bonus)* | +10 | Significant creative enhancements / engaging extra features. | Stagger, synergies, stances, juice. |
| **AI Implementation** *(bonus)* | +10 | Effective Unity AI systems (e.g. NavMesh, AI behavior). | Strategy-pattern boss AI §4.14 + optional Behavior Graph. |
| **Cheat Manager** *(bonus)* | +5 | Full cheat manager, Editor/Dev-build only, robust options. | §14.1. |
| **External Framework / API** *(bonus)* | +10 | Clean integration deeply connected to gameplay; strong docs. | Ink narrative §13. |

## 2.2 Baseline MVP / game-flow requirements (mandatory structure)
- [ ] **At least 2 separate scenes** — the game must **not** live in one scene. Minimum: a **Main Menu** scene and a **Gameplay** scene, with proper transitions. (We use `MainMenu` + `BattleArena`; see §4.16, §9.)
- [ ] **Main Menu scene**: Play / Start, How to Play / Instructions, Settings, Credits, Exit / Quit.
- [ ] **Gameplay scene(s)**: the playable experience, complete working controls, clear objective + win condition + lose condition + end state.
- [ ] **Pause Menu**: proper pause logic (`Time.timeScale`), Resume, Restart, Settings, Exit to Main Menu.
- [ ] **Game Over / End State**: clear win/lose/complete indication; restart / menu / exit options. May be a screen or integrated into the menu system if logic and UX are clear.
- [ ] **Complete control scheme**, responsive, documented in README and/or a How to Play screen. The player should never guess how to start, play, pause, restart, or exit.
- [ ] **Player feedback** where relevant: button hover/click feedback, SFX, UI prompts, hit/pickup/damage feedback, success/failure messages.
- [ ] **Audio via AudioMixer** (not hardcoded/disconnected). (§11)
- [ ] **Animation via Animator Controller + state machine** (not loose clips/manual triggers). (§10)
- [ ] **UI/HUD** readable, functional, resolution-responsive; must not break/overlap/become unreadable at common resolutions; supports gameplay clarity, not decoration.
- [ ] **≥10 distinct assets** (models, sprites, audio).
- [ ] **≥3 interaction types** (player input, collision detection, event triggers).
- [ ] **≥2 animated objects/characters.**
- [ ] **Progression/scoring** system (health, points, run progress).
- [ ] **≥1 polished visual element** (particles, camera movement, transitions). We do far more (§12).
- [ ] **Smooth, logical scene transitions.**
- [ ] **Technical stability**: no critical errors, no repeated console errors, no warning spam, no broken references, no missing scenes/assets, no major perf issues, no uncontrolled spawning/leaks, no unused-GameObject accumulation.
- [ ] **`DontDestroyOnLoad`/singletons/static data handled carefully** — no duplicated managers, broken transitions, or inconsistent state after restart. (§4.4 — guarded bootstrap.)
- [ ] **Works in Editor and in a build.** In a build, Exit quits the app; in the Editor it safely stops play mode. All scenes in Build Settings.

## 2.3 Counting rule for "gameplay mechanics"
A gradable mechanic must **meaningfully affect player interaction, decision-making, game-state progression, challenge, or feedback.** Menus, HUD, looping music, simple timers, plain score counters, primitive ScriptableObject data containers, and simple rotating/animating objects **do NOT count** — they are supporting polish. Our five+ counted mechanics (§5) are all genuine gameplay systems with decision impact. When implementing each counted mechanic, make it *affect choices and outcomes*, not just display a number.

---

# 3. TECHNICAL STACK & ENVIRONMENT

## 3.1 Engine & pipeline
- **Unity 6.3 LTS** (`6000.3.x`; project is on `6000.3.14f1`). Do not change the major version.
- **Universal Render Pipeline (URP).** All materials/shaders must be URP-compatible (URP/Lit, URP/Unlit, or Shader Graph targeting URP). Never use Built-in pipeline shaders (they render magenta under URP).
- **Camera: Orthographic.** The deliberate "2.5D" approach — real 3D models, orthographic projection for a clean, readable, flattened stage. Boss and heroes arranged on a shallow stage facing the camera.
- **Render target:** Windows Standalone (`.exe`) primary. Keep everything build-safe (no Editor-only APIs in runtime paths except behind `#if UNITY_EDITOR`).

## 3.2 Required packages (install via Package Manager; all free)
- **Universal RP** (URP) — present.
- **Input System** (`com.unity.inputsystem`) — use the new Input System (action maps) for all controls, not legacy `Input.GetKey` scattered through code. Present (the project shows an `InputSystem_Actions` asset).
- **Cinemachine** (`com.unity.cinemachine`) — camera framing and, importantly, **Cinemachine Impulse** (camera shake on hits; §12).
- **TextMeshPro** — all text uses TMP (sharp, scalable). Ships with Unity 6; import TMP Essentials when prompted.
- **Ink for Unity** (`com.inkle.ink-unity-integration`, MIT) — narrative bonus (§13). Git URL: `https://github.com/inkle/ink-unity-integration.git?path=Packages/Ink`.
- **AI Navigation** (`com.unity.ai.navigation`) — present; optional, only if NavMesh is used for positioning. Not required for turn-based logic.
- **(Optional) DOTween (free)** — popular tweening for UI/number juice. If used, document it. **Prefer a no-dependency coroutine/`Mathf.Lerp` tween helper** unless DOTween clearly saves significant time, to keep the project free of non-essential packages.
- **Unity Test Framework** — a small edit-mode suite on the damage formula and elemental matrix (helps Code Quality / Functionality).

> Packages already present per the project: 2D Sprite, AI Navigation, Burst, Assistant (Unity AI), Collections, Custom NUnit, Input System, JetBrains Rider Editor, Mathematics, MCP for Unity. Build on what's there.

## 3.3 The Claude-Code-via-MCP workflow (how you operate in Unity)
The **MCP for Unity** bridge is installed and connected (client: Claude Code, status: Configured, session active). Through it you can create/modify GameObjects, components, scenes, prefabs, ScriptableObject assets, and read the console. Standard loop for every task:
1. Read/modify scripts (filesystem); create/wire assets and scene objects (MCP).
2. Trigger a compile and **read the Unity console** via MCP. Resolve all errors and meaningful warnings.
3. Where logic is testable headlessly, add/extend an edit-mode test or a temporary `Debug.Log` trace and verify expected output.
4. Summarize, then `git add/commit/push` to `main`.

Keep the human's manual touches minimal and batched (see §10 asset pulls). When you genuinely need a human action, state it explicitly and precisely, then continue with everything else you *can* do.

## 3.4 Coding conventions & style guide (graded — follow exactly)
**General**
- **Language level:** modern C# as supported by Unity 6.3, but favor clarity over cleverness. This code is read and graded by humans and explained in a video.
- **Comment style (required):** keep the existing **low-level English comments every few lines**, explaining intent, logic, and any non-obvious lifecycle/memory behavior, so a non-expert reader can follow. Comment *why*, not just *what*. Match the density/tone of the existing `Entity.cs`/`Ability.cs`.
- **One responsibility per class.** Small, focused MonoBehaviours and ScriptableObjects. If a class exceeds ~300 lines or does two jobs, split it.
- **No magic numbers** in logic — surface tunables as serialized fields on ScriptableObjects so design can be balanced in the Inspector without code changes.

**Naming**
- Types & methods: `PascalCase`. Locals & parameters: `camelCase`. Private fields: `camelCase`, `[SerializeField]` when Inspector-exposed; constants `PascalCase`.
- Clean domain names for data classes: `Ability`, `CharacterDefinition`, `BossDefinition`, `AIBehavior`, `StatusEffectDefinition`, `ElementType`. Asset *files* named by content: `Warrior_PowerStrike.asset`, `Dragon_FlameBreath.asset`.
- Event (SO channel) names: `On<Event>` (e.g. `OnDamageDealt`, `OnTurnStarted`, `OnStaggerBroken`).
- Booleans read as questions: `isStaggered`, `canAct`, `hasActedThisTurn`.

**Structure**
- **Namespaces** rooted at `RPGArena`, sub-namespaced by system: `.Core`, `.Combat`, `.Combat.AI`, `.Combat.Status`, `.Characters`, `.UI`, `.Narrative`, `.Audio`, `.Cheats`, `.Utilities`. Use **assembly definitions** (`.asmdef`) per major folder for fast compiles and explicit dependencies.
- **Serialized references over `Find`/`GetComponent` in `Update`.** Cache in `Awake`/`OnEnable`. Avoid `GameObject.Find`, `Camera.main` in hot paths, `FindObjectOfType` at runtime.
- **Prefer events over polling.** Systems react to SO event channels (§4.13) rather than checking state every `Update`.
- **Coroutines/async for sequencing** the turn flow and animations; never block.
- **Null-safety:** validate serialized references in `OnValidate`/`Awake`; log a clear error naming the missing reference.

**Anti-patterns to avoid (these cost Code Quality marks)**
- God classes; giant `switch`/`if-else` chains over content types (use data/polymorphism instead).
- Scattered singletons and `static` mutable state (use SO variables/channels + a single guarded bootstrap — §4.4).
- Hardcoded audio volumes/clips (use the AudioMixer — §11).
- Loose animation clips played by hand (use Animator Controllers — §10).
- `Resources.Load` everywhere (prefer serialized references; `Resources` only where it clearly simplifies VFX/prefab lookup and is documented).

## 3.5 Folder & namespace structure (create exactly this)
```
Assets/
  _Project/
    Art/
      Models/               // Mixamo/Meshy FBX imports
      Materials/
      VFX/                  // particle prefabs (fire/ice/lightning/heal/impact)
      UI/                   // UI sprites, icons, fonts (TMP)
    Audio/
      Music/
      SFX/
      AudioMixer.mixer
    Scenes/
      Boot.unity            // tiny bootstrap scene (optional, §4.4)
      MainMenu.unity
      BattleArena.unity
    ScriptableObjects/
      Abilities/            // one .asset per ability
      Characters/           // CharacterDefinition per hero class
      Bosses/               // BossDefinition per boss
      AI/                   // AIBehavior strategy assets
      Status/               // StatusEffectDefinition assets
      Elements/             // ElementType definitions + matrix
      Events/               // SO event channels
      Variables/            // SO shared variables (optional)
    Prefabs/
      Combatants/           // hero & boss prefabs
      UI/                   // HUD widgets, floating text
      VFX/                  // wrapped, poolable VFX prefabs
    Scripts/
      Core/                 // bootstrap, scene loading, game state, service refs
      Combat/               // BattleManager FSM, turn system, damage pipeline, targeting
        AI/                 // AIBehavior + concrete strategies
        Status/             // status effect runtime + synergy resolver
        Commands/           // Command pattern action objects
      Characters/           // Entity, stats, party
      Narrative/            // Ink runner + bindings
      UI/                   // menus, HUD, character select, pause, game over
      Audio/                // audio manager, mixer hooks
      Cheats/               // cheat manager (dev-only)
      Utilities/            // helpers, extensions, object pool, juice (shake/hitstop)
    Settings/               // URP assets, input actions, volume profiles
  Ink/                      // .ink source files (auto-compiled to JSON next to them)
```
Mirror with `.asmdef` files: `RPGArena.Core`, `RPGArena.Combat`, `RPGArena.Combat.AI`, `RPGArena.Combat.Status`, `RPGArena.Characters`, `RPGArena.Narrative`, `RPGArena.UI`, `RPGArena.Audio`, `RPGArena.Cheats`, `RPGArena.Utilities`, plus a test assembly `RPGArena.Tests`.

---

# 4. ARCHITECTURE

This is the technical backbone. It is built from four named, textbook patterns so the design is clean, testable, extensible, and **easy to explain in the demo video / viva**. Internalize this section before writing any combat code.

## 4.1 Architectural principles
1. **Separate data from logic.** All *content* (heroes, abilities, bosses, AI, status effects, the elemental matrix) is **ScriptableObject data**. All *behavior* is code that reads that data. Adding content never means editing engine code.
2. **Decouple systems with events.** Systems communicate through **ScriptableObject event channels** (Unity's recommended pattern), not direct references or a web of singletons. The UI doesn't call the BattleManager; it listens to `OnTurnStarted`, `OnDamageDealt`, `OnStaggerBroken`, etc.
3. **Model actions as Commands.** Every combat action (attack, heal, buff, boss move) is a **Command object** created from `Ability` data and executed by the engine. This makes actions queueable, sequenceable, animatable, undoable (for cheats/tests), and uniformly logged — the *Slay the Spire* model.
4. **Drive flow with an explicit State Machine.** The battle is a **finite state machine** with named states. Boss behavior phases are a second, smaller state machine. No implicit state hidden in booleans scattered across classes.
5. **Pluggable behavior via Strategy.** Enemy AI is the **Strategy pattern**: each boss's brain is a swappable `AIBehavior` ScriptableObject. New boss behavior = new asset, zero engine changes.
6. **Composition over inheritance** for combatants: an `Entity` is composed of a stat block, an ability list, a status-effect container, and (for bosses) an AI brain — rather than a deep class hierarchy.

## 4.2 System map (dependency direction)
```
            ScriptableObject DATA (content, no logic)
   ┌─────────────────────────────────────────────────────────┐
   │ ElementType  ElementMatrix  Ability  StatusEffectDef     │
   │ CharacterDefinition  BossDefinition  AIBehavior          │
   └─────────────────────────────────────────────────────────┘
                         ▲ read by
   ┌─────────────────────────────────────────────────────────┐
   │ CORE LOGIC                                                │
   │  Entity (stats + abilities + status + optional AI)        │
   │  BattleManager (FSM)  TurnSystem  DamagePipeline          │
   │  TargetingSystem  StatusEffectSystem  StaggerSystem       │
   │  Command objects (AttackCommand, HealCommand, ...)        │
   │  AIBehavior strategies (DragonCycleAI, ...)               │
   └─────────────────────────────────────────────────────────┘
        │ raises ▼                         ▲ listens
   ┌─────────────────────────────────────────────────────────┐
   │ SO EVENT CHANNELS (the decoupling layer)                  │
   │  OnTurnStarted OnActionExecuted OnDamageDealt             │
   │  OnHealed OnStatusApplied OnStaggerBroken OnEntityDied    │
   │  OnBattleWon OnBattleLost OnBossPhaseChanged ...          │
   └─────────────────────────────────────────────────────────┘
        ▲ listens (never calls core directly)
   ┌─────────────────────────────────────────────────────────┐
   │ PRESENTATION (reacts only)                                │
   │  HUD  HealthBars  FloatingText  ActionMenu                │
   │  AudioManager  JuiceController (shake/hitstop/flash)      │
   │  AnimationDriver  VFXSpawner  NarrativeRunner (Ink)       │
   └─────────────────────────────────────────────────────────┘
```
**Rule:** dependencies point *downward* (presentation depends on core/data; core depends on data). Core never references presentation directly — it only raises events. This keeps logic testable without a scene.

## 4.3 The action model: Command pattern
- `Ability` (ScriptableObject) is **data**: name, description, icon, element, power, MP cost, target rule, status effects to apply, VFX prefab, SFX, animation trigger, cooldown, tags.
- When an entity acts, the engine builds a **Command** (a plain C# object) from the chosen `Ability` + caster + target(s):
  - `ICommand { IEnumerator Execute(BattleContext ctx); string DescribeForLog(); }`
  - Concrete commands: `AttackCommand`, `MultiHitAttackCommand`, `HealCommand`, `BuffCommand`, `DebuffCommand`, `ApplyStatusCommand`, `StanceSwitchCommand`, `DefendCommand`, `BossMoveCommand`, `CompositeCommand` (runs a sequence).
- The engine **queues** commands and executes them one at a time as a coroutine, so animations, VFX, hit-stop, and floating text sequence cleanly. The Command calls into the **DamagePipeline** and **StatusEffectSystem**, and raises the relevant **event channels**; presentation reacts.
- Benefits we get for free: uniform logging (for the combat log UI and debugging), trivial cheat hooks (inject an `InstantWinCommand`), easy unit testing (execute a command against a headless `BattleContext` and assert results), and clean replay/queueing.

## 4.4 Bootstrap, services, and the singleton question
- **Avoid scattered singletons and mutable statics** (a graded anti-pattern). Instead:
  - A single, **guarded** `GameBootstrap` MonoBehaviour lives in a tiny `Boot` scene (or as a `RuntimeInitializeOnLoadMethod`). It instantiates persistent managers **once**, and uses the standard duplicate-guard so a returned-to scene never creates a second copy:
    ```csharp
    // Standard singleton guard to prevent duplicate managers across scene loads.
    if (Instance != null && Instance != this) { Destroy(gameObject); return; }
    Instance = this; DontDestroyOnLoad(gameObject);
    ```
  - Persistent services (AudioManager, SceneLoader, RunState) are reached via a lightweight **service reference** (a serialized reference or a single small `Services` locator), *not* a pile of global statics.
- **Why this matters for grading:** the rubric explicitly warns about duplicated managers / broken transitions / inconsistent state after restart. Our bootstrap + event-driven design avoids all three. Test it: start → win → return to menu → start again, and confirm no duplicate managers (log a warning if a second `GameBootstrap` ever awakes).

## 4.5 The data model
**`StatBlock`** (plain serializable struct/class, embedded in definitions and runtime entities)
- Primary stats (MapleStory mapping): `STR`, `DEX`, `INT`, `LUK`.
- Resource stats: `MaxHP`, `MaxMP`.
- Derived/combat stats (computed from primaries; see §5.2): `Attack`, `MagicAttack`, `Defense`, `Speed/Initiative`, `Accuracy`, `Evasion`, `CritChance`, `CritDamage`.
- Per-element resistance/weakness comes from the entity's `ElementProfile` (see §4.11), not from StatBlock.

**`CharacterDefinition`** (ScriptableObject, one per hero class) — data only
- Identity: `className` ("Warrior"), `classFlavor` (MapleStory job text), `portrait`, `modelPrefab`, primary stat.
- `baseStats : StatBlock`.
- `abilities : List<Ability>` (the class's 4+ skills).
- `stance` data if the class has a stance/sub-class toggle (§5.9).
- `elementProfile` (most heroes neutral on defense; some have affinities).

**`BossDefinition`** (ScriptableObject, one per boss) — data only
- Identity, `modelPrefab`, intro/portrait, large `baseStats`.
- `abilities : List<Ability>` (the boss's move set).
- `aiBehavior : AIBehavior` (the Strategy asset).
- `phases : List<BossPhase>` (HP thresholds → behavior changes; see §7.1).
- `elementProfile` (weaknesses/resistances — the puzzle of the fight).
- `staggerThreshold`, stagger build rules.

**`Entity`** (MonoBehaviour, runtime combatant) — composition of:
- A runtime copy of `StatBlock` (current values, modifiable by buffs/debuffs; **never mutate the SO**).
- `currentHP`, `currentMP`.
- `List<Ability>` (from its definition).
- `StatusEffectContainer` (active statuses + flags; §4.10).
- Optional `AIBehavior` brain (null ⇒ player-controlled).
- A reference to its `AnimationDriver` and `VFXSpawner` (presentation), driven via events.
- Methods: `TakeDamage(DamageInfo)`, `Heal(amount)`, `SpendMP(amount)`, `ApplyStatus(...)`, `TickStartOfTurn()`, `IsAlive`, `CanAct`.
- **Critical:** entities operate on a *runtime copy* of their definition's data. Editing the ScriptableObject at runtime would corrupt shared content. Copy on spawn.

## 4.6 The battle engine: finite state machine
`BattleManager` is the orchestrator FSM. States (each a small class or enum-driven handler; prefer a clean state-object FSM):

1. **`BattleSetup`** — instantiate party (3 chosen heroes) and the boss from definitions; place on stage; initialize HUD; play boss intro (Ink, §13); raise `OnBattleStarted`.
2. **`RoundStart`** — compute initiative order for this round (TurnSystem, §4.7); tick start-of-round effects.
3. **`TurnStart`** — for the active combatant: tick start-of-turn statuses (DoT, regen, stun checks), raise `OnTurnStarted(entity)`. If the entity is stunned/frozen/staggered and cannot act, skip to `TurnEnd`.
4. **`AwaitInput`** *(player turn only)* — enable the ActionMenu; wait for the player to choose Ability + target. Validate (MP, cooldown, valid target). Build the Command.
5. **`EnemyDecision`** *(AI turn only)* — call `entity.brain.DecideAction(context)` → returns an `Ability` + target; build the Command. (Strategy pattern, §4.14.)
6. **`ResolveAction`** — execute the queued Command as a coroutine: animation → VFX → DamagePipeline/StatusEffectSystem → events → floating text → hit-stop/shake. Update Stagger. Handle reaction triggers.
7. **`CheckDeaths`** — remove dead entities (raise `OnEntityDied`); if a hero dies, update party; if the boss dies → `Victory`; if all heroes dead → `Defeat`.
8. **`TurnEnd`** — tick end-of-turn effects, decrement cooldowns/durations, raise `OnTurnEnded`. If more combatants remain in the round order → next `TurnStart`; else → `RoundEnd`.
9. **`RoundEnd`** — evaluate boss phase transitions (§7.1), decrement round-scoped effects → `RoundStart`.
10. **`Victory`** — raise `OnBattleWon`; show victory screen; if more bosses remain in the run, advance; else run complete.
11. **`Defeat`** — raise `OnBattleLost`; show game-over screen with retry/menu/quit.

Implement the FSM cleanly: a `BattleState` base with `Enter/Tick/Exit`, a `ChangeState()` method, and the manager holding `currentState`. Keep transition logic in one place. **Name the states in code exactly as above** so the architecture is self-documenting.

## 4.7 Turn / initiative system
- **Speed-ordered rounds** (clean, readable, classic JRPG). Each round, sort living combatants by `Speed` (descending; tie-break by a small random or LUK). Optionally support a future ATB upgrade, but ship speed-ordered first.
- **Action-economy hooks** (the depth, §5.5): exploiting a weakness or landing a crit can grant an **extra action** (a "1 More"), inserted into the round order. Staggering the boss can grant the party a free round. Implement as inserting an extra turn token into the round queue, with a clear cap (e.g. max 1 extra per entity per round) to prevent infinite loops.
- The TurnSystem raises `OnRoundStarted`, `OnTurnStarted(entity)`, `OnTurnEnded(entity)` for the HUD's turn-order display.

## 4.8 Damage pipeline (single source of truth for damage)
All damage flows through one ordered pipeline so it's consistent, testable, and tunable. `DamageInfo` carries source, target, ability, base power, element, flags. Order of operations (see §5.3 for formulas):
1. **Hit check** — accuracy vs evasion (some abilities are auto-hit; Dark Sight grants untargetable; Archer high accuracy). Miss ⇒ raise `OnAttackMissed`, floating "Miss", stop.
2. **Base damage** — from ability power and the caster's relevant offense stat (Attack for physical, MagicAttack for magic). MapleStory-style: `range × skill%`.
3. **Element modifier** — apply the **elemental matrix** multiplier (×weak / ×resist / ×immune) from target's `ElementProfile` (§4.11).
4. **Defense mitigation** — reduce by target defense (percentage-based reduction recommended for readable scaling; §5.3).
5. **Stagger modifier** — if target is **Staggered/Broken**, apply the stagger damage multiplier (big; §4.9).
6. **Crit** — roll crit; on crit multiply by CritDamage and flag for juicier feedback.
7. **Status modifiers** — vulnerabilities (e.g. *Oiled* ⇒ +fire damage; *Wet* ⇒ +lightning, ×stun chance), shields (Magic Guard absorbs to MP), defend stance (halves), buffs/debuffs.
8. **Clamp & apply** — never below 0; apply to `currentHP`; raise `OnDamageDealt(DamageInfo)` (presentation shows number, color-coded by weakness/crit; triggers shake/hit-stop scaled to magnitude).
9. **Stagger build** — add to target's stagger meter based on hit (weakness hits add more; §4.9).
10. **Reaction triggers** — e.g. counter-stance, on-hit status procs.

Keep the pipeline a pure-ish function (`ComputeDamage(DamageInfo) → DamageResult`) plus an `ApplyDamage` step, so the compute half is unit-testable headlessly.

## 4.9 Stagger / Break system (signature mechanic)
The standout depth mechanic, inspired by Octopath/FFXIII.
- Each boss has a **Stagger meter** (`staggerThreshold`). Landing hits — **especially weakness hits and specific "break" abilities** — adds to it. Different hits add different amounts (e.g. weakness hit +20, normal +8, dedicated break skill +30).
- When the meter fills, the boss is **Staggered (Broken)**: for the next full round it **loses its turn** and **takes a large damage multiplier** (e.g. ×1.75–×2.0). This is the party's burst window — the puzzle is to *set up* the break and then *unload* during it.
- After the stagger window, the meter resets (optionally with a higher threshold next time, to keep difficulty up).
- Telegraph it: a stagger bar under the boss's HP, color shifts as it fills, a screen-wide "BREAK!" flash + slow-mo on trigger (§12). Raise `OnStaggerBuilt(amount)`, `OnStaggerBroken`, `OnStaggerRecovered`.
- The Dragon's **charge turn** (§7.2) is the prime break target: breaking the Dragon mid-charge cancels its big attack — a readable, satisfying skill expression.

## 4.10 Status effect system
- `StatusEffectDefinition` (ScriptableObject): name, icon, type (Buff/Debuff/DoT/Flag/Control), duration (turns), stack rules, per-turn effect (e.g. burn damage), stat modifiers, and **flags** it sets (`isWet`, `isOiled`, `isBleeding`, `isStunned`, `isFrozen`, `isDefending`, `isStealthed`, `isMarked`).
- `StatusEffectContainer` (on each Entity, runtime): holds active effects, applies stat modifiers, ticks on turn start/end, manages durations and stacks, exposes flag queries (`Has(StatusFlag.Wet)`).
- **Control effects:** `Stun`/`Freeze` ⇒ skip turn; `Slow` ⇒ initiative penalty; `Silence` ⇒ no skills (basic only).
- **DoT:** `Burn`/`Poison`/`Bleed` ⇒ damage at turn start, color-coded floating numbers.
- **The synergy layer is what makes this a counted mechanic** (§4.12 / §5.8): flags set by one class are *consumed/amplified* by another.

## 4.11 Elements & the elemental matrix
- `ElementType` (enum or SO): `Physical`, `Fire`, `Ice`, `Lightning`, `Holy`, `Dark`, `Neutral`.
- `ElementMatrix` (ScriptableObject or a small data table): maps (attackElement → defenderProfile) to a multiplier. Default ×1.0; weakness ×1.5; resist ×0.5; immune ×0; absorb (optional) heals.
- Each Entity has an `ElementProfile`: which elements it's weak/resistant/immune to. **This is the core puzzle of every boss** — the player discovers and exploits it (and the UI hints it after the first time an element lands as "Weak!").
- See §5.4 for the concrete matrix table and §7.2 for the Dragon's profile (weak to **Ice**, resists/absorbs **Fire**).

## 4.12 Cross-class synergy resolver (combo system)
- A small resolver consulted in the DamagePipeline (step 7) and on status application:
  - *Oiled* + Fire hit ⇒ bonus fire damage + spreads burn.
  - *Wet* + Lightning hit ⇒ bonus lightning damage + high stun/paralyze chance.
  - *Wet* + Ice hit ⇒ apply *Frozen* (skip turn) more reliably.
  - *Marked* (Archer) + any hit ⇒ +crit chance / +stagger build.
  - *Bleeding* + physical hit ⇒ bonus damage.
- These are **data-described** where possible (a synergy table of `flag + element → effect`), so designers add combos without code. This system, combined with §4.10, is one of our five counted mechanics: it drives *who acts in what order* (Thief sets up, Mage/Archer pays off).

## 4.13 Event channels (the decoupling layer) — create these SO channels
Implement Unity's ScriptableObject event-channel pattern (a `GameEvent`-style SO + listener, or typed channels carrying a payload). Minimum channel set:
- Flow: `OnBattleStarted`, `OnRoundStarted`, `OnTurnStarted(Entity)`, `OnTurnEnded(Entity)`, `OnBattleWon`, `OnBattleLost`.
- Actions/results: `OnActionExecuted(Command)`, `OnDamageDealt(DamageResult)`, `OnAttackMissed(Entity)`, `OnHealed(Entity,int)`, `OnMPSpent(Entity,int)`.
- Status/stagger: `OnStatusApplied(Entity,StatusEffect)`, `OnStatusRemoved(...)`, `OnStaggerBuilt(Entity,float)`, `OnStaggerBroken(Entity)`, `OnStaggerRecovered(Entity)`.
- Boss: `OnBossPhaseChanged(BossPhase)`, `OnBossTelegraph(Ability)` (for "the Dragon is charging!").
- Entity: `OnEntityDied(Entity)`, `OnEntitySpawned(Entity)`.
- Meta: `OnRunAdvanced`, `OnSceneTransitionRequested(sceneName)`.
All presentation (HUD, audio, juice, animation, narrative) subscribes to these. Core raises them and never reaches into presentation. This is also great for debugging (you can log the whole battle from the channels) and for the demo video (clear, narratable data flow).

## 4.14 Enemy AI: Strategy pattern + utility scoring
- `AIBehavior` (abstract ScriptableObject): `Ability DecideAction(BattleContext ctx, Entity self, IReadOnlyList<Entity> heroes, out Entity target)`.
- Concrete strategies (each its own asset, assigned in `BossDefinition`):
  - **`DragonCycleAI`** *(build first)* — a **deterministic, telegraphed cycle** with light variance: `BasicSwipe → TailGuard (defense buff) → ChargingBreath (telegraph next turn) → FlameBreath (big AoE)` then repeat. Cycle position is state; it telegraphs `FlameBreath` a turn ahead (raises `OnBossTelegraph`), and breaking stagger during the charge cancels it. Phase 2 (low HP) shortens the cycle / adds enrage.
  - **`AggressiveAI`** (Evil Warrior) — utility scoring biased to **focus the lowest-HP hero** (~80%), occasional self-buff, executes when a hero is low.
  - **`ChaoticAI`** (Black Mage) — high randomness; random target weighting; favors applying debuffs and evasion/accuracy disruption; occasional powerful nuke; punishes a static party.
- **Why this counts for the AI bonus:** real decision-making logic (utility scoring + stateful cycles + targeting heuristics), cleanly pluggable. Optionally also implement one boss's brain as a **Unity Behavior Graph** to explicitly tick the "Unity AI systems" box, but the Strategy implementation is the primary, portable one.
- Keep AI **readable and explainable**: the decision method should be commentable into plain English ("if a hero is below 30% HP, the Evil Warrior executes them; otherwise it buffs then attacks the highest-threat target").

## 4.15 Save / persistence (minimal)
- **RunState** (a small persistent object or SO): which bosses are cleared, current party choice, and unlock flags. Lightweight — this is a boss-rush, not a save-heavy RPG.
- **Settings** (volume, resolution, etc.): persist with `PlayerPrefs` bound to the AudioMixer (§11). Keep it simple and robust.
- No mid-battle save required. A "continue run" from the menu is a nice-to-have, not mandatory.

## 4.16 Scene management & transitions
- Scenes: `Boot` (optional bootstrap) → `MainMenu` → `BattleArena`. (Per §2.2 we must have ≥2 real scenes; we have 2–3.)
- A `SceneLoader` service performs **async loads with a fade** (CanvasGroup alpha tween or a simple fade image) so transitions are smooth (a rubric item). Raise `OnSceneTransitionRequested`.
- **Guard persistent managers** across loads (§4.4). After a battle, returning to the menu must not duplicate audio/managers or leak the previous battle's entities. Tear down battle-scoped objects on scene exit.
- All scenes added to **Build Settings** (`MainMenu` first if no `Boot`, else `Boot` first).

---

# 5. COMBAT DESIGN SPECIFICATION (the rules of the game)

This section is the concrete, tunable ruleset. All numbers are **starting values** to put on ScriptableObjects and then balance by play-testing; they are not sacred. Keep them in the Inspector, not in code.

## 5.1 Core combat loop (player's mental model)
1. Read the boss: what's its weakness, what is it about to do (telegraph), how full is its Stagger bar?
2. Decide the party's three actions this round: set up (Thief flags, Archer mark, Mage debuff), pay off (hit weakness for big damage + stagger build + extra turn), and survive (heal, defend, Puppet decoy before a big boss hit).
3. Spend resources wisely (MP, cooldowns) — you can't nuke every turn.
4. Build the Stagger; when the boss Breaks, **unload** during the multiplier window.
5. Repeat through the boss's phases until it dies — or you do.

The fight should last **~4–6 minutes** and feel like solving a puzzle under pressure. A full 3-boss run is **~12–18 minutes** (within the 5–15 per-session expectation; the run can be played boss-by-boss).

## 5.2 Stats & derivation (MapleStory-flavored)
Primary stats: **STR, DEX, INT, LUK**. Each class keys off one (§1.3). Derived stats (starting formulas — tune freely):
- `Attack` (physical) = `baseWeaponPower + STR×k1` (Warrior) or `+ DEX×k1` (Archer) or `+ LUK×k1` (Thief). (Mirrors MapleStory: warriors scale STR, archers DEX, thieves LUK.)
- `MagicAttack` = `baseWandPower + INT×k2` (Mage).
- `MaxHP` ∝ class HP growth (Warrior highest, Mage lowest). `MaxMP` ∝ INT for casters.
- `Defense` = `baseDef + STR×k3 (small)`; Warrior has the most.
- `Speed/Initiative` = `baseSpeed + DEX×k4`; Thief/Archer faster, Warrior slower.
- `Accuracy` = `baseAcc + DEX×k5`; Archer highest. `Evasion` = `baseEva + LUK×k6 + DEX×k7`; Thief highest.
- `CritChance` = `baseCrit + LUK×k8 (Thief) / DEX×k8 (Archer)`; `CritDamage` = `1.5×` default.

Keep `k*` constants on a single `BalanceConfig` ScriptableObject so global tuning is one place.

## 5.3 Damage & defense formulas (starting point)
Use **percentage-based mitigation** for readable scaling (recommended by combat designers over flat subtraction):
```
rawPhysical = Attack × (abilityPower%) × randomRange(0.95,1.05)
rawMagic    = MagicAttack × (abilityPower%) × randomRange(0.95,1.05)
afterElement = raw × elementMultiplier        // 1.5 weak / 0.5 resist / 0 immune
mitigation   = Defense / (Defense + K)        // K ≈ 100; soft, diminishing
afterDef     = afterElement × (1 - mitigation)
afterStagger = afterDef × (target.isStaggered ? staggerMult : 1)   // ≈1.75–2.0
afterCrit    = afterStagger × (crit ? CritDamage : 1)
afterStatus  = afterCrit × statusVulnMult × (defending ? 0.5 : 1)
final        = max(0, round(afterStatus))
```
Healing: `heal = MagicAttack × healPower% × randomRange(0.97,1.03)`, clamped to MaxHP. Cleric heals scale on INT.

**Feel target for numbers:** a solid weakness hit should remove a *visible chunk* (~12–20% of boss HP), not chip 2%. A staggered burst should feel explosive. Tune boss HP so the fight is ~10–16 player actions long.

## 5.4 Elemental matrix (starting table)
Rows = attack element, columns = how a defender *profiled* to that element reacts. Each entity's `ElementProfile` lists its weak/resist/immune elements; default reaction is Neutral ×1.0.
| Reaction | Multiplier | UI feedback |
|---|---|---|
| **Weak** | ×1.5 | Big number, "WEAK!" pop, extra stagger build, may grant extra turn |
| **Neutral** | ×1.0 | Normal number |
| **Resist** | ×0.5 | Small number, "Resist" |
| **Immune** | ×0.0 | "Immune", 0 |
| **Absorb** *(optional, boss flavor)* | heals target | "Absorbed" — punishes wrong element |

Element list: `Physical, Fire, Ice, Lightning, Holy, Dark`. (Physical can also have weak/resist profiles for armor flavor.) The **Dragon** absorbs Fire and is weak to Ice (§7.2) — the central lesson of the first fight: *don't burn the fire dragon*.

## 5.5 Action economy & reward-for-skill (the depth engine)
The system that makes smart play feel great. Implement at least the first two:
1. **Weakness ⇒ extra build + bigger number** (always on).
2. **"One More" extra turn** on weakness-hit or crit (Persona-style), capped (max 1 extra per entity per round) to avoid loops. Visually flag the extra turn.
3. **Stagger ⇒ free pressure window**: while Broken, the boss skips its turn and takes the stagger multiplier; the party effectively gets a free round of burst.
4. **Overkill/efficiency feedback**: small score/grade bonus for breaking quickly or winning with no deaths (feeds the progression/scoring requirement, §2.2).

## 5.6 Resource systems
- **HP** — death at 0. Heroes can fall; the boss kill ends the fight. A wiped party = Defeat.
- **MP** — gates specials. Basic attacks cost 0 and **regen a little MP** (so there's always an action and a tempo decision: poke to build MP, or spend on a nuke). Specials cost meaningful MP. Mage has the largest pool; Magic Guard converts MP to a shield.
- **Cooldowns** — a few powerful skills (ultimates, big heals) have a turn cooldown so they're not spammed.
- **Stance/sub-resource** — Warrior's Berserk stance, Thief's Dark Sight charge, etc. (§5.9).

## 5.7 Hit / crit / evade
- Auto-hit for most basic and AoE abilities (keeps turn-based fights from feeling random); precision matters via **crit** and **status**, not whiff-fests.
- Single-target precise skills use Accuracy vs Evasion. **Thief** has high evasion (and Dark Sight = guaranteed dodge for a turn but can't act); **Archer** has high accuracy (rarely misses, ignores some evasion). The **Black Mage** debuffs party accuracy — a reason to bring the Archer or cleanse.
- Crit: rolled per hit; crit ⇒ ×CritDamage + juicier feedback (bigger shake, flash, distinct SFX).

## 5.8 Status effects catalogue (starting set)
Implement as `StatusEffectDefinition` assets. (Buff = good, Debuff = bad, DoT = damage over time, Flag = enables synergy, Control = denies actions.)
| Status | Type | Effect (starting values) | Set by | Synergy |
|---|---|---|---|---|
| **Burn** | DoT | 5% max HP/turn for 3 turns | Mage Fire | spreads if target *Oiled* |
| **Poison** | DoT | flat 30/turn for 4 turns, stacks | Mage Poison / Thief | — |
| **Bleed** | DoT | 4% current HP/turn, 3 turns | Thief/Warrior physical | physical hits deal bonus vs *Bleeding* |
| **Oiled** | Flag | +50% fire damage taken; enables burn spread | Thief (Oil Bomb) | + Fire ⇒ big burn |
| **Wet** | Flag | +50% lightning dmg; +ice freeze chance | Thief/Archer (Water) | + Lightning ⇒ stun; + Ice ⇒ Frozen |
| **Marked** | Flag | +crit chance & +stagger build vs target | Archer | any hit on *Marked* crits more |
| **Frozen** | Control | skip next turn; shatter bonus on hit | Mage Ice (esp. on *Wet*) | physical on *Frozen* ⇒ shatter dmg |
| **Stun/Paralyze** | Control | skip next turn | Lightning on *Wet*, some skills | — |
| **Defending** | Buff | incoming dmg ×0.5 this round | Defend action / Warrior | — |
| **Stealth (Dark Sight)** | Buff | untargetable 1 turn, cannot act | Thief | escape a telegraphed nuke |
| **Rage (Atk buff)** | Buff | +Attack to party, 3 turns | Warrior | stacks the burst window |
| **Bless (Acc/Def buff)** | Buff | +Accuracy/+Defense party, 3 turns | Mage Cleric | counters Black Mage debuffs |
| **Haste (Spd buff)** | Buff | +Initiative party, 3 turns | Thief | manipulate turn order before a break |
| **Weaken (Def debuff)** | Debuff | -Defense on boss, 3 turns | Thief/Warrior | amplifies the stagger burst |
| **Blind (Acc debuff)** | Debuff | -Accuracy on boss | Thief/Archer | survive Evil Warrior |

## 5.9 Stance / class-switching (a counted mechanic)
Each class has a meaningful in-combat toggle that changes its kit — adding decisions without new art:
- **Warrior — Berserk Stance**: toggle. Berserk = +Attack, -Defense, gains execute/lifesteal options; Guardian = +Defense, taunt/cover (draw boss single-target onto Warrior), -Attack. Switching costs the turn or a small resource.
- **Mage — Element Attunement**: switch active element school (Fire ⇄ Ice ⇄ Lightning ⇄ Holy/Heal). Same buttons, different element/effect, so the Mage can answer any boss's weakness and pivot to healing. This *directly* interacts with the elemental matrix and is the clearest "decision affects game state" mechanic.
- **Thief — Dark Sight charge**: build/spend a stealth charge for a guaranteed dodge + bonus-damage ambush.
- **Archer — Aim Mode**: trade turns for a charged, defense-ignoring precision shot vs. rapid lower-damage shots; place/refresh **Puppet** decoy.

Stance state lives on the Entity, is shown in the HUD, and is read by the DamagePipeline/abilities. Switching is a real tactical choice (tempo cost vs payoff).

## 5.10 The five+ counted mechanics (mapping to §2.2)
For the rubric, these are the distinct, moderately-complex gameplay mechanics (each meaningfully affects decisions/state):
1. **Turn-based FSM combat with speed-ordered initiative & action-economy rewards** (§4.6–4.7, §5.5).
2. **Elemental matrix** weakness/resist/immune system driving target/element choice (§4.11, §5.4).
3. **MP resource economy + cooldowns** (poke-to-build vs spend-to-burst) (§5.6).
4. **Stagger / Break system** with a burst window and telegraph-cancel (§4.9).
5. **Cross-class status-flag synergy combos** (Oiled+Fire, Wet+Lightning/Ice, Marked, Frozen-shatter) (§4.10, §4.12, §5.8).
6. **Stance / element-attunement switching** that re-shapes a class's kit mid-fight (§5.9).
(Six listed; the rubric needs five distinct — we keep one in reserve and present all six as depth.)

---

# 6. CONTENT — THE FOUR CLASSES

Each class is a `CharacterDefinition` SO with a `StatBlock` and a list of `Ability` SOs. Below are the **starting kits** — class identity, stat bias, and 4–6 abilities each (basic + specials + a stance/utility). Ability numbers are starting values for the Inspector. **Every class must feel distinct to pilot** (design pillar #3). The player picks **3 of these 4** per run.

Ability fields to fill on each `Ability` asset: `displayName`, `description`, `icon`, `element`, `effectType` (Attack/MultiHit/Heal/Buff/Debuff/Status/Stance/Defend), `power%`, `mpCost`, `targetRule` (SingleEnemy/AllEnemies/SingleAlly/AllAllies/Self), `statusesToApply`, `cooldown`, `vfxPrefab`, `sfxId`, `animationTrigger`, `tags` (e.g. `BreakSkill`, `Ranged`, `Setup`, `Finisher`).

## 6.1 Warrior (STR) — the frontline anchor
**Identity:** highest HP and Defense, slow, reliable physical damage, party protection, a stance toggle. Wants to soak boss aggro and enable the burst. MapleStory roots: Power Strike, Slash Blast, Rage, Iron/Hyper Body.
**Stat bias:** STR high, HP highest, Defense highest, Speed low, Crit low.
**Abilities (starting):**
| Name | Element | Type | Power% / Effect | MP | CD | Notes |
|---|---|---|---|---|---|---|
| **Power Strike** | Physical | Attack | 130% single | 0 | – | Basic; regens small MP. Heavy reliable hit. |
| **Slash Blast** | Physical | MultiHit | 60%×2 single (or cleave) | 8 | – | Two hits → good stagger build; bonus vs *Bleeding*. Tag `BreakSkill`. |
| **Rage** | – | Buff | +Attack to all allies, 3 turns | 12 | 2 | Stacks the burst window. |
| **Guardian Taunt** | – | Buff/Control | Draw boss single-target to Warrior + self *Defending*, 1 turn | 10 | 2 | Protect a low hero before a telegraphed hit. |
| **Berserk Stance** | – | Stance | Toggle Berserk(+Atk,-Def, execute) / Guardian(+Def, taunt,-Atk) | 0 | – | Costs the turn to swap. Core decision (§5.9). |
| **Crushing Blow** (ult) | Physical | Attack | 220% single, +big stagger; bonus on *Weaken* | 25 | 4 | Finisher; ideal during Stagger window. |

## 6.2 Mage (INT) — elemental nuker & healer
**Identity:** lowest HP, highest magic damage, **element attunement** to answer any weakness, **and** the party's healer/buffer. The most flexible class; the answer to the Dragon's Ice weakness and the Black Mage's debuffs. MapleStory roots: F/P + I/L wizard + Cleric; Magic Guard.
**Stat bias:** INT high, MP highest, HP lowest, Defense low.
**Abilities (starting):**
| Name | Element | Type | Power% / Effect | MP | CD | Notes |
|---|---|---|---|---|---|---|
| **Magic Bolt** | matches attunement | Attack | 120% single, element = current attunement | 0 | – | Basic; regens MP; element follows stance. |
| **Element Attunement** | – | Stance | Switch school: Fire / Ice / Lightning / Holy(Heal) | 0 | – | The signature pivot (§5.9). Changes Magic Bolt + unlocks the matching nuke. |
| **Fireball / Ice Lance / Spark** | Fire/Ice/Lightning | Attack | 160% single (school-dependent) | 12 | – | Fire→*Burn*/ignites *Oiled*; Ice→chance *Frozen* (sure on *Wet*); Lightning→*Stun* on *Wet*. |
| **Heal** | Holy | Heal | restore ally HP (INT-scaled) | 14 | 1 | Cleric attunement. Keeps the glass party alive. |
| **Bless** | Holy | Buff | +Accuracy/+Defense party, 3 turns | 12 | 2 | Counters Black Mage accuracy debuffs. |
| **Magic Guard** | – | Buff | Convert incoming damage to MP loss (shield), 2 turns | 10 | 3 | Survive a nuke on the squishiest hero. |
| **Meteor / Blizzard** (ult) | Fire/Ice | Attack | 240% AoE/single, school-dependent | 30 | 4 | Massive; Blizzard on the Ice-weak Dragon = burst king. |

## 6.3 Thief (LUK) — the combo enabler
**Identity:** high crit/evasion, low direct damage, but the **setup engine** — applies the flags (*Oiled*, *Wet*, *Marked*, *Weaken*) that the Mage/Archer convert into huge payoffs, plus Dark Sight survival and Haste turn-order manipulation. MapleStory roots: Lucky Seven, Dark Sight, Haste, Steal.
**Stat bias:** LUK high, Evasion highest, Crit high, Speed high, HP low-mid.
**Abilities (starting):**
| Name | Element | Type | Power% / Effect | MP | CD | Notes |
|---|---|---|---|---|---|---|
| **Lucky Seven** | Physical | MultiHit | 50%×2, LUK-scaled, high crit | 0 | – | Basic; regens MP; good stagger build via 2 hits. |
| **Oil Bomb** | – | Status | Apply *Oiled* to boss | 8 | 1 | Setup → Mage Fire detonates for huge burn. |
| **Water Bomb** | – | Status | Apply *Wet* to boss | 8 | 1 | Setup → Lightning *Stun* / Ice *Frozen*. |
| **Shadow Mark** | – | Status | Apply *Marked* (crit + stagger build) | 6 | 1 | Force-multiplies the whole party's hits. |
| **Dark Sight** | – | Buff | Untargetable 1 turn, cannot act; next attack ambush bonus | 6 | 2 | Dodge a telegraphed boss nuke, then punish. |
| **Smoke Bomb** | – | Debuff | *Blind* boss (-Accuracy), 3 turns | 10 | 2 | Survive Evil Warrior / Black Mage. |
| **Assassinate** (ult) | Physical | Attack | 200% single, +bonus vs *Marked*/*Weaken*, guaranteed crit | 24 | 4 | Finisher during Stagger. |

## 6.4 Archer (DEX) — ranged precision
**Identity:** highest accuracy and crit-from-range, safe consistent damage, defense-piercing shots, and a **Puppet** decoy that tanks boss single-target hits. The reliable damage backbone; never misses, ignores some evasion (great vs Black Mage). MapleStory roots: Double Shot, Soul Arrow, Puppet, Eye of Amazon.
**Stat bias:** DEX high, Accuracy highest, Crit high, Speed mid-high, HP mid.
**Abilities (starting):**
| Name | Element | Type | Power% / Effect | MP | CD | Notes |
|---|---|---|---|---|---|---|
| **Double Shot** | Physical | MultiHit | 70%×2 ranged, can't miss | 0 | – | Basic; regens MP; reliable 2-hit stagger build. |
| **Soul Arrow** | Physical | Attack | 150% single, ignores % of Defense | 10 | – | Pierces tanky bosses; great in Stagger. |
| **Mark Target** | – | Status | Apply *Marked* + reveal weakness hint | 6 | 1 | Synergy + information. |
| **Puppet** | – | Buff/Summon | Spawn a decoy that draws boss single-target aggro, 2–3 turns | 12 | 3 | Survive telegraphed single-target nukes; protect the Mage. |
| **Eye of Amazon** | – | Buff | +Accuracy/+Range party, 3 turns | 8 | 2 | Team accuracy; counters Blind. |
| **Arrow Rain** (ult) | Physical | Attack | 210% AoE, +stagger; bonus vs *Marked* | 26 | 4 | Finisher; strong stagger build. |

## 6.5 Party composition & synergy matrix
The player picks **3 of 4**, so there are 4 possible trios. Each should be viable but play differently — no trap picks, no mandatory pick.
- **Warrior + Mage + Thief**: tanky setup-and-nuke. Thief oils/wets, Mage detonates, Warrior protects. Classic, forgiving.
- **Warrior + Mage + Archer**: durable + reliable ranged + flexible magic. Safest vs the Black Mage (accuracy).
- **Warrior + Thief + Archer**: physical/crit burst, no dedicated healer → must play defense/Dark Sight/Puppet well. High skill, high reward.
- **Mage + Thief + Archer**: glass-cannon combo engine; fastest kills, most fragile. The Mage is the only sustain.
**Design rule:** ensure each trio can clear the Dragon. If a trio lacks the Dragon's Ice answer (no Mage), give the Archer/Thief access to an Ice-flagged option or make physical/Stagger play sufficient — *every* party must have a path, so weakness-exploitation is rewarded but not mandatory. Validate this in play-testing.

---

# 7. CONTENT — THE BOSSES

## 7.1 Boss design philosophy (applies to all three)
- **Distinct AI and role** (combat designers' rule: specialize enemies). The three bosses test three different skills: timing/defense (Dragon), adaptability/cleanse (Black Mage), protection/tempo (Evil Warrior).
- **Telegraphed threats.** Big attacks are announced a turn ahead (`OnBossTelegraph`), giving the player a decision: defend, Dark Sight, Puppet, or race to Stagger and cancel it. Telegraphing is what makes the puzzle fair.
- **Phases.** Each boss has 2 phases gated by HP (`BossPhase` with `hpThreshold`, behavior changes, optional new abilities, an enrage). Phase change raises `OnBossPhaseChanged` (visual/audio shift).
- **A clear weakness (the puzzle).** Each boss's `ElementProfile` is the lesson of the fight; the UI hints it after the first reveal.
- **A Stagger identity.** Each boss has a prime stagger window (the Dragon's charge; the Black Mage's cast; the Evil Warrior's wind-up).

## 7.2 THE DRAGON — *first build target* (full spec)
The anchor boss. Build the entire game against this one first (§8). It is the clearest teacher of every system.

**Fantasy & arena:** an ancient flame dragon on a rocky platform; orthographic stage, lava-glow lighting, embers (VFX). Large, readable silhouette.

**Element profile (the central puzzle):**
- **Weak to Ice (×1.5)** — Ice Lance/Blizzard and *Frozen* shatter are the burst path.
- **Absorbs Fire (heals!)** — burning the fire dragon *heals it*. The lesson: read the boss, don't auto-nuke. Punishes the obvious Fire mage.
- Resists Physical slightly (×0.85) until Staggered; neutral to Lightning/Holy.
- *Wet* + Ice ⇒ reliable *Frozen*; this is the intended combo (Thief Water Bomb → Mage Ice/Archer).

**Stats (starting):** large HP (sized so the fight is ~10–16 player actions), high Attack, moderate Defense, low Speed (acts after fast heroes most rounds), `staggerThreshold` tuned so a focused party breaks it roughly every ~3–4 rounds.

**Move set (`Ability` assets on the BossDefinition):**
| Name | Element | Type | Effect | Telegraph |
|---|---|---|---|---|
| **Claw Swipe** | Physical | Attack | single-target moderate on a hero | no |
| **Tail Sweep** | Physical | Attack | AoE light on all heroes | no |
| **Tail Guard** | – | Buff | self +Defense 1–2 turns (a defensive beat in the cycle) | no |
| **Charging Breath** | – | Telegraph | winds up; raises `OnBossTelegraph`; "The Dragon inhales…" warning | **THIS turn** |
| **Flame Breath** | Fire | Attack | **big AoE** on all heroes next turn (can wipe an unprepared party) | fired turn after charge |
| **Enrage** (phase 2) | – | Buff | on reaching ~40% HP: +Attack, shorter cycle | phase change |

**AI — `DragonCycleAI` (deterministic cycle + light variance):**
Cycle: `Claw Swipe → Tail Guard → Charging Breath → Flame Breath → (repeat)`, with ~20% chance to substitute Tail Sweep for Claw Swipe for variety. Phase 2 (≤40% HP): drop Tail Guard, shorten to `Claw → Charging Breath → Flame Breath`, +Attack (enrage). **Telegraph contract:** when it uses Charging Breath, it commits to Flame Breath next turn *unless Staggered first*. The clean, learnable loop is exactly what makes the fight a fair puzzle.

**Stagger identity (the key skill expression):** the **charge turn** is the prime break window. If the party fills the Stagger meter while the Dragon is charging, it **Breaks**, **cancels Flame Breath**, loses its turn, and eats the burst multiplier. So the optimal line is: build Stagger (Wet→Ice, multi-hit skills, *Marked*), time the break for the charge, then unload (Blizzard / Assassinate / Arrow Rain / Crushing Blow). Defensive answer if you can't break in time: Warrior Guardian Taunt + Defend, Archer Puppet, Thief Dark Sight, or Mage Magic Guard on the squishiest hero.

**Narrative (Ink, §13):** a short intro where the Dragon speaks; a pre-fight choice (e.g. taunt it or study it) sets an Ink variable that the BattleManager reads — taunting makes it open with Charging Breath sooner (riskier/faster), studying reveals its Fire-absorb up front. This is the "narrative affects gameplay state" hook for the bonus.

**Win/lose:** win when Dragon HP = 0 (victory screen, run advances). Lose when all three heroes fall (game-over screen: retry / menu / quit).

## 7.3 The Black Mage — high-variance chaos caster (spec for after the Dragon)
MapleStory's iconic antagonist. Tests **adaptability and cleansing**.
- **Element profile:** weak to **Holy/Light** (the Mage's Cleric school and holy hits punish it); resists Dark; neutral elsewhere. Thematic: light beats the dark mage.
- **AI — `ChaoticAI`:** high randomness; randomly targets; favors **debuffs** (party-wide -Accuracy "Curse", -Defense, random *Silence* on a hero), occasional big Dark nuke, and self-evasion buffs. Punishes a static plan; rewards Bless/Eye of Amazon/Smoke-cleanse and the Archer's can't-miss shots.
- **Phases:** phase 2 adds "Reality Warp" — periodically shuffles turn order or swaps a buff/debuff, forcing re-planning.
- **Stagger identity:** breaks best when interrupted mid-cast; Staggering cancels its queued nuke.
- **Narrative:** taunts that reference the player's choices; an Ink variable can gate which phase-2 ability it favors.

## 7.4 The Evil Warrior — relentless aggressor (spec for after the Dragon)
A dark-knight mirror of the player's Warrior. Tests **protection and tempo**.
- **Element profile:** heavily armored — resists Physical (×0.6) until Staggered, weak to **Lightning** (×1.5; armor conducts). The lesson: shatter the armor (Stagger) or shock it.
- **AI — `AggressiveAI`:** utility scoring biased ~80% to the **lowest-HP hero** to pick off your party one by one; self-buffs (Rage-like), and **executes** heroes below a HP threshold. Rewards Warrior Guardian Taunt/cover, Archer Puppet, healing, and fast Stagger to strip its armor.
- **Phases:** phase 2 "Last Stand" — gains lifesteal and a multi-hit flurry; the DPS race tightens.
- **Stagger identity:** its big wind-up attack is the break window; breaking strips the physical resistance so the Warrior/Thief/Archer can finally hit hard.
- **Narrative:** a rival dynamic with the player's Warrior if present (Ink can reference party composition).

---

# 8. THE DRAGON-FIRST BUILD PLAN (milestones)

Build in this order. Each milestone ends **compiling, committed to `main`, and play-testable**. Do not start a milestone's art before its systems work with primitives. This is the operational roadmap; each bullet is a concrete deliverable Claude Code should produce.

## Milestone 0 — Skeleton & scaffolding
- Create the full folder structure (§3.5) and `.asmdef` files.
- Set up URP camera as **Orthographic**; create `MainMenu` and `BattleArena` scenes; add both to Build Settings.
- Implement the **SO event-channel** base (`GameEvent` + listener, typed variants) and create the channel assets from §4.13.
- Implement `BalanceConfig`, `ElementType`, `ElementMatrix` (with the §5.4 table), and the `StatBlock` type.
- Guarded `GameBootstrap` + `SceneLoader` (fade) + stub `AudioManager` + `RunState` (§4.4, §4.16).
- **Exit/Compile/Commit.** ✅ Project opens, both scenes load, fade transition works, no errors.

## Milestone 1 — Core combat vs the Dragon (primitives, one hero)
- Implement `Entity` (composition; runtime stat copy), `Ability` SO, the **Command** classes (§4.3), the **DamagePipeline** (§4.8), **TargetingSystem**, and the **BattleManager FSM** (§4.6) with the **TurnSystem** (§4.7).
- Implement the **StatusEffectSystem** (§4.10) and **StaggerSystem** (§4.9) and **synergy resolver** (§4.12) — at least the framework + the Wet/Ice/Oiled/Fire/Marked combos.
- Author the **Dragon** `BossDefinition` + `DragonCycleAI` (§7.2) and **one hero** (Mage) with a starter kit, as **capsule primitives**.
- Drive the whole fight headlessly-verifiable: log each turn, damage, element multiplier, stagger build/break, telegraph, win/lose. Add **edit-mode unit tests** for the damage formula and elemental matrix.
- **Exit/Compile/Commit.** ✅ A full Mage-vs-Dragon fight resolves start→finish in the Console, including a Stagger break cancelling Flame Breath.

## Milestone 2 — All four classes + party of three
- Author the **four `CharacterDefinition`s** and **all their abilities** (§6) as SO assets.
- Implement **party of 3**, hero turns, **stance/attunement switching** (§5.9), MP economy + cooldowns (§5.6), and the action-economy extra-turn rule (§5.5).
- Validate all **four trios** can clear the Dragon (§6.5).
- **Exit/Compile/Commit.** ✅ Choose any 3 heroes (via a temporary debug selector), fight the Dragon, all mechanics fire.

## Milestone 3 — UI, HUD, menus, scene flow
- Build the **Main Menu**, **Character Select** (pick 3 of 4), **Battle HUD** (HP/MP/Stagger bars, turn order, action menu, target picker, combat log, status icons, telegraph banner), **Pause Menu**, **Game Over / Victory**, **Settings**, **How to Play**, **Credits** (§9).
- Wire all UI to **event channels** only (no direct calls into BattleManager). Resolution-responsive (anchors, TMP).
- Replace the debug selector with the real Character Select flow; full loop Menu → Select → Fight → Win/Lose → restart/menu/quit.
- **Exit/Compile/Commit.** ✅ Complete playable loop with primitives and full UI.

## Milestone 4 — Assets, animation, materials, lighting
- Human pulls models (Mixamo) + dragon (Asset Store/Meshy) + VFX pack into the right folders (§10). Claude Code: import-configure rigs (Humanoid), build **Animator Controllers** (Idle/Hit/Action state machines), swap primitives → models keeping all components, wire **VFX prefab spawning** per ability (the "3 anims + VFX = many skills" technique), set up URP **materials**, **lighting** (APV + a post-processing **Volume**: Bloom, Color Adjustments, Vignette), and Cinemachine framing.
- **Exit/Compile/Commit.** ✅ The Dragon fight looks like a game, not capsules.

## Milestone 5 — Audio + juice + narrative + cheats
- **Audio** (§11): `AudioMixer` (Music/SFX groups), menu+battle music, full SFX set, settings sliders bound to the mixer.
- **Juice** (§12): hit-stop, Cinemachine Impulse shake, screen flash, **floating damage text** (coroutine/DOTween punch), post-FX pulses (chromatic aberration/zoom on crit & Break), "BREAK!" slow-mo, UI tweens.
- **Narrative** (§13): Ink package, Dragon intro `.ink` with a choice bound to combat state; `NarrativeRunner` + external-function bindings.
- **Cheat Manager** (§14.1): dev-only panel (invincibility, refill MP, skip-to-boss, instant-win, force-Stagger), wrapped in `#if UNITY_EDITOR || DEVELOPMENT_BUILD`.
- **Exit/Compile/Commit.** ✅ The Dragon fight is a *complete, polished vertical slice*.

## Milestone 6 — Expand to the other two bosses (content, not engineering)
- Author **Black Mage** + `ChaoticAI` (§7.3) and **Evil Warrior** + `AggressiveAI` (§7.4) as SO assets + models/VFX/Ink intros. Reuse every system from the Dragon slice.
- Implement the **run flow**: Dragon → Black Mage → Evil Warrior (boss-rush progression + RunState), with the ability to play any single boss (for the 5–15-min session and easy grading/demo).
- **Exit/Compile/Commit.** ✅ Full three-boss game.

## Milestone 7 — Stabilize, document, submit
- Run the **sanity check** (§16); fix all issues; make a **Windows build** and test start→finish + Exit-quits.
- **README** from template + **screenshots** (§17); **Scrum backlog → Trello** (§15); record + upload **demo video** (§17).
- Final commit/push to `main`; submit repo link on Moodle (formality). ✅ **Shipped.**

> If time is short, a fully polished **Dragon-only** build (Milestones 0–5) is already a complete, high-scoring submission. The other two bosses are additive. Never ship three broken bosses over one excellent one.

---

# 9. UI / UX SPECIFICATION

UI is presentation: it **subscribes to event channels** and reads runtime state; it never drives combat logic. All text = TMP. Build resolution-responsive (Canvas Scaler "Scale With Screen Size", proper anchors). Provide hover/click feedback on every button (a rubric item).

## 9.1 Scene & screen flow
```
MainMenu ──Play──▶ Character Select ──Confirm──▶ BattleArena
   │  How to Play / Settings / Credits / Quit          │
   └───────────────────────────◀── Exit to Menu ───────┤
                                                  Pause / Game Over / Victory
```

## 9.2 Main Menu (scene)
Buttons: **Play** (→ Character Select), **How to Play** (controls + mechanics primer panel), **Settings** (audio sliders, resolution, fullscreen), **Credits** (team + asset/AI-tool attributions), **Quit** (quits build; in Editor stops play mode). Animated title, hover SFX, music. A "Continue Run" entry if a run is in progress (RunState).

## 9.3 Character Select
- Show the four classes with portrait, name, MapleStory-flavored role blurb, primary stat, and a 1-line kit summary. Player selects **exactly 3**; show synergy hints; **Confirm** loads `BattleArena` with the chosen trio (and, in the run, the current boss).
- For grading/demo convenience, allow choosing which boss to face (or default to the run order starting at the Dragon).

## 9.4 Battle HUD (the most important screen)
Must support gameplay clarity (not decoration). Elements:
- **Boss panel** (top): name, large **HP bar**, **Stagger bar** (color-shifts as it fills; flashes on Break), current **status icons**, and a **telegraph banner** ("⚠ The Dragon is charging Flame Breath!") when `OnBossTelegraph` fires.
- **Party panel** (bottom): for each of 3 heroes — portrait, **HP** and **MP** bars, status icons, current **stance/attunement** indicator, and a highlight for whose turn it is.
- **Turn-order tracker**: upcoming initiative order (so the player can plan around the boss's next turn / a Break).
- **Action menu** (active hero's turn): the hero's abilities as buttons with **icon, name, MP cost, cooldown state**, disabled if unaffordable/on cooldown, with tooltips (element, effect, target rule). A **Defend** and **stance/attunement** control. Clear **target picker** (click an enemy/ally; valid targets highlighted).
- **Combat log** (small, scrollable): plain-English lines from `OnActionExecuted`/`OnDamageDealt` (great for clarity *and* it showcases the Command pattern's uniform logging).
- **Weakness hints**: once an element lands as "Weak!", annotate the boss panel so the player learns the puzzle.

## 9.5 Pause Menu
Triggered by `Esc`/Start. Proper pause logic (`Time.timeScale = 0`, input gated). Options: **Resume**, **Restart** (reload the fight), **Settings**, **Exit to Main Menu**. Must not duplicate managers on exit (§4.4).

## 9.6 Game Over / Victory
- **Victory**: "The Dragon is slain!" + a simple **grade/score** (based on turns, deaths, time — feeds the progression/scoring requirement). Options: **Continue** (next boss / run complete), **Main Menu**, **Quit**.
- **Defeat**: clear lose indication + **Retry**, **Main Menu**, **Quit**. May be a screen or integrated into the menu, with clear UX (rubric allows either).

## 9.7 Input scheme (new Input System, document in README + How to Play)
Turn-based, so input is light and mostly UI:
- Mouse: click buttons/abilities/targets (primary). Hover for tooltips.
- Keyboard: number keys 1–4 select abilities; arrow/Tab to cycle targets; Enter confirm; `Esc` pause; optional gamepad navigation. 
- Cheat panel hotkey (dev builds only), e.g. `` ` `` (backquote) (§14.1).
Define one `InputActionAsset` with `UI` and `Battle` action maps; the project already has an `InputSystem_Actions` asset to build on.

## 9.8 Feedback & accessibility
- Every action has **visual + audio** feedback (§12, §11). Damage numbers color-coded (white normal, orange crit, cyan weakness, grey resist, green heal).
- Colorblind-friendly: don't rely on color alone — pair with icons/labels ("WEAK", "RESIST").
- Readable TMP at common resolutions; UI never overlaps the stage's key info.
- A "How to Play" screen documents controls and the core mechanics (so the player never guesses).

---

# 10. ART & ASSET SPECIFICATION

The strategy: **off-the-shelf assets, first-party systems.** We hit ≥10 distinct assets and ≥2 animated characters cheaply, and lean on juice (§12) and lighting (§10.6) to make it look intentional.

## 10.1 The 2.5D orthographic approach
Real 3D models on a shallow stage, **orthographic camera** facing the combatants — heroes on the left/front, boss on the right/back, MapleStory-style side framing. This hides topology/quality issues, reads clearly, and lets us use free rigged models without a polished 3D scene.

## 10.2 Art direction / visual language
- **Cohesion via lighting and post, not asset uniformity.** Pick one mood (e.g. dramatic rim-lit fantasy, warm key + cool fill, lava glow for the Dragon arena) and apply it consistently through a URP **Volume** so mismatched models still feel like one game.
- **Readable silhouettes.** Keep VFX from fuzzing character outlines (a Unity 6 polish guideline). Big, clear shapes.
- **Consistent scale & ground.** All combatants share a ground plane and consistent sizing; the Dragon is dramatically larger.
- One **master shader look** (URP/Lit or a simple Shader Graph) across models for consistent lighting response.

## 10.3 Asset pipeline (what the human pulls; what Claude Code does)
**Human (batched, one session):**
- **Heroes (Mixamo, free, rigged):** Warrior, Mage, Thief, Archer humanoid models. For each, download **Idle**, **Hit/React**, and one **Action** (attack/cast) animation. Export **FBX for Unity** into `Assets/_Project/Art/Models/`.
- **Dragon:** a free Asset Store dragon **or** a Meshy text-to-3D dragon (auto-rigged) → `Models/`. (Meshy is best for the non-humanoid the heroes' Mixamo can't cover.)
- **VFX:** a free particle pack (e.g. "Cartoon FX"/"Free RPG VFX") with **fire, ice, lightning, heal, generic impact** prefabs → `Assets/_Project/Art/VFX/`.
- **UI:** a free UI kit / icon set for ability icons and panels (or generate placeholder icons) → `Art/UI/`.
**Claude Code (everything else):**
- Configure model import (rig → Humanoid, scale, materials), build **Animator Controllers** + state machines, set up prefabs, wire components, swap primitives → models, assign **VFX prefabs** to abilities, author materials, set up lighting + post, Cinemachine framing.

## 10.4 Required distinct assets (≥10 — we exceed easily)
Count toward the requirement: 4 hero models, 1 dragon model (+2 more boss models later), fire/ice/lightning/heal/impact VFX (5), menu music, battle music, and SFX set (sword, fireball, ice, lightning, heal, UI click, victory, defeat). That's well past 10 distinct assets even for the Dragon slice. Track them for the README asset list (§17) **with attributions/licenses**.

## 10.5 Animation (proper Animator Controllers — rubric requirement)
- Each combatant has an **Animator Controller** with a small **state machine**: `Idle ⇄ Action`, `Idle ⇄ Hit`, return to `Idle`. Transitions via parameters/triggers (`Attack`, `Hit`, `Cast`, `Die`), **not** loose `Animation.Play` calls.
- The **AnimationDriver** (presentation) listens to events (`OnActionExecuted`, `OnDamageDealt`, `OnEntityDied`) and sets Animator triggers — keeping logic and animation decoupled.
- **The "few animations, many skills" technique:** every hero needs only Idle/Hit/Action. Skill *identity* comes from the **VFX prefab + SFX + number color** spawned at the action's hit moment, not from unique per-skill animations. The DamagePipeline timing drives when VFX/floating-text appear. This is how 4 abilities × 4 heroes read as distinct without 16 animations.
- ≥2 animated characters is trivially satisfied (all combatants animate); the Dragon also animates (Idle/Attack/Hit).

## 10.6 Materials, shaders, lighting (URP)
- **Materials:** URP/Lit for models; a simple Shader Graph if a stylized look is wanted (e.g. rim light, fresnel). No Built-in shaders.
- **Lighting:** a key directional light + fill; **Adaptive Probe Volumes (APV)** for nice ambient on the models (Unity 6). Arena mood lighting (lava glow for the Dragon).
- **Post-processing Volume** (global): **Bloom** (for VFX/embers), **Color Adjustments** (mood/contrast), **Vignette** (focus), optional **Chromatic Aberration**/**Lens Distortion** pulsed on impacts (§12). Keep it tasteful; readability first.
- Keep overdraw/perf reasonable (no runaway particles; pool VFX — §16 perf budget).

---

# 11. AUDIO SPECIFICATION

Audio must use a **reasonable structure (AudioMixer)** — not hardcoded volumes/disconnected clips (rubric requirement).

## 11.1 AudioMixer structure
- One `AudioMixer` with groups: **Master → {Music, SFX, UI}**. Expose `MusicVolume`, `SFXVolume`, `MasterVolume` parameters.
- **AudioManager** (persistent, guarded singleton via bootstrap §4.4): plays music (cross-fade on scene/phase change), and exposes `PlaySFX(id)` that routes through the SFX group. Subscribes to event channels so SFX fire automatically (`OnDamageDealt` → hit SFX, `OnHealed` → heal chime, etc.).
- Settings sliders write to mixer parameters via `SetFloat` (logarithmic mapping) and persist with `PlayerPrefs`.

## 11.2 Music
- **Menu theme** (calm/heroic loop). **Battle theme** (driving, orchestral/rock; AIVA or Suno/Riffusion, royalty-free). Optional **phase-2 intensify** or a **Stagger stinger**. Cross-fade between menu and battle on scene transition.

## 11.3 SFX (route all through the mixer)
Minimum set: sword/physical hit, fireball/cast, ice, lightning, heal chime, ability whoosh, **crit** (distinct, punchy), miss/blocked, **Stagger BREAK** (big), boss telegraph warning, UI hover, UI click/confirm, victory fanfare, defeat sting, button-disabled. Generate via **ElevenLabs Sound Effects** (text-to-SFX, royalty-free).

## 11.4 Implementation notes
- Use `AudioSource` pooling for overlapping SFX; don't instantiate/destroy sources per shot.
- No `AudioSource.PlayClipAtPoint` with hardcoded volume; always via the manager + mixer.
- Duck music slightly under big SFX (optional, via a mixer snapshot) for punch.

---

# 12. VISUAL POLISH / GAME FEEL SPECIFICATION (juice)

"Juice it or lose it." This is where a turn-based game with off-the-shelf assets becomes *fun and stunning*. Every impact must feel weighty. Implement a central **JuiceController** (presentation) that subscribes to event channels and orchestrates the effects below. None of this touches combat logic.

## 12.1 Hit / impact feedback (on `OnDamageDealt`)
- **Hit-stop / freeze-frame:** briefly set `Time.timeScale ≈ 0` for ~0.05–0.12s on impactful hits (scale duration with damage/crit), then restore. The single highest-impact juice technique.
- **Screen shake** via **Cinemachine Impulse** — generate an impulse scaled to damage; crits and Flame Breath shake harder. (Use Cinemachine, not manual camera jitter.)
- **Screen flash / hit flash:** brief white flash on the struck character (material flash) and a subtle full-screen flash on big hits.
- **Knockback/lunge:** attacker lunges toward target and back (small position tween); target recoils. Sells contact without new animations.

## 12.2 Floating combat text (on `OnDamageDealt`/`OnHealed`)
- Spawn pooled TMP popups at the target: number scales/pops in then drifts up and fades (coroutine or DOTween "punch" scale). **Color-coded:** white normal, **orange crit** (bigger), **cyan "WEAK!"**, grey "Resist", green heal, purple DoT. Crits/weakness get extra size + a shake.

## 12.3 Stagger / Break spectacle (on `OnStaggerBroken`)
- Full-screen **"BREAK!"** banner, brief **slow-motion**, a shockwave VFX on the boss, color-grade punch (saturation/contrast bump via the Volume), distinct stinger SFX. Make breaking the boss feel like the highlight of the fight — because it is.

## 12.4 Telegraph clarity (on `OnBossTelegraph`)
- Pulsing warning banner + the boss's wind-up animation + a charging VFX (e.g. fire gathering at the Dragon's mouth) + rising audio cue. The threat must be *unmistakable* so the player's choice (defend/break/dodge) is informed.

## 12.5 Post-processing pulses
- On crit/Break: quick **Chromatic Aberration** + slight **Lens Distortion**/zoom punch (Cinemachine FOV/ortho-size kick) then ease back. On low party HP: subtle red **Vignette** pulse. Keep subtle; never sacrifice readability.

## 12.6 UI juice
- Buttons scale/brighten on hover, depress on click (+ SFX). Bars **lerp** smoothly (HP/MP/Stagger animate to new values, with a delayed "chip" bar for damage). Panels slide/fade in (CanvasGroup tweens). Turn-order tracker animates reorders. Victory/defeat screens animate in.

## 12.7 Ambient life
- Idle bob/breathing on combatants (Animator). Arena ambience: embers/dust particles, flickering lava light for the Dragon. Camera subtly breathes / re-frames on phase change (Cinemachine).

## 12.8 Implementation rules
- All juice is **event-driven and pooled** (no per-frame allocations, no Instantiate/Destroy churn — see perf budget §16).
- Juice must **degrade gracefully**: if an effect's asset is missing, log a warning and continue (never break the fight for a missing particle).
- Keep a single `JuiceConfig` SO for intensities (shake amount, hit-stop duration, etc.) so it's tunable in one place.

---

# 13. NARRATIVE SPECIFICATION (Ink — external framework bonus, +10)

Ink (by inkle, MIT-licensed) is our **External Framework / API** bonus. To earn the full +10 it must be **cleanly integrated and deeply connected to gameplay**, not a bolted-on cutscene. The connection: Ink choices and variables **read and write combat state**.

## 13.1 Why Ink, and the integration thesis
- Ink is a mature, free narrative scripting language with an official Unity integration. It's text-authorable (so Claude Code can write the entire script), version-controllable, and designed exactly for branching dialogue + variables.
- **Deep connection requirement:** the BattleManager exposes state to Ink, and Ink exposes choices/variables back. A pre-fight choice changes how the fight starts; mid/post-fight beats reflect what happened (deaths, which element you exploited, whether you broke the boss). This makes the narrative *mechanically meaningful*, which is what the rubric rewards.

## 13.2 Architecture
- **`NarrativeRunner`** (in `RPGArena.Narrative`): wraps Ink's `Story`, advances text, surfaces choices to a dialogue UI, and binds **external functions** + **variable observers** to the combat layer.
- Story flows are small `.ink` files in `Assets/Ink/` (auto-compiled to JSON by the Ink package). One per boss intro + a short framing intro + per-boss victory/defeat beats.
- **Bindings (the deep hook):**
  - Ink → game: `EXTERNAL startWithTelegraph()`, `EXTERNAL revealWeakness()`, choices set Ink vars (`taunt_dragon`, `study_dragon`) that the BattleManager reads at `BattleSetup` to alter the opening (e.g. Dragon opens with Charging Breath if taunted; weakness shown immediately if studied).
  - Game → Ink: the runner sets Ink vars from `RunState`/battle results (`bosses_cleared`, `heroes_lost`, `exploited_ice`, `broke_boss`) so dialogue references what actually happened (e.g. the Black Mage taunts you about a fallen hero).
- Dialogue UI: a simple TMP textbox + choice buttons, themed to match the menus; appears at fight intro and key beats; pauses combat input while active.

## 13.3 Content (Claude Code writes the .ink)
- **Framing intro:** the premise (§13.4), shown once at run start.
- **Per-boss intro** with a 1–2 option choice that sets a gameplay-affecting variable (the Dragon's taunt/study choice is the template; give each boss one meaningful pre-fight decision).
- **Victory/Defeat stingers** referencing the result. Keep prose tight — flavor, not walls of text.

## 13.4 Story premise (light, serves the boss-rush)
The heroes are champions who enter **the Arena of the Algorithms**, a trial where legendary threats — an ancient **Dragon**, the world-ending **Black Mage**, and a fallen **Evil Warrior** — are bound as the ultimate test. Clear all three to prove yourself. The premise exists to justify a boss gauntlet and give each boss a voice; it does not need deep lore. (Keep MapleStory as flavor inspiration only — original text, no copyrighted lore.)

---

# 14. BONUS FEATURES SPECIFICATION (target the full +35)

## 14.1 Cheat Manager (+5) — dev/Editor only
A robust developer cheat panel, **compiled out of release builds**. Wrap everything in `#if UNITY_EDITOR || DEVELOPMENT_BUILD`. This both earns the bonus and massively speeds *your* testing.
- **Activation:** a hotkey (e.g. backquote `` ` ``) toggles an on-screen panel (IMGUI `OnGUI` or a uGUI dev canvas). Never present in a normal player build.
- **Options (at least these; "full cheat manager with robust options" is the bar):**
  - God Mode (party invincible) / one-shot kill toggle.
  - Refill all HP/MP; set boss HP (slider) — jump to "almost dead" for phase/ending tests.
  - **Force Stagger / Break now** (test the burst window instantly).
  - Skip to boss / select boss; restart fight; instant Win / instant Lose.
  - Toggle infinite MP; reset cooldowns.
  - Add/clear any **status effect** on any combatant (test synergies fast).
  - Toggle telegraphs/AI on/off; force the Dragon's next move; advance the AI cycle.
  - Time-scale slider (slow-mo/fast-forward for inspecting juice).
  - Spawn floating-text/VFX test; toggle the combat-log verbosity.
- Implement cheats as **Commands** where possible (`InstantWinCommand`, `ForceStaggerCommand`) so they reuse the engine and stay consistent. Document the panel in the README (dev section) and **show it briefly in the demo video** to claim the bonus.

## 14.2 AI Implementation (+10)
Covered by the **Strategy-pattern boss AI** (§4.14): stateful telegraphed cycles (Dragon), utility-scored targeting (Evil Warrior focuses the weakest hero), and high-variance decision weighting (Black Mage). This is genuine decision-making logic, cleanly pluggable via SO assets.
- To *also* tick the literal "Unity AI systems (NavMesh / AI behavior)" phrasing, optionally implement **one** boss brain as a **Unity Behavior Graph** (the new Unity behavior package) that ticks the same decisions — but the Strategy implementation is the primary, portable, explainable one. In the video, explain the decision logic in plain English (the doc gives you the script).

## 14.3 External Framework / API (+10)
Covered by **Ink** (§13), deeply connected to gameplay. Document the integration thoroughly in the README (what Ink is, why, how it's wired to combat) to claim full marks.

## 14.4 Creativity & Extra Effort (+10)
Covered by the **Stagger/Break system, cross-class status synergies, stance/element-attunement switching, the telegraph-cancel skill expression, and the full juice layer** — well beyond a baseline turn-based game. Make sure the video calls these out as deliberate creative systems.

## 14.5 Bonus summary
Banked: Cheat Manager +5, AI +10, Ink +10, Creativity +10 = **+35** (the cap). All four are achievable within the Dragon-first slice (Milestone 5), so even a Dragon-only submission can claim the full bonus.

---

# 15. PROJECT MANAGEMENT & PROCESS (10 pts)

The course grades **effective PM tools, clear roles, consistent task tracking**, mapped to its Scrum/User-Story/Trello methodology. This must look real and consistent across the project, not back-filled.

## 15.1 Scrum mapping
- **Product backlog = the milestones in §8**, decomposed into user stories. Story format: *"As a player, I can exploit the Dragon's ice weakness for bonus damage, so that smart element choices feel rewarding."* Each milestone bullet → 1–3 stories with acceptance criteria drawn from this doc.
- **Sprints:** treat each milestone (or half-milestone) as a sprint with a goal. Keep a short sprint backlog and a "done" column.
- **Roles:** even if the team is small, define them (e.g. owner = Product Owner + Lead Engineer; Claude Code = implementation pair). Document who did what (the README's project-management section + commit history demonstrate this).

## 15.2 Trello (or GitHub Projects) board
- Columns: **Backlog → To Do (this sprint) → In Progress → In Review/Testing → Done.**
- Cards = user stories/tasks with acceptance criteria, labels (system: Combat/UI/Art/Audio/Narrative/Bonus), and links to the relevant commits.
- **Consistency is graded:** move cards as work happens; don't dump everything in "Done" at the end. Take a couple of board screenshots over time for the README.

## 15.3 Git workflow (the `main` branch is graded)
- Work on `main` (or short feature branches merged promptly to `main`); **`main` must always compile and be the best version**.
- **Commit conventions:** small, frequent, descriptive. Prefix by area, e.g. `combat: add DamagePipeline element step`, `ui: wire Stagger bar to OnStaggerBuilt`, `art: import Dragon model + Animator`. The commit history *is* part of the PM evidence — keep it clean and meaningful.
- Push after every completed step (§18). Tag a commit at each milestone (`m1-core-combat`, etc.).
- Ensure the renamed **`.gitignore`** (Unity template) is in place so `Library/`, `Temp/`, `Logs/`, build artifacts, etc. are not committed. Commit `Assets/`, `Packages/`, `ProjectSettings/`.

---

# 16. TESTING & QA

Functionality (30 pts) and technical stability are graded hard. Bake testing into the workflow, not just the end.

## 16.1 Automated tests (edit-mode, Unity Test Framework)
Add a small but real suite in `RPGArena.Tests` (also helps Code Quality):
- **Damage formula:** weakness ×1.5, resist ×0.5, immune ×0, crit, defense mitigation, defend halving, clamp ≥0 — assert expected outputs for known inputs.
- **Elemental matrix:** every (attackElement, profile) pair returns the right multiplier; Dragon absorbs Fire (heals) and is weak to Ice.
- **Stagger:** meter builds correctly, breaks at threshold, applies the multiplier window, resets.
- **Status/synergy:** Wet+Lightning ⇒ stun proc; Oiled+Fire ⇒ bonus burn; durations tick and expire.
- **Turn order:** speed-sort + extra-turn cap (no infinite loops).
Keep compute logic pure enough to test without a scene (that's why the DamagePipeline's compute half is separated, §4.8).

## 16.2 Manual play-test checklist (run before each milestone commit)
- Full loop: Menu → Select 3 → Fight → Win and Lose paths → Retry/Menu/Quit all work.
- Each class's every ability: correct target rule, MP cost, cooldown, element, status, VFX/SFX, animation, floating number.
- Each boss: AI cycle/telegraph correct; phase transition fires; Stagger window works; telegraph-cancel works (Dragon).
- All four trios can clear the Dragon (§6.5).
- Pause works (timescale, input gated); no manager duplication after returning to menu and replaying.
- Settings sliders change audio; persist across restart.
- Resolutions: UI readable/non-overlapping at 16:9 common sizes; fullscreen/windowed.
- Cheat panel absent in a non-dev build; present in dev build.

## 16.3 The submission sanity check (must all pass — §17, §2.2)
- [ ] Fresh `git clone` opens in Unity 6.3 with no missing assets/scripts.
- [ ] **All scenes in Build Settings**, correct order; game starts from the first scene.
- [ ] Playable start → finish without console errors; no red error spam, no warning spam, no broken refs.
- [ ] No missing-reference/null exceptions during a normal playthrough.
- [ ] No uncontrolled spawning / memory growth; VFX/audio pooled; no GameObject leak across fights.
- [ ] **Exit** quits the built app; in Editor it stops play mode safely.
- [ ] Animator Controllers drive all animation; AudioMixer routes all audio.
- [ ] README complete with setup, controls, mechanics, assets+licenses, PM, AI-tool disclosure.
- [ ] A Windows build (`.exe`) runs and is play-tested end-to-end.

## 16.4 Performance budget
- Target a smooth 60 FPS on a typical laptop (turn-based is light, but juice + particles can bite).
- **Pool** floating text, VFX, and AudioSources — never Instantiate/Destroy per hit.
- No per-frame allocations in combat hot paths; cache references; avoid `Find`/`GetComponent` in `Update`.
- Cap simultaneous particles; keep post-processing tasteful; watch overdraw from big VFX.
- No infinite loops in turn/AI logic (extra-turn cap; AI always returns a valid action or passes).

---

# 17. SUBMISSION DELIVERABLES

## 17.1 README (from the course template — 15 pts; fill every section)
- **Title & one-line pitch.**
- **How to run:** Unity version (6.3 LTS `6000.3.x`), open the project, open `MainMenu`, press Play **or** run the provided Windows build. Note any first-open steps (TMP Essentials import auto-prompts).
- **Gameplay & objective:** boss-rush; pick 3 of 4 heroes; beat the Dragon (then Black Mage, Evil Warrior); win/lose conditions.
- **Controls:** full scheme (§9.7) — mouse + keyboard; pause/restart/quit.
- **Mechanics:** the five+ counted mechanics (§5.10) explained briefly (elemental matrix, MP economy, Stagger/Break, status synergies, stance switching, FSM turns).
- **Architecture overview:** the four patterns (Command, State, Strategy, ScriptableObject data + events) — short, so the grader sees the design intent.
- **Assets & credits:** every asset with **source + license** (Mixamo, Asset Store/Meshy, VFX pack, music tool, SFX tool, UI kit, Ink). 
- **AI-tool disclosure:** state honestly that the project was built with AI assistance (Claude Code + Unity MCP, Unity AI, gen-AI for some art/audio). Disclosure is good practice and avoids any integrity concern; the *engineering, design, and integration* are the student's.
- **Bonus features:** Cheat Manager (how to open — dev build), Ink integration (what/why/how it's wired to combat), AI design, creativity systems.
- **Project management:** Scrum approach, board link/screenshots, roles, sprint summary.
- **Known issues / future work** (honest, short).

## 17.2 Demo video (5–10 min, narrated — required; the one irreducible manual task)
This is the deliverable a tool can't make for you, and likely the basis of any viva — so the **nameable-patterns architecture exists precisely so you can explain it confidently**. Suggested beats:
1. 30s pitch + show the Main Menu → Character Select.
2. Play the Dragon fight: narrate a smart line — exploit Ice (and explain why **not** Fire: it absorbs), set up Wet→Freeze, build Stagger, **break during the charge to cancel Flame Breath**, unload in the window. This showcases the 5+ mechanics live.
3. Show class identity (swap heroes/trios), stance/attunement switching, healing/defense, Puppet/Dark Sight survival.
4. Architecture walkthrough (screen-share the project): "abilities are ScriptableObjects (data), actions are Command objects, the battle is a finite state machine, boss AI is the Strategy pattern, systems talk through ScriptableObject event channels." Show the combat log as evidence of the Command pattern.
5. Bonus tour: Ink intro choice affecting the fight; the Cheat Manager (dev build); call out the juice/creativity systems.
6. Briefly show the Trello board + README + commit history (PM evidence).
Keep it tight and confident; the doc gives you the script for every claim. Record clean audio.

## 17.3 Build settings
- Platform: **Windows Standalone** (`.exe`). Scenes added in order (first scene = `Boot` or `MainMenu`).
- Set product name, icon (optional), default resolution, windowed/fullscreen toggle in Player Settings.
- **Development Build** for your testing (enables the Cheat Manager + profiler); a **non-dev release build** to confirm cheats are stripped. Test the release build end-to-end and that **Exit quits**.
- Submit the repo link via **GitHub Classroom** (the `main` branch is graded). Submitting on Moodle is a formality; the repo is the artifact.

---

# 18. WORKING AGREEMENT FOR CLAUDE CODE (operational rules)

How you (Claude Code) should behave on this project, every session. These are binding.

## 18.1 The loop (every task)
1. **Read context:** this `CLAUDE.md` (root) + the relevant scripts/assets. Re-read the section you're working in.
2. **Plan briefly:** state the small increment you're about to do and which milestone/section it serves.
3. **Implement** the increment (scripts via filesystem; scenes/prefabs/SO assets/wiring via the Unity MCP bridge).
4. **Compile & read the Console** via MCP. Fix **all** errors and meaningful warnings before continuing. Never leave `main` non-compiling.
5. **Verify logic:** run/extend an edit-mode test, or add a temporary `Debug.Log` trace and confirm expected output (turn order, damage, multiplier, stagger, win/lose).
6. **Commit & push to `main`** with a clear, area-prefixed message. Tag at milestones.
7. **Summarize:** what changed, compile/play status, decisions/assumptions, what's next.

## 18.2 Definition of Done (per task)
A task is done only when: it compiles; it does what the spec says; it's wired through **events** (not direct cross-layer calls); it follows the §3.4 conventions and comment style; it added no paid/non-essential dependency; relevant tests/QA pass; and it's committed to `main`.

## 18.3 Decision-making
- **Default to acting, not asking.** When a detail is unspecified, choose the option that best satisfies (rubric → architecture → polish), implement it, and note the assumption in the summary. Only stop for the human when a true external action is required (asset pull, video, account/login, a paid choice).
- **Data over code** for anything content-shaped. If you're about to `switch` on a content type, make it a ScriptableObject field instead.
- **Prefer the simplest design that meets the spec.** Start systems with primitives; add art/juice in the designated milestone. Don't gold-plate early.
- **Never compromise the core target** (complete, polished, fun, complex) for the sake of automation convenience. If automation would degrade quality, do the higher-quality path and tell the human what (if anything) they need to do.

## 18.4 Guardrails
- **No paid Asset Store packages** or dependencies the grader must buy/install. Free, essential, included, documented — or don't add it.
- **No Built-in-pipeline shaders** (URP only). **No legacy Input** scattered in code (use the Input System). **No hardcoded audio** (AudioMixer). **No loose animation clips** (Animator Controllers). **No scattered singletons/static mutable state** (events + guarded bootstrap).
- **Keep `main` shippable** at all times. If a change is risky, branch, but merge back promptly and keep `main` the best version.
- **Disclose AI tooling** in the README (it's encouraged); never claim hand-made art/audio that was generated.
- **Respect child-safety / content norms:** this is a clean fantasy combat game; keep all content appropriate.

## 18.5 When stuck
- If a tool/bridge action fails, read the Console, diagnose, and retry a different way before involving the human. If you must involve the human, give the **exact** step and continue with everything else you can do meanwhile.
- If two parts of this doc seem to conflict, prefer: (1) rubric requirements, (2) the architecture in §4, (3) the milestone order in §8, (4) polish. Note the conflict in your summary so the human can adjust the doc.

---

# 19. GLOSSARY & QUICK-REFERENCE DATA TABLES

## 19.1 Glossary
- **Stagger / Break:** a meter the party fills by pressuring the boss (esp. weakness hits); on fill, the boss skips a turn and takes a big damage multiplier — the burst window.
- **Telegraph:** a one-turn-ahead warning of a big boss attack (`OnBossTelegraph`), giving the player an informed choice.
- **Attunement (Mage) / Stance (Warrior/Thief/Archer):** an in-combat toggle that re-shapes a class's kit without new art.
- **Flag (status):** a status that doesn't directly damage but enables a synergy (Oiled, Wet, Marked).
- **Synergy / combo:** one class sets a flag, another consumes it for a big payoff (Oiled+Fire, Wet+Lightning/Ice, Marked+anything).
- **Command:** a runtime object representing one action, built from `Ability` data and executed by the engine.
- **Event channel:** a ScriptableObject used as a decoupled event bus between core logic and presentation.
- **Element profile:** an entity's weak/resist/immune/absorb reactions per element — the puzzle of each boss.
- **Run:** one playthrough of the boss gauntlet (Dragon → Black Mage → Evil Warrior).

## 19.2 Elemental matrix (quick ref — see §5.4)
Weak ×1.5 · Neutral ×1.0 · Resist ×0.5 · Immune ×0 · Absorb = heal target.
Elements: Physical, Fire, Ice, Lightning, Holy, Dark.

## 19.3 Boss weaknesses (the three puzzles)
| Boss | Weak (×1.5) | Resist/Absorb | Stagger window | Tests |
|---|---|---|---|---|
| **Dragon** | **Ice** | **Absorbs Fire**; slightly resists Physical pre-break | the Charging Breath turn (break = cancel Flame Breath) | timing & defense |
| **Black Mage** | **Holy/Light** | resists Dark | mid-cast (break = cancel nuke) | adaptability & cleanse |
| **Evil Warrior** | **Lightning** | resists Physical until Staggered | the wind-up (break = strip armor) | protection & tempo |

## 19.4 Status quick ref (see §5.8)
DoT: Burn, Poison, Bleed. Flags: Oiled, Wet, Marked. Control: Frozen, Stun. Buffs: Defending, Stealth, Rage, Bless, Haste. Debuffs: Weaken, Blind.

## 19.5 Class one-liners (see §6)
- **Warrior (STR):** tanky anchor; Power Strike / Slash Blast / Rage / Guardian Taunt / Berserk stance / Crushing Blow.
- **Mage (INT):** elemental nuker + healer; Magic Bolt / Attunement / school nukes / Heal / Bless / Magic Guard / Meteor·Blizzard.
- **Thief (LUK):** combo enabler; Lucky Seven / Oil Bomb / Water Bomb / Shadow Mark / Dark Sight / Smoke Bomb / Assassinate.
- **Archer (DEX):** ranged precision; Double Shot / Soul Arrow / Mark Target / Puppet / Eye of Amazon / Arrow Rain.

## 19.6 The five+ counted mechanics (see §5.10)
1. FSM turn-based combat + initiative & extra-turn economy. 2. Elemental matrix. 3. MP economy + cooldowns. 4. Stagger/Break. 5. Cross-class status synergies. 6. Stance/attunement switching. *(Need 5; we ship 6.)*

## 19.7 Architecture in one breath (see §4)
ScriptableObject **data** (content) → **Command** objects (actions) executed by a **finite-state-machine** BattleManager, with enemy **Strategy** AI, all systems decoupled through ScriptableObject **event channels**, presentation (UI/audio/juice/animation/Ink) reacting only to events.

## 19.8 Build order in one breath (see §8)
Scaffold → core combat vs Dragon (primitives, 1 hero) → 4 classes + party → UI/menus/flow → assets/animation/lighting → audio/juice/Ink/cheats → (Dragon slice complete) → clone for Black Mage & Evil Warrior → stabilize/document/build/video/submit.

---

*End of CLAUDE.md. This document is the source of truth; keep it updated as systems evolve, and prefer editing it over letting it drift. Build the Dragon fight to excellence first — everything else follows.*

---

# APPENDIX A — CORE CODE SKELETONS (contracts to implement against)

These are **reference skeletons**, not final implementations. They lock in the names, signatures, and shapes that the rest of this document assumes, so generated code is consistent. Keep the low-level English comment style. Namespaces rooted at `RPGArena`. Flesh out bodies during the relevant milestone; do not treat these as complete.

## A.1 Elements & the matrix
```csharp
// Element identity for attacks and resistances. Kept as an enum for speed and
// simple switch-free table lookups; the matrix lives in data, not in code.
namespace RPGArena.Combat
{
    public enum ElementType { Physical, Fire, Ice, Lightning, Holy, Dark }

    // How a defender reacts to an incoming element. Drives the damage multiplier.
    public enum ElementReaction { Neutral, Weak, Resist, Immune, Absorb }
}

// A ScriptableObject lookup table mapping (attackElement -> reaction) for a profile.
// Each Entity has its own ElementProfile asset/struct describing its weaknesses.
namespace RPGArena.Combat
{
    using UnityEngine;

    [CreateAssetMenu(menuName = "RPGArena/Element Profile")]
    public class ElementProfile : ScriptableObject
    {
        // Designer fills these lists in the Inspector; everything else is Neutral.
        public ElementType[] weakTo;
        public ElementType[] resistTo;
        public ElementType[] immuneTo;
        public ElementType[] absorbs; // e.g. the Dragon absorbs Fire (heals!)

        // Returns how this profile reacts to an incoming element.
        public ElementReaction GetReaction(ElementType incoming)
        {
            // Order matters: absorb/immune override weak/resist if mis-authored.
            // (Implementation: check arrays in priority order, default Neutral.)
            return ElementReaction.Neutral; // placeholder
        }

        // Converts a reaction into a damage multiplier (tunable via BalanceConfig).
        public static float MultiplierFor(ElementReaction r, BalanceConfig cfg)
        {
            // Weak -> cfg.weakMult (1.5), Resist -> cfg.resistMult (0.5),
            // Immune -> 0, Absorb -> negative sentinel (caller heals instead),
            // Neutral -> 1.0.
            return 1f; // placeholder
        }
    }
}
```

## A.2 Stats & balance config
```csharp
namespace RPGArena.Characters
{
    using System;
    using UnityEngine;

    // Plain serializable stat container. Embedded in definitions (base values)
    // and copied per-Entity at runtime (current, mutable values).
    [Serializable]
    public class StatBlock
    {
        // Primary stats (MapleStory mapping: STR/DEX/INT/LUK).
        public int STR, DEX, INT, LUK;
        // Resources.
        public int maxHP, maxMP;
        // Base offense/defense knobs (derived stats computed from these + primaries).
        public int baseAttack, baseMagicAttack, baseDefense, baseSpeed;
        public float baseAccuracy, baseEvasion, baseCritChance;
        public float critDamage = 1.5f;

        // Returns a deep copy so runtime entities never mutate shared SO data.
        public StatBlock Clone() => (StatBlock)MemberwiseClone();
    }
}

namespace RPGArena.Core
{
    using UnityEngine;

    // One place for every global tuning constant so balancing is centralized.
    [CreateAssetMenu(menuName = "RPGArena/Balance Config")]
    public class BalanceConfig : ScriptableObject
    {
        [Header("Element multipliers")]
        public float weakMult = 1.5f, resistMult = 0.5f;
        [Header("Defense mitigation")]
        public float defenseK = 100f; // mitigation = Def / (Def + K)
        [Header("Stagger")]
        public float staggerDamageMult = 1.85f;
        public float staggerBuildNormalHit = 8f, staggerBuildWeaknessHit = 20f, staggerBuildBreakSkill = 30f;
        [Header("Action economy")]
        public int maxExtraTurnsPerEntityPerRound = 1;
        [Header("Stat derivation constants (k1..k8)")]
        public float k1 = 2f, k2 = 2.2f, k3 = 0.5f, k4 = 1f, k5 = 1f, k6 = 1f, k7 = 0.5f, k8 = 0.4f;
    }
}
```

## A.3 Ability data (ScriptableObject) and the Command pattern
```csharp
namespace RPGArena.Combat
{
    using UnityEngine;
    using RPGArena.Combat.Status;

    public enum EffectType { Attack, MultiHit, Heal, Buff, Debuff, ApplyStatus, Stance, Defend, BossMove, Composite }
    public enum TargetRule { SingleEnemy, AllEnemies, SingleAlly, AllAllies, Self, Summon }

    // DATA ONLY. An ability is a designer-authored asset; behavior lives in Commands.
    [CreateAssetMenu(menuName = "RPGArena/Ability")]
    public class Ability : ScriptableObject
    {
        [Header("Identity")]
        public string displayName;
        [TextArea] public string description;
        public Sprite icon;

        [Header("Rules")]
        public EffectType effectType;
        public TargetRule targetRule;
        public ElementType element = ElementType.Physical;
        public float power = 1.0f;        // 1.0 == 100%
        public int hits = 1;              // for MultiHit
        public int mpCost = 0;
        public int cooldown = 0;

        [Header("Status / synergy")]
        public StatusEffectDefinition[] statusesToApply;

        [Header("Presentation (read by listeners, not logic)")]
        public GameObject vfxPrefab;
        public string sfxId;
        public string animationTrigger = "Attack";

        [Header("Tags")]
        public string[] tags;             // e.g. "BreakSkill", "Ranged", "Setup", "Finisher"
    }
}

namespace RPGArena.Combat.Commands
{
    using System.Collections;
    using RPGArena.Characters;

    // Carries everything one action needs to resolve. Built by the BattleManager
    // from the chosen Ability + caster + target(s). Passed to the active Command.
    public struct ActionRequest
    {
        public Ability ability;
        public Entity caster;
        public Entity[] targets;
    }

    // The Command pattern. Each concrete action implements Execute as a coroutine
    // so animation/VFX/hit-stop/floating-text can sequence cleanly. DescribeForLog
    // feeds the combat-log UI (uniform logging is a free benefit of this pattern).
    public interface ICommand
    {
        IEnumerator Execute(BattleContext ctx);
        string DescribeForLog();
    }

    // Concrete commands to implement (one responsibility each):
    // AttackCommand, MultiHitAttackCommand, HealCommand, BuffCommand, DebuffCommand,
    // ApplyStatusCommand, StanceSwitchCommand, DefendCommand, BossMoveCommand,
    // CompositeCommand (runs a list in order), plus cheat commands
    // InstantWinCommand / ForceStaggerCommand (dev only).
}
```

## A.4 Damage pipeline (compute half is pure & unit-testable)
```csharp
namespace RPGArena.Combat
{
    using RPGArena.Characters;

    // Input to the pipeline. Flags let synergies/stances modify the result.
    public struct DamageInfo
    {
        public Entity source, target;
        public Ability ability;
        public ElementType element;
        public float basePower;
        public bool isMagic;
        public bool forceHit;     // many abilities auto-hit
        public bool isBreakSkill; // adds extra stagger
    }

    // Output. Presentation reads this from OnDamageDealt to drive numbers/juice.
    public struct DamageResult
    {
        public int amount;
        public bool hit, crit, absorbed;
        public ElementReaction reaction;
        public float staggerBuilt;
    }

    public interface IDamagePipeline
    {
        // PURE compute (no side effects) so it can be unit-tested without a scene.
        DamageResult Compute(DamageInfo info, Balance/*Config*/ cfg);
        // Applies the result to the target and raises events (side effects here).
        void Apply(DamageInfo info, DamageResult result, BattleContext ctx);
    }
    // Steps inside Compute, in order (see §4.8): hit check -> base -> element ->
    // defense -> stagger -> crit -> status/defend -> clamp -> stagger build.
}
```

## A.5 Entity (runtime combatant — composition)
```csharp
namespace RPGArena.Characters
{
    using System.Collections.Generic;
    using UnityEngine;
    using RPGArena.Combat;
    using RPGArena.Combat.Status;
    using RPGArena.Combat.AI;

    // A combatant in a battle. Composes a stat block, abilities, a status container,
    // and (for bosses) an AI brain. Player-controlled when brain == null.
    public class Entity : MonoBehaviour
    {
        public string displayName;
        public StatBlock stats;                 // runtime COPY (never the SO's)
        public int currentHP, currentMP;
        public ElementProfile elementProfile;
        public List<Ability> abilities = new();
        public StatusEffectContainer status;    // active statuses + flags
        public AIBehavior brain;                // null => player-controlled
        public float staggerMeter;              // bosses only
        public bool isStaggered;

        public bool IsAlive => currentHP > 0;
        public bool CanAct => IsAlive && !isStaggered && !status.HasControlEffect;

        // Builds a runtime Entity from a definition (deep-copies stats!).
        public void InitializeFrom(StatBlock baseStats /*+ definition refs*/) { }

        public void TakeDamage(DamageResult result) { /* apply, clamp, raise events */ }
        public void Heal(int amount) { }
        public bool TrySpendMP(int amount) => false;
        public void TickStartOfTurn() { /* DoT, regen, duration decrements */ }
        public void TickEndOfTurn() { }
    }
}
```

## A.6 Battle FSM, turn system, context
```csharp
namespace RPGArena.Combat
{
    // Shared services/state handed to states and commands. Avoids static singletons.
    public class BattleContext
    {
        public System.Collections.Generic.List<Characters.Entity> heroes;
        public Characters.Entity boss;
        public TurnSystem turns;
        public IDamagePipeline damage;
        public Status.StatusEffectSystem statuses;
        public StaggerSystem stagger;
        // event channel refs injected here (OnDamageDealt, OnTurnStarted, ...)
    }

    // Base state for the battle FSM. States named exactly per §4.6.
    public abstract class BattleState
    {
        protected readonly BattleManager m;
        protected BattleState(BattleManager m) { this.m = m; }
        public virtual void Enter() {}
        public virtual void Tick() {}
        public virtual void Exit() {}
    }
    // Concrete states: BattleSetup, RoundStart, TurnStart, AwaitInput, EnemyDecision,
    // ResolveAction, CheckDeaths, TurnEnd, RoundEnd, Victory, Defeat.

    public class BattleManager : UnityEngine.MonoBehaviour
    {
        private BattleState current;
        public BattleContext Context { get; private set; }
        public void ChangeState(BattleState next) { current?.Exit(); current = next; current.Enter(); }
        private void Update() => current?.Tick();
    }

    // Speed-ordered initiative with a capped extra-turn mechanism (action economy).
    public class TurnSystem
    {
        // Builds the round order by Speed (desc); supports inserting one bonus turn
        // per entity per round on weakness/crit/stagger (see §4.7, §5.5).
        public System.Collections.Generic.Queue<Characters.Entity> BuildRoundOrder(
            System.Collections.Generic.IEnumerable<Characters.Entity> combatants) => null;
        public void GrantExtraTurn(Characters.Entity e) { /* respect the per-round cap */ }
    }
}
```

## A.7 Status effects & synergy
```csharp
namespace RPGArena.Combat.Status
{
    using UnityEngine;

    public enum StatusKind { Buff, Debuff, DoT, Flag, Control }
    public enum StatusFlag { None, Wet, Oiled, Marked, Bleeding, Frozen, Stunned, Defending, Stealthed }

    [CreateAssetMenu(menuName = "RPGArena/Status Effect")]
    public class StatusEffectDefinition : ScriptableObject
    {
        public string displayName; public Sprite icon;
        public StatusKind kind; public StatusFlag flag = StatusFlag.None;
        public int durationTurns = 3; public bool stacks;
        public float perTurnPercentHP;  // DoT
        public int perTurnFlatDamage;   // DoT
        // Stat modifiers applied while active (attack/def/acc/eva/speed deltas).
        public int attackMod, defenseMod, speedMod; public float accuracyMod, evasionMod;
    }

    // Runtime container on each Entity: applies mods, ticks durations, exposes flags.
    public class StatusEffectContainer
    {
        public bool HasControlEffect; // Frozen/Stunned => skip turn
        public bool Has(StatusFlag f) => false;
        public void Apply(StatusEffectDefinition def) { }
        public void TickStartOfTurn(Characters.Entity owner) { }
        public void TickEndOfTurn(Characters.Entity owner) { }
    }

    // Consulted by the damage pipeline (step 7) and on status application.
    // Data-described where possible: (flag + element) -> bonus effect.
    public static class SynergyResolver
    {
        // e.g. Oiled+Fire => bonus burn; Wet+Lightning => stun; Wet+Ice => Frozen;
        //      Marked+any => crit/stagger up; Bleeding+Physical => bonus damage.
        public static void ResolveOnHit(/*DamageInfo, DamageResult, target*/) { }
    }
}
```

## A.8 AI (Strategy pattern)
```csharp
namespace RPGArena.Combat.AI
{
    using System.Collections.Generic;
    using UnityEngine;
    using RPGArena.Characters;

    // Strategy base. Each boss brain is a swappable asset on its BossDefinition.
    public abstract class AIBehavior : ScriptableObject
    {
        // Returns the chosen ability and target. Must always return a valid action
        // (or an explicit pass) so the turn loop can never deadlock.
        public abstract Ability DecideAction(
            Combat.BattleContext ctx, Entity self, IReadOnlyList<Entity> heroes, out Entity target);
    }

    // Concrete strategies (separate assets):
    //  DragonCycleAI : deterministic telegraphed cycle + phase-2 enrage (build first)
    //  AggressiveAI  : utility scoring, focuses lowest-HP hero, executes low targets
    //  ChaoticAI     : high-variance targeting + debuff-favoring, occasional nuke
}
```

## A.9 Event channels (decoupling layer)
```csharp
namespace RPGArena.Core.Events
{
    using System;
    using System.Collections.Generic;
    using UnityEngine;

    // Generic ScriptableObject event channel. Create typed subclasses + assets
    // for each channel in §4.13. Presentation subscribes; core raises. No direct
    // cross-layer calls.
    public abstract class EventChannel<T> : ScriptableObject
    {
        private readonly List<Action<T>> listeners = new();
        public void Subscribe(Action<T> cb) { if (!listeners.Contains(cb)) listeners.Add(cb); }
        public void Unsubscribe(Action<T> cb) => listeners.Remove(cb);
        public void Raise(T payload) { for (int i = listeners.Count - 1; i >= 0; i--) listeners[i]?.Invoke(payload); }
    }

    // Examples: [CreateAssetMenu] DamageResultChannel : EventChannel<DamageResult> { }
    //           EntityChannel : EventChannel<Entity> { }  (turn started/ended/died)
    //           VoidChannel : EventChannel<bool> { }      (battle won/lost, etc.)
}
```

## A.10 Guarded bootstrap (no scattered singletons)
```csharp
namespace RPGArena.Core
{
    using UnityEngine;

    // The ONE persistent root. Instantiates services once and survives scene loads.
    // The duplicate guard prevents a second manager when returning to a scene.
    public class GameBootstrap : MonoBehaviour
    {
        public static GameBootstrap Instance { get; private set; }

        private void Awake()
        {
            // Standard singleton guard to prevent duplicate managers across loads.
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this; DontDestroyOnLoad(gameObject);
            // Initialize AudioManager, SceneLoader, RunState here (once).
        }
    }
}
```

---

# APPENDIX B — SCRIPTABLEOBJECT ASSET TEMPLATES (exact field layouts)

What to fill when authoring each content asset. Create these in `Assets/_Project/ScriptableObjects/<folder>/`. Values shown are the Dragon-slice starting set; balance later.

## B.1 CharacterDefinition (one per hero) — e.g. `Mage.asset`
```
className:        "Mage"
classFlavor:      "Wielder of elemental magic and holy restoration. INT-based."
primaryStat:      INT
portrait:         <sprite>
modelPrefab:      <Mixamo mage FBX prefab>
baseStats:        STR 5  DEX 8  INT 30  LUK 7
                  maxHP 70  maxMP 140
                  baseDefense low  baseSpeed mid
abilities:        [ Mage_MagicBolt, Mage_Attunement, Mage_Fireball,
                    Mage_IceLance, Mage_Spark, Mage_Heal, Mage_Bless,
                    Mage_MagicGuard, Mage_Meteor, Mage_Blizzard ]
elementProfile:   Mage_Profile (neutral; maybe weak Physical)
stance:           ElementAttunement (Fire/Ice/Lightning/Holy)
```
Repeat for `Warrior.asset` (STR, high HP/Def, abilities §6.1), `Thief.asset` (LUK, high eva/crit, §6.3), `Archer.asset` (DEX, high acc/crit, §6.4).

## B.2 Ability (one per skill) — e.g. `Mage_IceLance.asset`
```
displayName:      "Ice Lance"
description:      "Hurls a spear of ice. Strong vs the Dragon; freezes Wet targets."
icon:             <sprite>
effectType:       Attack
targetRule:       SingleEnemy
element:          Ice
power:            1.6        // 160%
hits:             1
mpCost:           12
cooldown:         0
statusesToApply:  [ Frozen (conditional on Wet) ]
vfxPrefab:        VFX_IceImpact
sfxId:            "sfx_ice"
animationTrigger: "Cast"
tags:             [ ]
```
Author all abilities from §6 (heroes) and §7 (bosses) this way. The Dragon's `Dragon_FlameBreath.asset`: element Fire, AllEnemies, high power, tag `Telegraphed`; `Dragon_ChargingBreath.asset`: effectType BossMove, raises the telegraph, no damage.

## B.3 BossDefinition — `Dragon.asset`
```
displayName:      "The Dragon"
modelPrefab:      <dragon FBX prefab>
portrait/intro:   <sprite>, Ink knot "dragon_intro"
baseStats:        large HP (size for ~10–16 hero actions), high Attack,
                  moderate Defense, low Speed
abilities:        [ Dragon_ClawSwipe, Dragon_TailSweep, Dragon_TailGuard,
                    Dragon_ChargingBreath, Dragon_FlameBreath, Dragon_Enrage ]
aiBehavior:       DragonCycleAI
elementProfile:   weakTo [Ice]; absorbs [Fire]; resistTo [Physical](pre-break)
staggerThreshold: tuned so a focused party breaks ~every 3–4 rounds
phases:           [ Phase1 (100%→40%), Phase2 (≤40%: enrage, shorter cycle) ]
```
Then `BlackMage.asset` (weak Holy, ChaoticAI, §7.3) and `EvilWarrior.asset` (weak Lightning, resist Physical, AggressiveAI, §7.4).

## B.4 StatusEffectDefinition — examples
```
Wet.asset:    kind Flag, flag Wet, duration 3, +lightning/ice vulnerability hooks
Oiled.asset:  kind Flag, flag Oiled, duration 3, +50% fire taken
Burn.asset:   kind DoT, perTurnPercentHP 0.05, duration 3
Frozen.asset: kind Control, flag Frozen, duration 1 (skip a turn)
Rage.asset:   kind Buff, attackMod +X, duration 3, target AllAllies
Bless.asset:  kind Buff, accuracyMod +X defenseMod +X, duration 3
```
Author the full §5.8 table.

## B.5 ElementMatrix / BalanceConfig / JuiceConfig
- `BalanceConfig.asset`: fill the constants from §A.2 (weak 1.5, resist 0.5, defenseK 100, staggerDamageMult 1.85, stagger build values, k1..k8).
- `JuiceConfig.asset`: hitStopBase 0.06s, hitStopCritBonus, shakeAmplitude, shakeOnCritMult, breakSlowMoScale 0.3, breakSlowMoDuration, flashIntensity, floatTextRiseSpeed, etc.
- Event channel assets: one per channel in §4.13.

---

# APPENDIX C — SAMPLE INK SCRIPT (Dragon intro, deeply wired)

Place in `Assets/Ink/dragon_intro.ink` (auto-compiles to JSON). The `EXTERNAL` functions and the `taunt_dragon`/`study_dragon` variables are read by the `NarrativeRunner` and `BattleManager` to alter how the fight starts (§13.2). This is the template for every boss intro.

```ink
// dragon_intro.ink — shown at BattleSetup before the Dragon fight.
// Variables here are observed by the game; the choice changes how combat opens.

VAR taunt_dragon = false
VAR study_dragon = false

// Game binds these. They let the story affect the battle state directly.
EXTERNAL StartWithTelegraph()   // make the Dragon open by charging (riskier/faster)
EXTERNAL RevealWeakness()       // surface the Dragon's Ice weakness / Fire-absorb in UI

=== dragon_intro ===
The cavern shakes. An ancient dragon uncoils from the lava, eyes like furnace doors.
"Another band of champions? You will make fine ash."
* [Taunt it — "Your fire is nothing to us."]
    ~ taunt_dragon = true
    The dragon's nostrils flare. It rears back, already gathering flame.
    ~ StartWithTelegraph()
    -> begin
* [Study it — read its stance before striking.]
    ~ study_dragon = true
    You watch how the flames coil around it — fire feeds this beast; ice will bite it.
    ~ RevealWeakness()
    -> begin
* [Say nothing. Draw your weapons.]
    The dragon roars. Battle is joined.
    -> begin

=== begin ===
-> DONE
```
The `NarrativeRunner` also **sets** Ink variables from the game (e.g. `bosses_cleared`, `heroes_lost`, `exploited_ice`, `broke_boss`) before later beats so victory/defeat dialogue references what actually happened. Write `dragon_victory.ink`, `dragon_defeat.ink`, and equivalents for the other bosses the same way.

---

# APPENDIX D — FIRST-RUN PROMPT SEQUENCE (paste into Claude Code)

Exact prompts to drive Milestones 0–1. Run them in order in the project folder (with this `CLAUDE.md` at the root). After each, let Claude Code compile, read the Console, and commit before moving on. (Later milestones follow §8; prompt them similarly, one increment at a time.)

**0a — Scaffold**
> Read CLAUDE.md. Create the full folder structure and assembly definitions from §3.5. Set the main camera to Orthographic. Create the `MainMenu` and `BattleArena` scenes and add both to Build Settings. Don't build gameplay yet — just the skeleton. Compile, fix all errors, summarize, and commit to main.

**0b — Core data & event channels**
> Read CLAUDE.md §4.13, §A.1–A.2, §A.9. Implement `ElementType`, `ElementReaction`, `ElementProfile`, `StatBlock`, `BalanceConfig`, and the generic `EventChannel<T>` base plus the typed channels and channel assets listed in §4.13. Create the `BalanceConfig.asset` with the §A.2 starting values. Compile, fix errors, commit.

**0c — Bootstrap & scene flow**
> Read CLAUDE.md §4.4, §4.16, §A.10. Implement the guarded `GameBootstrap`, a `SceneLoader` with an async fade transition, a stub `AudioManager`, and a `RunState`. Wire a Boot/MainMenu flow that can load `BattleArena` with a fade. Confirm no duplicate managers when returning to MainMenu. Compile, fix errors, commit.

**1a — Combat core (primitives, one hero vs Dragon)**
> Read CLAUDE.md §4.3–4.9, §5, §A.3–A.8. Implement `Ability`, the `ICommand` interface and the core Commands, the pure `Compute` + `Apply` `DamagePipeline`, `Entity` (with runtime stat copy), the `BattleManager` FSM with all states named per §4.6, the `TurnSystem`, the `StatusEffectSystem`/container, the `StaggerSystem`, and the `SynergyResolver` framework. Author the Dragon `BossDefinition` + `DragonCycleAI` and a single Mage hero with a starter kit, all as capsule primitives. Drive a full Mage-vs-Dragon fight that logs every turn, damage, element multiplier, stagger build/break, telegraph, and win/lose to the Console. Compile, fix all errors, run it, paste the battle log in your summary, and commit.

**1b — Tests**
> Read CLAUDE.md §16.1. Add an edit-mode test assembly `RPGArena.Tests` and write unit tests for the damage formula (weakness/resist/immune/crit/defense/defend/clamp) and the elemental matrix (Dragon absorbs Fire, weak to Ice), plus stagger build/break and one synergy (Wet+Lightning ⇒ stun). Make them pass. Commit.

**1c — Verify the Dragon's signature moment**
> Confirm via a focused test or logged playthrough that filling the Stagger meter during the Dragon's Charging Breath turn breaks it and cancels Flame Breath (§4.9, §7.2). If it doesn't, fix the stagger/telegraph interaction. Commit.

From here, proceed through Milestones 2–7 in §8, one increment per prompt, always compiling, testing, and committing to `main`.

---

# APPENDIX E — APPROVED DESIGN REVISIONS (v2, owner-approved)

These four revisions are owner-approved and **override the referenced sections** where they conflict. They are part of the canonical design.

**E.1 Informed-gamble RNG (a 7th counted mechanic — overrides §5.7's "auto-hit most abilities").**
Combat gains a visible accuracy + damage-roll layer so outcomes carry *informed* luck, not blind chance.
- **Hit tiers per `Ability`:** `Reliable` (~99% — basics, all AoE, the Archer), `Standard` (~90%), `Risky` (~80–88% — big single-target nukes/ultimates). Effective hit% = tierBase + caster.Accuracy − target.Evasion, clamped to a floor/ceiling. **A Staggered/Broken target ignores evasion (≈guaranteed hit)** — extra reward for breaking.
- **Damage roll:** widen the variance band to ≈±15% (was ±5%); crit is the top band (×CritDamage). The rolled multiplier drives a dice/roll flourish + the floating-text size/color.
- **Informed gamble:** the action menu shows **hit% and a damage range** before the player commits, so accuracy/evasion become a real decision axis (and make Bless/Eye-of-Amazon, Blind/Smoke, Dark Sight, and the Black Mage's accuracy curse matter much more).
- **Anti-feel-bad guards:** `Reliable` abilities never miss; a per-caster **pity counter** forbids >2 consecutive misses; a *missed committed skill* still builds partial Stagger and/or refunds part of its MP — so a whiff is never a fully dead turn.
- Tunables live on `BalanceConfig` (`damageVarianceMin/Max`, `reliableHitBase/standardHitBase/riskyHitBase`, `hitFloor/hitCeiling`, `missStreakCap`). `Ability` gains a `hitTier` field. The `DamagePipeline` hit-check (step 1) is now a first-class step that returns the roll for presentation.

**E.2 Roguelite between-boss boons (new progression layer — adds RPG meta to the boss-rush §5/§7).**
Between bosses the run pauses for recovery + a build choice.
- Full **HP/MP restore**, then **pick 1 of 3 boons** drawn from a data-driven pool (~12).
- New `BoonDefinition` (ScriptableObject): kind (StatBoost / Passive / AbilityUpgrade / HealCharge / EconomyTweak), magnitude, target (party/hero/class), description, icon. A `BoonSystem` applies the boon's runtime modifiers for the rest of the run; `RunState.acquiredBoons` persists them; a **Boon-Select UI screen** (3 cards) presents the choice.
- Gives the run a meta-decision loop, replay depth, and a **sustain valve for trios without a Mage** (e.g., a party "Heal Charge" boon).
- Run flow becomes: Dragon → *Boon Select* → Black Mage → *Boon Select* → Evil Warrior → Victory.

**E.3 Trim to a tighter core (legibility — overrides the §5.8 catalogue and §5.9/§6 stance lists).**
- **Statuses ≈15→≈11.** Drop **Bleed** (fold "bonus vs bleeding" into **Marked**/**Weaken**). Merge **Stun** into **Frozen** — one skip-turn control (Lightning-on-*Wet* now applies *Frozen*). Keep DoTs **Burn** + **Poison**; flags **Oiled/Wet/Marked**; buffs **Defending/Stealth(Dark Sight)/Rage/Bless** (**Haste** optional/cut if underused); debuffs **Weaken/Blind**.
- **Stances → two real systems only:** **Mage Element-Attunement** and **Warrior Berserk/Guardian**. **Thief Dark Sight** and **Archer Aim/Puppet** become **plain cooldown abilities**, not bespoke stance subsystems. (Counted-mechanic #6 "stance switching" still holds via the two real systems.)

**E.4 Layered onboarding.** The Dragon fight introduces mechanics one at a time (weakness → break → combo → defend) with optional, dismissible hints, so a first-time player isn't shown all seven systems at once.

**Counted mechanics are now SEVEN** (was six): FSM turns + economy, elemental matrix, MP/cooldowns, Stagger/Break, status synergies, stance switching, **+ informed-gamble accuracy/crit RNG**.

---

*Appendices end. Together with the main sections, this document specifies the entire game — design, architecture, content, build order, and exact contracts — so Claude Code can build it with minimal further input. Build the Dragon fight to excellence first.*
