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
- Unused-but-good: `URP GanzSe Free Weapons Pack` (URP weapon meshes).
- **The ENTIRE `Assets/Hovl Studio` tree is unusable, MoonSword included** (verified July 30): all **45** materials — 38 in `Magic effects pack/Materials`, 7 in `MoonSword/Materials` — reference shader guids `0406db5a14f94604a8c57ccfbc9f3b46` or `933532a4fcc9baf4fa0491de14d08ed7`, and **neither shader asset exists anywhere under `Assets/`** (the packs ship no `.shader` files at all). Every one of those prefabs draws the magenta error shader. 24 live abilities plus the three `JuiceController` slash-arc fields were pointing into it. `VfxAssigner.VfxRoots` is now Erb-only so the assigner cannot reintroduce them; there is **no** slash-arc substitute in ErbGameArt, so melee falls through to the code-built `SpawnVFX` burst until someone authors the missing shaders.
- Audio: `_Project/Audio/{SFX,Music}`, GameMixer. UI: TMP everywhere (essentials imported); if TMP renders blank, TMP essentials are missing.
- **The AudioListener lives on `GameBootstrap`, not on a camera** (fixed July 30). NONE of the three
  scenes had one, so the mixer, both clip banks and every `PlaySfx`/`PlayMusic` call had been playing
  into a void for the entire project — audio looked wired and was inaudible. It belongs on the
  persistent bootstrap: that object survives every scene load, so there is always exactly one and
  never a "2 audio listeners" fight. Position is irrelevant (SFX pool + music source are 2D).
  `menu.mp3` / `battle.mp3` are still 5.1-SECOND stubs — they play, but they loop far too fast.
- **HUD glyphs are Latin-only.** The SlimUI SDF atlases carry almost nothing decorative: `Poppins-Bold`
  has only `—`, `RUBIK-MEDIUM` adds `↕ ← » • × –`. Everything else (`★ ◆ ◇ ⚔ ✖ ⚠ ⏳ ✓ ▲`) renders as a
  **tofu box** — the boss-telegraph warning and the BROKEN banner shipped that way for a while before
  anyone looked closely. Use `»` for menu markers and `!!` for warnings. Check with
  `fontAsset.HasCharacter(ch)` before adding any symbol, and sweep for regressions by walking live
  `TextMeshProUGUI` objects asserting `txt.font.HasCharacter(ch)` for every non-ASCII char.
- **`Ellipsis`/`Truncate` DELETE a line that is too tall for its rect — they do not clip it.** You get
  `textInfo.characterCount == 0`, no warning, no tofu, just nothing. **Every ability name in the game
  was invisible this way** (July 30): the band was 21px while Poppins-Bold at 16pt needs 23, because
  its face is `lineHeight/pointSize = 96/64 = 1.5×` the point size — so the skill menu showed five
  icons and five damage bands and not one move name. Rubik is ~1.25×. Budget `fontSize × ratio + 3px`
  for any single-line Ellipsis label, and remember **`Overflow` is immune** (it spills instead), which
  is why the neighbouring cost/detail labels rendered fine and hid the bug.
