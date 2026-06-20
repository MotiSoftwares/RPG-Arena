# MISSION_PLAN.md — Building *Arena of the Algorithms* to its fullest

> Companion to `CLAUDE.md` (the spec). This is the **execution plan**: scope, scale, architecture decisions, sequencing, tooling, QA, and the "and more" stretch goals. Tracked on GitHub as epics [#1–#8](https://github.com/MotiSoftwares/RPG-Arena/issues).

## 0. What "to its fullest / largest scale" means here
Not the Dragon-only minimum — the **complete three-boss game** with every system, all four classes, full juice/audio/narrative/cheats, a real automated test suite, a shipped Windows build, and **stretch content beyond CLAUDE.md** (§13 below). The bar is the 100% column of every rubric row in §2 **plus the full +35 bonus**, then extra polish and content on top.

## 1. Scale in concrete numbers (the deliverable inventory)
| Dimension | Target |
|---|---|
| Scenes | 3 — `Boot` (bootstrap), `MainMenu`, `BattleArena` |
| Playable classes | **4** (Warrior/Mage/Thief/Archer), fully distinct kits |
| Hero abilities | **~26** `Ability` SOs (6–7 per class incl. stance/ultimate) |
| Bosses | **3** (Dragon, Black Mage, Evil Warrior), 2 phases each |
| Boss abilities | **~18** `Ability` SOs |
| Status effects | **~11** (trimmed, per CLAUDE.md Appendix E.3: `Burn/Poison/Oiled/Wet/Marked/Frozen/Defending/Stealth/Rage/Bless/Weaken/Blind`) |
| Elements | 6 (`Physical/Fire/Ice/Lightning/Holy/Dark`) + full matrix |
| AI strategies | 3 (`DragonCycleAI`, `ChaoticAI`, `AggressiveAI`) + optional Behavior Graph |
| Command types | ~10 (`Attack/MultiHit/Heal/Buff/Debuff/ApplyStatus/Stance/Defend/BossMove/Composite` + cheat commands) |
| SO event channels | ~20 (flow/results/status/stagger/boss/entity/meta) |
| C# scripts | ~70–90 across the assembly graph (§3) |
| ScriptableObject assets | ~90–110 (abilities, characters, bosses, statuses, elements, channels, configs) |
| Edit-mode tests | Damage formula, elemental matrix, stagger build/break, status synergies, turn order |
| Distinct assets (rubric ≥10) | 4 hero + 3 boss models, fire/ice/lightning/heal/impact VFX, 2–3 music tracks, ~20 SFX, ~30 ability icons + 4–7 portraits → **60+** |
| Counted mechanics (rubric ≥5) | **7** (FSM turns+economy, elemental matrix, MP/cooldowns, Stagger/Break, status synergies, stance switching, **informed-gamble RNG** — see CLAUDE.md Appendix E) |
| Run progression | **Roguelite boons** between bosses (heal + 1-of-3 boon pick; `BoonDefinition` SO + `BoonSystem` + Boon-Select UI) — Appendix E.2 |

## 2. Architecture (the four named patterns)
ScriptableObject **data** → **Command** objects (actions) → executed by a finite-**state-machine** `BattleManager` → enemy **Strategy** AI → all decoupled via SO **event channels** → presentation (UI/audio/juice/animation/Ink) reacts only to events. Compute logic (damage pipeline) kept pure for headless unit tests.

### Assembly graph (deliberate decision — note vs CLAUDE.md §3.5)
The §3.5 1:1 asmdef-per-namespace split is **cyclic** with the Appendix-A code (`Combat.Status` ↔ `Characters`, `Characters` ↔ `Combat`). To keep assemblies acyclic while preserving the fine-grained **namespaces** (`RPGArena.Combat`, `.Characters`, `.Combat.AI`, `.Combat.Status`, `.Combat.Commands`), assemblies are grouped by dependency direction:
```
RPGArena.Utilities        (base; pooling, tween/juice helpers, extensions)
RPGArena.Core             → Utilities            (EventChannel<T> base, BalanceConfig, RunState, GameBootstrap, SceneLoader)
RPGArena.Gameplay         → Core, Utilities       (Entity, Ability, DamagePipeline, BattleManager FSM, TurnSystem,
                                                    Status, AI, Commands, ElementType/Matrix, + typed event-channel subclasses)
RPGArena.UI               → Core, Gameplay, Utilities
RPGArena.Audio            → Core, Utilities
RPGArena.Narrative        → Core, Gameplay, Utilities   (Ink)
RPGArena.Cheats           → Core, Gameplay, Utilities   (dev-only)
RPGArena.Tests            → Core, Gameplay, Utilities   (edit-mode)
```
Cycle broken by keeping the **generic** `EventChannel<T>` in Core and the **typed** channels (carrying `DamageResult`/`Entity`) in Gameplay. Documented for the viva.

## 3. Execution roadmap (maps 1:1 to GitHub epics #1–#8)
Each milestone ends **compiling, committed to `main`, play/console-verifiable**. I follow the §18.1 loop: implement → compile → read console → test → commit → summarize.

- **M0 Skeleton** ([#1]) — assemblies, orthographic camera, MainMenu+BattleArena in Build Settings, event-channel base + channels, `BalanceConfig`/`ElementType`/`ElementMatrix`/`StatBlock`, guarded bootstrap+SceneLoader+stub Audio+RunState. Remove `com.unity.ai.assistant` (console spam).
- **M1 Core combat vs Dragon (primitives, Mage)** ([#2]) — Entity, Ability, Commands, pure DamagePipeline, BattleManager FSM, TurnSystem, Status+Stagger+Synergy, Dragon+DragonCycleAI, headless-logged fight, **edit-mode tests**. Proof: stagger during Charging Breath cancels Flame Breath.
- **M2 Four classes + party** ([#3]) — all CharacterDefinitions+abilities, party of 3, stance/attunement, MP/cooldowns, extra-turn economy, all four trios clear the Dragon.
- **M3 UI/HUD/menus/flow** ([#4]) — full menu set, character select, battle HUD (HP/MP/Stagger, turn order, action menu, target picker, combat log, status icons, telegraph banner), pause/gameover/victory, all event-driven.
- **M4 Assets/animation/lighting** ([#5]) — Tripo 3D (Dragon + hero auto-rig attempt; Mixamo fallback), Animator Controllers, primitive→model swap, VFX prefab spawning, URP materials + APV + post-Volume + Cinemachine, Pollinations icons/portraits.
- **M5 Audio+juice+narrative+cheats (Dragon slice complete)** ([#6]) — AudioMixer + ElevenLabs music/SFX, hit-stop/shake/floating-text/BREAK juice, Ink intro wired to combat state, dev Cheat Manager. **Banks the +35 bonus.**
- **M6 Other two bosses** ([#7]) — Black Mage+ChaoticAI, Evil Warrior+AggressiveAI, run flow, play-any-boss.
- **M7 Stabilize/document/build/submit** ([#8]) — sanity check, Windows build (Exit quits, cheats stripped), README + screenshots, board screenshots, demo video, final push.

> Safety net: a polished **Dragon-only** build (M0–M5) is already a full-marks submission; M6–M7 are additive. I never ship three broken bosses over one excellent one.

## 4. Tooling & automation strategy (what's automated vs. manual)
- **Code/scenes/prefabs/SO assets/wiring** → me, via filesystem + Unity MCP. Compile + `read_console` after every change.
- **SFX + music** → ElevenLabs MCP (free 10k credits) + `jsfxr` for UI blips. **2D icons/portraits** → `Tools/pollinations_image.mjs` (free) — Gemini-billing/fal optional quality upgrade.
- **3D** → Tripo MCP (text/image→3D → rig → retarget → FBX/GLB). Dragon first; heroes auto-rig attempt → **Mixamo manual fallback** (the one ~30-min human touch if needed).
- **Narrative** → Ink (`.ink` authored by me, auto-compiled).
- **PM** → GitHub Issues/epics; board view built in UI; cards move as work happens.
- **Tests** → Unity Test Framework edit-mode suite.
- **Irreducibly human**: Mixamo fallback download (maybe), and the **demo video** (§17.2). Everything else is automated.
- **Operational note**: Unity defers compiles until its Editor window has focus — I'll ask you to keep Unity focused (or alt-tab on request) during MCP-driven package/asset/compile steps so the bridge doesn't stall.

## 5. Quality, polish & game-feel plan
Juicy by design: hit-stop, Cinemachine Impulse shake, screen/material flash, pooled floating damage text (color-coded), "BREAK!" slow-mo + shockwave, telegraph banners + charge VFX, post-FX pulses on crit/break, smooth bar lerps + UI tweens, ambient embers/lava light, idle bob. All event-driven, pooled, no per-frame allocs, degrades gracefully if an asset is missing. One `JuiceConfig` SO for tuning.

## 6. Testing & QA plan
Edit-mode tests on the pure compute layer (damage/matrix/stagger/synergy/turn-order); per-milestone manual checklist (§16.2); the §16.3 submission sanity check before build; 60 FPS budget with pooling and no `Find`/`GetComponent` in hot paths. `main` always compiles.

## 7. Bonus plan (+35, all within the Dragon slice)
Cheat Manager (+5, dev-only, cheats-as-Commands), AI (+10, Strategy + optional Behavior Graph), Ink external framework (+10, choices read/write combat state), Creativity (+10, Stagger/synergies/stances/telegraph-cancel/juice).

## 8. Risk register (live)
| Risk | Mitigation | Status |
|---|---|---|
| Gemini free image tier = 0 | Pollinations free fallback wired | ✅ resolved |
| 3D rigged-humanoid autonomy | Tripo attempt → Mixamo fallback | planned |
| Unity defers compile until focused | Ask user to focus Unity on MCP steps | operating |
| asmdef cycles | Consolidated acyclic assembly graph | ✅ designed |
| Tripo credit exhaustion | User rotates keys / 3 spare accounts | planned |
| Console spam (Unity AI NoSubscription) | Remove `com.unity.ai.assistant` in M0 | queued |

## 9. Cadence & reporting
Small verifiable increments; compile + console-read each step; commit/push to `main` with area-prefixed messages; tag at milestones (`m1-core-combat`…); move GitHub cards as work happens; end each work session with changed/status/next.

## 10. "And more" — stretch goals beyond CLAUDE.md (added once the core game is excellent)
- **Score/Grade + run summary** (turns/deaths/time → letter grade), feeding progression.
- **Difficulty modifiers** (Story/Standard/Hard tuning via BalanceConfig profiles).
- **Bestiary / weakness codex** that fills in as you discover element profiles.
- **Endless / Boss-Rush time-attack** mode and a **New Game+** with remixed boss profiles.
- **Optional 4th secret boss** reusing the engine (e.g., a mirror "Algorithm Core").
- **Accessibility**: colorblind-safe weakness labels, text scaling, screen-shake toggle, hit-stop toggle.
- **Extra synergies & a combo-counter** rewarding chained setups.
- **Behavior-Graph version** of one boss brain to literally tick the Unity-AI box.

---
*Build the Dragon fight to excellence first; everything else follows. Status is tracked live on the GitHub epics and in the working summaries.*
