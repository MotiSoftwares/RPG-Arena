# RPG Arena — Working Agreement & Overhaul Plan

Unity 3D turn-based boss-battler (URP). Party of 3 heroes (from Warrior/Mage/Thief/Archer) vs a boss roster (Dragon → BlackMage → EvilWarrior). Design contract: **spamming basics must LOSE** — you win via combos, Break/Fury venting, party Valor/Overdrive, or risky d20 specials. Each hero has exactly **5 skills (4 basic + 1 risky special)** — never add a 6th.

## How to validate (MCP for Unity)

- Instance: `set_active_instance("RPG Arena@71e9c1b1bb21d1f9")`. `execute_code` is **codedom C#6**: fully-qualify types, no `using` in body, `UnityEngine.Object` (ambiguous otherwise).
- **Logic** = headless: `BattleManager.RunToCompletion(ctx)` with live SO assets (see `PartyTrioTests`), read `ctx.log`. Live coroutine play STALLS when the editor is unfocused.
- **Visuals** = play mode + `EditorApplication.Step()` loop + `ScreenCapture.CaptureScreenshot`; drive turns via `BattleController.SubmitAction(ability, target)` (reflection) when `AwaitingInput`.
- **Balance gate**: after ANY change to combat logic/data, run EditMode tests. `PartyTrioTests.All_Four_Trios_Can_Clear_The_Dragon` must pass AND `SpamLosesTests` must still lose. `BossRosterTests` covers all 3 bosses.
- After script edits: refresh, wait for compile, `read_console(types=["error"])`.

## Architecture map (all under `Assets/_Project/Scripts/`)

- **Logic (test-covered, presentation-free)**: `BattleManager` (headless loop), `DamagePipeline`, `SynergyResolver` (Wet→Freeze, Frozen+Phys=Shatter ×2.3, Marked+Frozen=Brittle), `StaggerSystem` (Break vents Fury), `ChargeSystem` (Valor/Overdrive), `TurnSystem`, `Entity`, `Ability` (SO), `BalanceConfig` (SO with ALL tuning constants).
- **Live game**: `BattleController` (scene `BattleSystem` object; coroutine `RunBattle`; spawns visuals via `BattleSpawner` + `AttachBody`; static `PresentationBusyUntil` gates pacing) → mirrors invariants of `BattleManager` — **change both or tests drift**.
- **Presentation**: `JuiceController` (floating numbers, VFX spawn, screen shake, camera — caches `Camera.main` at Awake and **rewrites camera pos every LateUpdate**; any camera work must update its `camBasePos/camBaseFov`), `AnimationDriver` (trigger names: Attack/AreaAttack/Cast/Hit/Die/Victory), `CombatantMotion` (dash), `BattleHUD` (code-built TMP UI; fonts wired in scene: Poppins-Bold SDF + RUBIK-MEDIUM SDF from SlimUI), `BattleAudio` + `AudioManager` (`Assets/_Project/Audio/GameMixer.mixer`, exposed MasterVolume/MusicVolume/SFXVolume).
- **Meta**: `RunState` (in-memory run: bossesCleared, party, boons, currentBossIndex; roster hardcoded `Dragon, BlackMage, EvilWarrior`), `RunFlow` (victory→boon pick→next boss), `GameBootstrap`, `NarrativeRunner : IBattleIntro` (Ink).
- **AI**: `AIBehavior : ScriptableObject` strategy (`DecideAction`) — already pluggable; brains: DragonCycleAI, AggressiveAI, ChaoticAI, SimpleHeroAI. New AI = new SO subclass + asset.
- **Events**: SO channels in `Core/Events/` + instances in `ScriptableObjects/Events/` (OnBattleStarted/Won/Lost, OnTurnStarted/Ended, OnEntityDied, OnStaggerBroken, OnDamageDealt, OnBossTelegraph). Raisers are null-safe so headless tests stay silent — preserve that.

## Content & asset map

- Hero rigs (Mixamo, animation-in-FBX): Warrior=`Pro Sword and Shield Pack/Paladin WProp J Nordstrom.fbx` (has `Sword_joint`/`Shield_joint` grip sockets), Mage=`Pro Magic Pack/Ch39_nonPBR.fbx`, Thief/Archer=`Pro Longbow Pack/Arissa.fbx`, spare=`Assassin Pack/Vampire A Lusth.fbx`, full-kit wizard=`WizardPolyArt` (own controller + staff meshes).
- Dragon: `FourEvilDragonsPBR` (SoulEater has Fireball Shoot/Tail Attack/Scream/Get Hit/Die/fly set). Controllers in `_Project/Art/Rigs/Controllers/` (`*_Battle`), prefabs in `_Project/Art/Rigs/Prefabs/`.
- VFX: **ErbGameArt "Effects normal/" prefabs are URP-safe** (Fireball, Ice arrow, Magic arrow, Spears rain, Healing buff…). **Hovl Magic effects pack materials are broken** (missing shader GUID `0406db5a…` + built-in Standard mats) — fix shaders before using. Avoid Erb "Effects with projectors/" (built-in-RP Projector).
- Unused-but-good: `URP GanzSe Free Weapons Pack` (URP weapon meshes), Hovl `MoonSword` slash arcs.
- Audio: `_Project/Audio/{SFX,Music}`, GameMixer. UI: TMP everywhere (essentials imported); if TMP renders blank, TMP essentials are missing.

