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
- [ ] **P1 Combat visuals**: boss scale+framing, weapon grip sockets, real projectile flight, run-in/out movement with easing, URP VFX visibility pass, dragon idle/facing
- [ ] **P2 Animation events**: contact-timed impacts via AnimationEvents + event bus; kill hardcoded waits
- [ ] **P3 UI**: one 1920×1080 reference, smaller panels, hide empty log, fix overflows, camera reposition (via JuiceController base fields)
- [ ] **P4 Features**: items+currency (RunState) + shop in RunFlow + in-battle Use Item command; minions (BattleContext list + TargetRule.Summon + drops); audio mixer snapshots + pause muffle; cinematic intro (IBattleIntro); particle material pass
- [ ] **P5 Premium art (ComfyUI)**: menu/loading art, boss portraits, logo, on-brand polish
- [ ] Final: tests green, console clean, screenshot gallery

## Guardrails

- Don't regenerate rig prefabs blindly — `CharacterRigBuilder` overwrites controllers + `*_URP.mat`.
- `MainMenuController` is obsolete dead code — build on `MainMenuUI`.
- `BattleSpawner`/`BattleManager` are NOT scene components — never add them to the scene.
- Presentation must never block headless logic; keep channel raisers null-safe.
- Commit style: `area: what` (see git log); re-run tests before committing balance-touching work.