- **Sweep for BOTH text failures together** — same walk, and it is how the above was proven fixed:
  for every active `TextMeshProUGUI` with non-empty `text`, call `ForceMeshUpdate()` then assert
  `textInfo.characterCount > 0` (dropped line), assert `rect.height - fontSize×faceRatio >= 1.5`
  when overflow kills (fragile band), and assert `font.HasCharacter(ch)` per non-ASCII char (tofu).
  Run it once per distinct menu state — the skill menu and the item menu build different rows.

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
- [x] **P7a HOLD — SUPERSEDED (July 29 pass 3)** by the phase loop below: the player now picks WHO acts each turn, which subsumes hold entirely. `SubmitHold/CanHold/heldThisRound` are gone.
- [x] **P8 THE PHASE LOOP (July 29 pass 3, user playtest feedback)**: the live round is now PLAYER PHASE (pick any hero → act; each once; last auto-selects; `AwaitingHeroPick`/`SelectHero`/`CancelHeroSelection`, hero DoTs tick ONCE at phase start so pick-cancel can't double-tick) then ENEMY PHASE (minions first, boss last — climax ordering). **Headless `BattleManager` keeps speed order** — the gate measures the fight without the player's ordering advantage; HARD difficulty prices that in. No silent skips anywhere: a Frozen/Broken boss gets a centre-screen banner + PlayHit reel instead of vanishing; every action is narrated by the announcer (`StringChannel OnAnnouncement`, `BattleController.Announce/AnnounceAction` → `BattleHUD` centre caption). **Clean misses are GONE** (`cleanMissOvershoot = 1.0` in the asset): the worst roll is a 35% GRAZE, so no turn ever reads as "nothing happened".
- [x] **P8a Dragon rework**: kit = Claw Swipe / Wing Buffet / Flame Breath (3 attacks) + Terrifying Roar (party-wide Weaken −8 ATK, Death-magic-circle VFX) + Charging Breath, which now **lashes mid-charge** (pow 0.35 SingleEnemy; `BossMoveCommand` deals the ability's power when telegraphing) so the charge turn still attacks. Rotation STALKING claw→wing→roar→charge, shifts at 50%/15% HP. Tail Sweep/Tail Guard are out of the roster (AI fields kept as legacy so assets deserialize). **4 whelps** (death anims verified — Whelp_Battle has a one-shot Die clip). Weaken.asset now has real teeth (attackMod −8) — this also strengthens the Black Mage's Weakening Hex.
- [x] **P8b Action commands v2**: timing bar sweeps OUT-then-BACK (0.8s each way, PERFECT off<0.05, GOOD off<0.18); BRACE is a timed skill — a fast ping-pong needle at random speed/phase, press on gold = ×0.55, near = ×0.78, bad press = FUMBLED (full damage), no press = full damage (`SubmitBrace(float mult)`); new **FOLLOW-UP strike** — after a landed single-target hero blow, 45% chance a PRESS! prompt appears after a random 0.25–0.6s delay with a 0.30s window → bonus echo hit at 35% power through the real pipeline (forceHit, no statuses/risk; presentation rng only, logic stream untouched).
- [x] **P8c Difficulty**: `RunState.difficulty` (Core enum, **HARD default**), toggle on the character-select screen. Applied in `ApplyDifficulty` to spawned runtime stats only: HARD enemies ×1.15 ATK ×1.10 HP ("combo or die" — a no-heal/no-block script wipes by round 5, which is intended); EASY enemies ×0.70 ATK ×0.85 HP, heroes +25% HP. Headless/tests never see it (no GameBootstrap ⇒ vanilla).
- [x] **P8d Stage & backdrop**: dragon `modelHeight` 7.5 (1.5×), boss x=5.1, heroes shifted left (front row −2.05), camBlend 0.48 (faces visible, dots 0.67–0.82), 4-whelp skirmish line at two alternating depths, camera (0.55, 4.35, −14.9) fov 46, HUD reference resolution 1664×936 (~15% bigger UI). `SkyLife.cs`: circling SoulEater sky-dragon (own `SkyDragon.controller`, Fly Glide looped), 4 breathing god-rays, 3 wrapping fog banks — all SpriteRenderer-based (the URP `_Surface` runtime-flip gotcha). Camera focus punch-in on every turn start lives in `JuiceController.FocusOn` (composed inside the LateUpdate stomp, unscaled).
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

## Control needs diminishing returns (July 30) — the perma-freeze lockout

An adversarial audit found the FROST line was a **win button**, not a combo: Frost Touch has **no
cooldown**, costs 8 MP against 4 MP/turn regen plus an 8 MP refund on the free basic (MP-positive),
and Frozen lasts 2 of the victim's turns — so re-freezing every round locked a boss out of the
**entire fight**. The phase rewrite made it worse by guaranteeing Wet→Freeze inside one player phase.
No boss counterplay survives it: Devour needs a turn, Fury never lands a hit, stagger decay is
out-paced by SHATTER, and the two later bosses have no adds, so those fights became zero-damage
autowins.

Fix: `Entity.controlLockTurns` + `BalanceConfig.controlLockTurns` (4). Landing a turn-skipping status
starts a thaw cooldown that ticks on the **victim's own** turns and outlasts the freeze, so Freeze is
a 2-turn stun on a 4-turn cycle (~50% uptime) that costs 2 hero actions per cycle. Gated in
`AbilityCommands.ApplyStatuses`, so both loops share one implementation. Measured: the maximal lock
loop took the boss from **0% to ~40% of turns taken**; gate stayed green (Dragon 48/48 @6.0r,
BlackMage 48/48 @4.8r, EvilWarrior 46/48 @5.5r, 12 seeds × 4 trios).

## Difficulty must scale DAMAGE, not baseAttack (July 30)

`ApplyDifficulty` scaled `stats.baseAttack`, which was nearly a no-op: derived `Attack = baseAttack +
primary*k1` with k1=2, and the primary term dominates every enemy block — Hard's advertised **×1.15
landed as +3.3%** (Dragon) / +4.7% (Evil Warrior). Worse, the **Black Mage deals MAGIC damage from
`baseMagicAttack` with `baseAttack: 0`**, so he was *completely immune to the difficulty setting on
both settings*. Now `Entity.difficultyDamageMult` is multiplied in `DamagePipeline` step 2, hitting
physical and magic uniformly. It is a **separate field from `damageOutMultiplier` on purpose** —
`CheckPhaseTransition` overwrites that one on enrage, which would erase the difficulty.

## Other audit fixes worth not re-breaking

- **Defending was a no-op.** `durationTurns: 1` on a status applied during the caster's own turn is
  stripped by that same turn's end-of-turn tick, strictly before the enemy phase it exists to
  survive — the ×0.5 mitigation could never apply. Now 2.
- **The follow-up echo built FULL stagger.** Stagger build is independent of `basePower`, so a
  35%-power echo banked a whole hit's Break progress (and being `forceHit` it dodged the graze
  reduction too). Now scaled by `followUpPowerFraction`.
- **`>>FREEZE!` lied.** The tag fired on Wet+Ice regardless of whether the ability *carries* a Frozen
  status — Magic Bolt (free, Ice by default attunement, no statuses) and Blizzard (applies Wet)
  both advertised the marquee combo and delivered nothing. Gate on `CarriesControl(ability)`.
- **Hit% showed 100% for the whole Reliable tier.** Reliable is just a high base (0.85) clamped to a
  0.90 ceiling, not a guarantee. `EstimateHit` now mirrors the pipeline; only `autoHit` returns 1.
- **AggressiveAI ignored Taunting** despite its own comment claiming it rewards Guardian Taunt — so
  the tank's aggro tool did nothing in the one fight built around executing your weakest hero. It now
  restricts to taunters like the other two brains, and implements `PreviewIntent` so its
  *deterministic* Execute branch stops previewing as "??? unpredictable".
- **Stagger decay was invisible** — `Entity.DecayStagger` only wrote to `ctx.Log` (the headless
  trace). The Evil Warrior's whole identity read as a buggy meter. Now announced on screen.

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
- Direct-play now **self-bootstraps** (July 30): `GameBootstrap.EnsureRuntime()` — called from `MainMenuUI.Awake` and `BattleController.Awake` — spawns `Resources/GameBootstrap.prefab` when `Instance` is null, so pressing Play on MainMenu or BattleArena gets audio, `RunState`, gold and items. The prefab IS the Boot scene's object (`SaveAsPrefabAssetAndConnect`), so the AudioManager wiring can't drift. A fallback spawn sets the static `spawningFallback` flag, which is how `Awake` knows to SKIP `Scenes.LoadScene(firstSceneName)` — without that it would bounce you straight back to the menu. The fallback run is seeded with 250 gold + 2 extra items; `Reset()` at character select wipes that, so a real run is never affected. (Old note, now obsolete: direct-play had no bootstrap and items/gold were hidden.)
- Erb "projectile" prefabs: root ParticleSystem startSpeed 15, world-space, sub-emitters explode on particle death. Never spawn at identity/origin; configure BEFORE first sim frame (same call as Instantiate).

## Capturing footage from inside Unity (Aug 16 — how the submission video was made)

- **Never screen-grab the window.** The Game view is GPU-composited and Windows' `gdigrab` BitBlt path
  returns black. `ScreenCapture.CaptureScreenshotAsTexture()` in a `WaitForEndOfFrame` coroutine reads
  the real framebuffer, so it also picks up the Screen Space **Overlay** HUD that a `camera.Render()`
  into a RenderTexture silently misses. The frames contain **zero editor chrome** — they are
  indistinguishable from the shipped exe, so there is no reason to capture from a build.
- **`Time.captureFramerate = 30`** pins BOTH `deltaTime` and `unscaledDeltaTime` to 1/30, so the take
  plays back at true speed even though JPEG encoding drags the editor to ~3.5 real fps. Essential
  here because the timing bars and BRACE run on unscaled time.
- **Audio: `UnityEngine.AudioRenderer`** (`Start` / `GetSampleCountForCaptureFrame` / `Render`) is
  Unity's offline audio path — the one Unity Recorder uses. It hands you exactly the samples belonging
  to each captured frame, so A/V stay locked no matter how far below real time the capture runs.
  Capturing the audio thread's real-time output instead desyncs within seconds. **Drain it EVERY
  frame** — skip one and the next call returns a bigger block and the track drifts permanently.
  Measured: 13.12s of WAV against 13.13s of video. Write 32-bit float WAV (fmt tag 3) and patch the
  header sizes on close.
- **Batchmode `-executeMethod` builds fail while the Editor has the project open** (exit 1, log stops
  at "Successfully changed project path"). Use the MCP `manage_build` tool to build inside the running
  Editor instead. Also: `BuildScript.BuildWindows` calls `EditorApplication.Exit(1)` on failure, so do
  NOT invoke it from `execute_code` — it would close the editor.
- Driving the HUD for footage: call the HUD's private `StartTimingBar(hero, ab, target)` by reflection
  rather than `BattleController.SubmitAction` — SubmitAction skips `TimingSequenceRoutine`, so the
  needle bar never appears on camera. Lock a bar by reading the `Needle` RectTransform's
  `anchorMin.x` (that IS the value the scoring reads) rather than dead-reckoning against sweep time.
  **The last hero of a phase auto-selects**, so waiting on the hero-pick panel alone spins until
  timeout and silently drops that hero's turn.

## `RunFlow.Text()` sets `pivot = anchor` — boxes are NOT centred on their anchor

A box's extent is `(anchorY × parentH)` with `(anchorY × boxH)` *below* that point, i.e. it grows
upward from the anchor. Every boon card was laid out as if the anchor were the centre, putting the
name box at 212..262 in card space and the description box's top at 241 — and because the description
is `UpperCenter`, its first line rendered straight through the boon's own name. **All three cards in
every draft were unreadable**, and it was invisible in play because nobody stops on the victory screen
long enough to read it; it only showed up on a frozen 1080p capture frame. Card is 300 tall; the
working layout is banner 267..289 / name 220..264 / description 39..209.

## Open: a flat-pink disc during the Black Mage and Evil Warrior fights

~2s, flat `(254,1,216)`, hard-edged, no falloff, no shading. **Not** a missing shader. Ruled out, all
verified: no material asset with a null/unsupported shader; no renderer with an empty material slot
(the 153 "empty" slots in ErbGameArt are all slot **1** on ParticleSystemRenderers — the unused trail
material slot, harmless); no ability referencing the built-in-RP `Effects with projectors/` prefabs;
and a watcher scanning every renderer every frame through both fights never caught the error shader
once. The Dragon fight is clean, which points at VFX unique to the later two bosses (`Glowing orbs` /
`Magic buff`), but those use existing supported ERB shaders with white tint and real textures. Still
unidentified — do not "fix" it by guessing.

## Running Unity in BATCHMODE rewrites ProjectSettings behind your back

Two different edits were observed after `-batchmode -executeMethod` / `-runTests` runs, neither
intended and neither announced:

- **`preloadedAssets` emptied.** It held `InputSystem_Actions.inputactions`, which Unity preloads so
  the Input System works in a player. Committing that would have shipped a build whose input could
  fail, and nothing in the console says a word about it.
- **A scripting define dropped** (`SENTIS_ANALYTICS_ENABLED`), because the package's editor hook does
  not initialise in batchmode. Harmless churn, same family as commit 47ef53b.

**Always `git diff ProjectSettings/ProjectSettings.asset` after any batchmode Unity run** and revert
what you did not intend. Note the editor's own `manage_build` path did NOT do this -- only batchmode.

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
