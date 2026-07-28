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
- [x] **P6 Game design (July 29)**: enemy INTENT preview (`AIBehavior.PreviewIntent`, must be side-effect free — no rng draws, no cycle-index writes, or the preview lies); OVERDRIVE as a choice (Surge/Sunder/Rally); combo web reworked into 3 lines + exclusive ladder; per-boss identities (Devour / stagger decay); rule-changing boons + run attrition (see the two sections below). 41/41 EditMode.
- [x] **P7a HOLD — turn order becomes playable (July 29)**: `BattleController.SubmitHold()` moves the actor to the end of the round and re-runs the slot (`orderIndex--` then `continue`, since the for-loop increment still fires). The whole game is setup→detonate but who acted first was a pure initiative roll, so a round where the Warrior moved before the Mage's Blizzard simply could not SHATTER. Guards: once per hero per round via `heldThisRound` (two heroes could otherwise hold for each other forever), only offered when someone is still scheduled behind you (`CanHold`), and the start-of-turn tick is SKIPPED on the second visit or a held hero eats a second stack of every DoT. Player-only/live-only, like items and Overdrive — headless stays untouched.
- [x] **P7b THE STAKE — push-your-luck on the SPECIAL (July 29)**: a risk-die skill now asks STEADY / PRESS / ALL IN before the timing bar (`RiskStake` on `ActionRequest`; `Press == 0 ==` default, so AI and headless are bit-identical). One symmetric knob drives it — `band' = 1 + (band-1) × scale` — so a stake that fattens the jackpot fattens the backfire by exactly as much and can never be free. STEADY also floors the roll above the whiff threshold **and pays a flat `stakeSteadyDamageMult` tax**: measured over the full d20, removing the bad faces is itself worth ~3% EV, so without the tax the safe option had both the best average *and* the lowest variance and PRESS was strictly dominated. Final spread (avg / floor / ceiling / recoil): STEADY 76/68/153/0 · PRESS 86/40/240/30 · ALL IN 89/20/300/42 — every measure monotonic in risk, which is the test `No_Stake_Dominates_Another` enforces.
- [x] **P7c Positioning** is fully playable: row swap in the action menu + `backRowMeleeMult` mitigation in `DamagePipeline` step 4b.
- [x] **P2 Impact timing (July 29)**: the 0.35s swing-connect and 0.45s cast-release constants are gone. `AnimationDriver.contactFraction` (serialized per rig, default 0.42) + `TimeToContact()` read the clip **currently playing** and return the seconds left until its contact frame; `JuiceController.WaitForContact` waits on that, keeping the old constant only as the pre-transition value and as a ceiling so an interrupted state can never stall a turn. Melee is now two beats — dash, *then* ask the swing clip — because the swing does not start until the dash callback fires. Measured live: Mage cast contact 0.83s and Warrior swing 0.73s vs the old flat 0.35s (2–2.4× early), and the Warrior's blend tree rolls between 1.30/1.73/2.33s clips per swing, so no constant could ever have been right.
  - **Tried FBX AnimationEvents first and reverted them.** Authoring `ModelImporterClipAnimation.events` requires materialising `clipAnimations`, and on the FBX that had none configured that silently changed clip ranges (mutant swiping 2.43→2.99s, overdraw 3.60→9.93s) and clamped event times to 100% of length. Runtime clip-length math gets the same result with zero import-setting risk. If you ever do go back to real events, revert the `.fbx.meta` files first and set event times from the *imported* `AnimationClip.length`, not from `(lastFrame-firstFrame)/30`.