## Known root causes (fixed/being fixed in overhaul — verify before re-diagnosing)

1. Weapon grips: props parented to hand bones at identity offset, no grip system (fix: sockets + per-weapon offsets in prefabs).
2. Projectiles: VFX spawned AT TARGET with `Quaternion.identity` (JuiceController damage path + `PlayNonDamagingFx`); directional Erb prefabs fly a fixed world direction from inside the target (fix: spawn at caster, fly to target).
3. Boss scale: `targetModelHeight = 9.5f` in `BattleController.AttachBody` ⇒ ~3.8× hero height and camera-clipping (fix: ~5 and reframe).
4. Movement: linear lerp dash, no run clips in controllers, Attack trigger fires at dash start; facing = 50/50 slerp between foe and camera.
5. Timing: all impact beats are hardcoded guesses (0.38s etc.) — replace with AnimationEvents → event channels.
6. HUD: `BattleHUD` reference canvas 1280×720 (others 1920×1080) ⇒ giant UI; empty combat-log panel shows at battle start.

## Overhaul plan (status)

- [x] Recon + this file
- [x] **P1 Combat visuals**: boss 9.5→5.0 + reframe; bolt-on weapons removed (native Paladin gear, socketed Mage staff, Arissa bow hidden); real projectiles (`ProjectileFlight` — Erb prefabs are SELF-PROPELLED: spawn aimed at target, lifetime=dist/speed so the death-explosion lands ON the target); eased melee dash + Run animator states + swing-on-arrival; 31 ability vfx remapped; FourEvilDragonsPBR mats → URP.
- [x] **P3 UI**: all canvases 1920×1080; log hidden till content; menu fits Items row; JuiceCanvas scaler (popups were raw pixels).
- [x] **P4 Features**: gold + Supply Camp shop + in-battle items (`ItemDefinition` wraps an Ability; `flatPower` for stat-free item heals); minions (`MinionDefinition` + `SequenceAI` rotation brain + adds-first targeting + gold/item drops — LIVE battles only, headless tests stay trio-vs-boss); audio mixer "Paused" snapshot (620Hz lowpass, authored via internal AudioMixerController reflection) + elemental hit SFX (ElevenLabs) + dragon_roar telegraph; cinematic intro (`BattleIntroCinematic : IBattleIntro`, drives camera via `JuiceController.SetCameraBase` — NEVER move the camera directly, JuiceController stomps it every LateUpdate; HUD auto-hides during intro).
- [x] **P4.5 Playability (July 28 pass 2)**: ACTION COMMANDS — timed-strike needle bar on damaging skills (PERFECT ×1.18 via `ActionRequest.timingMult`→basePower; 0 reads as 1 so headless is bit-identical) + BRACE reaction window on enemy blows (×0.7, `BattleController.braceWindow`, HUD polls `BraceWindowOpen`/`SubmitBrace`); combo-ready `>>FREEZE!/>>SHATTER` tags + gold card edges in previews (`BattleHUD.ComboTag`). Animation depth: dragon TailAttack on AoE + Roar(Scream) on telegraph/intro, Hit states for Mage/Thief/Archer, Thief melee punch/kick (was bow-mime), Warrior+Mage 3-clip AttackVariant blend trees. The Black Mage = WizardPolyArt hooded wizard (staff on the pack's animated Weapon bone) at per-boss `BossDefinition.modelHeight` 3.4u.
- [ ] **P2 Animation events**: infrastructure half-done (dash arrival callback + projectile arrival ARE event-driven); remaining: AnimationEvents on FBX clips (`ModelImporterClipAnimation.events`, normalized time) to replace the 0.35s swing-connect + 0.45s cast-release constants in JuiceController.AttackBeat.
- [ ] **P5 Premium art (ComfyUI)**: menu/loading art, boss portraits, logo. Also: Archer needs a GanzSe bow with a proper grip socket (she's currently unarmed-mime); EvilWarrior still wears the hero Warrior model (Assassin Pack mutant is the candidate); TargetRule.Summon unimplemented; HUD target picker for minion-vs-boss; ambient forest audio loop.
- Gotcha: `SceneManager.LoadScene` from `execute_code` only applies if you Step a few frames IN THE SAME call right after it; PowerShell `-replace`/`Set-Content` mojibakes UTF-8 C# files (repair: read UTF8 → encode 1252 → decode UTF8) — use .NET File IO or the Edit tool.
- [x] Final (this pass): 29/29 EditMode green after every feature; validated visually via Step+screenshot loop.

## Simulation-driven findings (July 28 pass 3/4) — measure, don't guess

Run N headless fights and aggregate the log (build a `BattleContext` like `PartyTrioTests`,
`RunToCompletion`, then parse `ctx.log`). This found what code-reading missed — and all of it is FIXED:

- ~~Combos never fire~~ **FIXED.** `SimpleHeroAI` scored by raw power, so setup skills scored 0 and the
  combo web was decorative in every balance test. It now plays the intended loop (lay a setup →
  detonate with the paying element). Synergies per 8 fights: 0 → 89/109/64 by trio.
- ~~23.6% of attacks whiff~~ **FIXED** via glancing blows (`BalanceConfig.glanceDamageMult 0.35`,
  `cleanMissOvershoot 0.55`): a failed roll grazes instead of evaporating. Dead turns 23.6% → 10.4%.
- Because combos now actually fire, the party melted the old 1700 HP dragon in 4 rounds → **HP 2600**
  (swept 4 trios × 9 seeds; fights now run 6–10 rounds).
- **The gate test is a single seed (99).** Changing the AI's RNG consumption reshuffles that fight
  entirely — a trio can win 8/8 on other seeds and still fail the gate. **Sweep trios × seeds before
  tuning**, and prefer a lever with a real design reason over one that only fixes seed 99
  (`staggerBuildBreakSkill 30→36` was chosen precisely because Break skills counter the new charge).
- ~~W+M+A can never combo~~ **FIXED**: Ice Lance only Freezes a WET target and that trio had no Wet
  source, so Freeze/Shatter/Brittle were unreachable. **Blizzard now applies Wet**, giving the Mage a
  self-contained Blizzard→IceLance(FREEZE)→physical(SHATTER) line. 0 → 69 synergies per 8 fights.
  Dragon HP settled at **2500** (4 trios × 11 seeds, all 10/10, fights 5–10 rounds).
- Still uneven: damage skews Mage-heavy; the no-Mage W+T+A trio only ever gets Wet+Physical (no Ice
  to Freeze with) and is the slowest clear at ~9 rounds.

## Staging rule (learned the hard way)

**Keep the fight axis LATERAL.** Heroes rotate to face their target, so if the boss sits much deeper
in Z than the party, "face the boss" literally means "turn your back to the player" — that is exactly
what a nice-looking receding diagonal caused. Keep every combatant within ~1.5u of the same depth,
then `AttachBody`'s `camBlend` (~0.34) yaws the profile into a 3/4 front view. Verify numerically:
`Vector3.Dot(body.forward, (cam.position - body.position).normalized)` should be **positive** for
every combatant (>0.15 = front visible; negative = back turned).

## Hard-won gotchas (verify before re-deriving)

- **Animation clips MUST be Humanoid.** 13 clips wired into the battle controllers were Generic rigs on Humanoid characters — Unity can't retarget those, so they silently never played (the "weird/unnatural animation" complaint). Any new clip: `animationType = Human`, `avatarSetup = CopyFromOther`, `sourceAvatar` = that pack's own model avatar. Verify with `read_console` for "not humanoid".
- **Vendor prefabs have no AnimationDriver** — `AttachBody` now adds one if missing. Without it a boss/minion stands frozen all fight.
- **Pause ownership**: `Core.GamePause` is the single authority (Core so Gameplay+UI can both read it; UI→Gameplay is a one-way dependency). `Time.timeScale == 0` does NOT mean paused — hit-stop parks it at 0 too. Anything real-time (timed-strike bar, BRACE) must check `GamePause.IsPaused`, and JuiceController's watchdog must never force-restore a deliberate pause.
- **Unfocused-editor stall root cause**: `WaitForSecondsRealtime` never resumes under `EditorApplication.Step()` with the editor unfocused → hit-stop parked `Time.timeScale` at 0 forever. Fixed: TimeEffect integrates `unscaledDeltaTime` manually + a LateUpdate watchdog force-restores after 1.5s frozen. Don't reintroduce realtime waits in presentation.
- ComfyUI and Unity must NOT run at once on this 32GB machine — ComfyUI keeps ~13-20GB of model weights pinned in RAM after a render, which pages Unity to death (looks like a 5-FPS editor). Batch-generate, quit ComfyUI fully, then return to Unity.
- The MCP `execute_code` frame-stepping loop spams "PlayerLoop called recursively" errors in the console — that's the automation, not a game bug.
- Burst screenshots inside one `execute_code` call flush the same frame — capture ONE screenshot per call (step ~5 after) for sequences.
- Direct-play BattleArena has NO GameBootstrap/RunState (items/gold hidden). Test via an INACTIVE GameObject + AddComponent + reflection-set `Instance`/`Run` (Awake never fires on inactive).
- Erb "projectile" prefabs: root ParticleSystem startSpeed 15, world-space, sub-emitters explode on particle death. Never spawn at identity/origin; configure BEFORE first sim frame (same call as Instantiate).

## Guardrails

- Don't regenerate rig prefabs blindly — `CharacterRigBuilder` overwrites controllers + `*_URP.mat`.
- `MainMenuController` is obsolete dead code — build on `MainMenuUI`.
- `BattleSpawner`/`BattleManager` are NOT scene components — never add them to the scene.
- Presentation must never block headless logic; keep channel raisers null-safe.
- Commit style: `area: what` (see git log); re-run tests before committing balance-touching work.