- [x] **P5a Evil Warrior gets his own body (July 29)**: was the *identical* Paladin mesh + `Warrior` controller as the player's hero — mechanically distinct since the identity pass, visually a clone. Now the Assassin Pack mutant (`EvilWarriorMutant.prefab`, `EvilWarrior_Battle.controller`, modelHeight 3.9). All 13 pack FBX were **Generic/NoAvatar** — same import bug as the 13 hero clips — so nothing could ever retarget; reimported Human with `CopyFromOther` off the Vampire avatar, clips renamed off `mixamo.com`, locomotion looping and one-shots not. Clip map: swiping=Attack, jumping=AreaAttack, flexing=Roar+Victory, dying=Die, idle/run=locomotion. **No `Hit`/`Cast` parameter on purpose** — the set has no flinch clip and `AnimationDriver` capability-checks every call, so PlayHit no-ops and PlayCast falls back to PlayAttack.
- [ ] **P5 Premium art (ComfyUI)**: menu/loading art, boss portraits, logo. Also: TargetRule.Summon unimplemented; HUD target picker for minion-vs-boss; ambient forest audio loop. (Archer bow: NOT an issue — `arissa:Weapons_Geo` is active on her prefab and disabled on the Thief's, which is correct.)
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

## Combo web (reworked July 29) — three lines, one ladder

- **FROST** (needs a Mage): Wet → Ice = FREEZE → Physical = **SHATTER ×2.3**. The marquee line.
- **HUNT** (no Ice needed): Marked + Oiled → Physical = **QUARRY ×1.9**. The all-physical trio's own
  ceiling; deliberately under SHATTER because it needs no cross-school coordination.
- **COATING** (entry level): a single Wet *or* Oiled → Physical = ×1.15 +12 stagger.
- Sources: Wet = Thief Water Bomb / Mage Blizzard · Oiled = **Archer Pitch Arrow** (was the redundant
  Mark Target; Thief Shadow Mark already covered Marked) · Marked = Thief Shadow Mark.
- **Physical lines are an EXCLUSIVE LADDER — never make them additive again.** Physical is the element
  every class throws; stacked, a loaded target hit ~5.8× and fights collapsed to 3.6 rounds. Marked's
  crit and Brittle ride on top on purpose (Mark is an accuracy tool, not a damage line).
- Health check: all 4 trios should clear 12/12 seeds within a ~6-round band, each showing a DIFFERENT
  dominant synergy in the log.

## Boss identities (July 29) — three fights, not three HP pools

| Boss | Identity | Mechanic | Brain |
|---|---|---|---|
| Dragon | elemental **puzzle** | telegraphed charge you race to Break; Fury punishes ignoring the meter | DragonCycleAI |
| Black Mage | **anti-setup** | `DEVOURS` stockpiled Wet/Oiled/Marked off himself, heals + gains Fury (Frozen is deliberately inedible — the party's escape hatch) | DevourerAI |
| Evil Warrior | **anti-passivity** | `staggerDecayPerTurn` bleeds unconverted stagger each of his turns, so chip-and-turtle can never bank a Break | AggressiveAI |

Health check (32 fights each): Dragon 32/32 @5.9r, Black Mage 32/32 @4.6r, Evil Warrior 29/32 @5.9r,
each showing a **different signature** in the log (charges / DEVOURS / shake-offs). Hardest last.
Sizing gotcha: a boss whose HP is too low never gets to *use* its identity — the Black Mage died in
2.7 rounds at 850 HP and Devoured exactly zero times. Always confirm the mechanic FIRES before tuning it.

## The run layer (July 29) — boons change rules, wounds carry

**Rule boons.** `RunModifiers` on `BattleContext` (null in headless = vanilla, same contract as
`charge`). `BoonDefinition` has both stat deltas and rule fields; `BoonSystem.ApplyRules` is called
once per boon while `Apply` is called once per boon **per hero** — rules are per-run, stats are
per-hero. `RunFlow.PickThree` guarantees ≥1 rule card per draft and never re-offers an owned one.
Prefer authoring rule cards: a stat card makes the same fight easier, a rule card makes it a
different fight. Measured deltas (32 fights): Permafrost −0.6r vs Dragon, Lingering Break +3 wins vs
Evil Warrior, Tarpits +30% SHATTERs, Second Wind +3 wins / 13 saves vs Evil Warrior.

**Warcry can't be measured headlessly** — the headless loop never *spends* Overdrive, and a sweep
that leaves `ctx.charge` null makes `AwardFor` early-return, so the card reads as literally zero
effect. Verify it by valor EARNED per fight with a ChargeSystem attached (232 → 313), not by rounds.

**Attrition.** HP/MP carry between bosses as fractions in `RunState.carryHp/carryMp`, floored at
`CarryHpFloor 0.50` / `CarryMpFloor 0.60` so a run can never become unwinnable by arithmetic. A hero
who fell carries 0 and is clamped up to the floor. Grade seeds the next fight's Valor (S 30/A 20/B
10/C 0). The Supply Camp gained a REST whose price climbs (`120 + 60×n`) so it can't erase attrition.
**Retry rewinds to `SnapshotForRetry()`** (taken at fight setup, after the carry is applied) — without
that, attrition + retry is a death spiral where each attempt starts weaker than the failed one before.

## Staging rule (learned the hard way)

**Keep the fight axis LATERAL.** Heroes rotate to face their target, so if the boss sits much deeper
in Z than the party, "face the boss" literally means "turn your back to the player" — that is exactly
what a nice-looking receding diagonal caused. Keep every combatant within ~1.5u of the same depth,
then `AttachBody`'s `camBlend` (~0.34) yaws the profile into a 3/4 front view. Verify numerically:
`Vector3.Dot(body.forward, (cam.position - body.position).normalized)` should be **positive** for
every combatant (>0.15 = front visible; negative = back turned).

## ⚠ VERIFY COMPILATION BEFORE TRUSTING ANY TEST RESULT

`read_console` can return **zero entries while compilation is failing**. Unity then keeps serving the
last good DLL, so `run_tests` happily reports 29/29 green **against stale code** — I burned an hour
on this. Symptoms: play mode won't enter/stay, and edits appear to have no effect.

**The source of truth is `%LOCALAPPDATA%\Unity\Editor\Editor.log`** — grep it for `error CS`.
`Tools/compilecheck.ps1` does this properly and has been wrong in **both** directions, so trust the
current version rather than re-deriving: errors must be scoped to the text after the LAST
`[ScriptCompilation] Requested script compilation` marker (a fixed tail re-reports errors you already
fixed), and staleness must be checked **per .asmdef** against that assembly's own sources (comparing
every DLL to the globally-newest file flags Core as stale after a UI-only edit). A false alarm is as
corrosive as a miss — it teaches you to ignore the one signal that catches silent breakage.
Verified by dropping a deliberately-broken probe script in and confirming it is still caught. Note
that a *data* (.asset) change applies without recompiling, so "my asset edit worked" does NOT mean
the assembly is current. Compare `Library/ScriptAssemblies/RPGArena.*.dll` mtimes against the newest
`.cs`; string literals in a DLL are UTF-16, so grep with Unicode encoding, not UTF-8.
Helper: `scratchpad/compilecheck.ps1`. Two real errors this caught, both invisible in the console:
`goto case` targeting a `default:` label, and an unbalanced brace from an edit.

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

## Importing a new character pack (the two bugs that always bite)

1. **Mixamo FBX import as `Generic` / `NoAvatar`.** They look fine in the project view and simply never
   play — 13 hero clips and all 13 Assassin Pack files hit this. Fix: source model →
   `animationType = Human` + `avatarSetup = CreateFromThisModel`; every animation FBX →
   `Human` + `CopyFromOther` + `sourceAvatar` = that model's avatar. Also rename the clips (every
   Mixamo clip is called `mixamo.com`) and set `loopTime` per clip — **a looping death clip never ends.**
2. **Vendor materials ship with `_EMISSION` on and `_EmissionColor` white**, so the model renders as a
   blown-out white silhouette regardless of lighting (the dragon did this, the mutant did this).
   Fix: `DisableKeyword("_EMISSION")` + `_EmissionColor` black + `globalIlluminationFlags =
   EmissiveIsBlack`. Check `_BaseMap` too — packs with no textures at all (Assassin) need authored
   flat colour, which suits the low-poly direction anyway.
   **Material remapping caveat**: flipping `materialLocation` to `External` re-extracts under NEW
   names (`Vampire_MAT1` → `Vampire_diffuse`), silently orphaning any `AddRemap` you set against the
   old identifiers. Simplest reliable path: let it extract, then edit the extracted `.mat` in place.

## Guardrails

- Don't regenerate rig prefabs blindly — `CharacterRigBuilder` overwrites controllers + `*_URP.mat`.
  **The two bosses are no longer in its `Targets` list** and must not be re-added: they have
  hand-authored rigs (EvilWarriorMutant / BlackMageWizard), and `AssignToDef` writes
  `def.modelPrefab`, so one run of the menu item would silently re-point them at tinted hero
  lookalikes with no error and no visible diff until play mode.
- `MainMenuController` is obsolete dead code — build on `MainMenuUI`.
- `BattleSpawner`/`BattleManager` are NOT scene components — never add them to the scene.
- Presentation must never block headless logic; keep channel raisers null-safe.
- Commit style: `area: what` (see git log); re-run tests before committing balance-touching work.
