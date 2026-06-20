# Improvement Plan — CLAUDE.md review pass (from the 7-agent audit)

Status legend: [ ] todo · [~] in progress · [x] done · [defer] needs 3D models.
Implementing in impact/effort order, committing + engine-verifying each.

## THEME 1 — Combat depth & correctness (highest rubric leverage)
- [x] 1.1 Fix wrong-boss Stagger duration (resolve boss before BossStaggeredTurns) `S` BUG — 9eb864b
- [x] 1.2 Boss phases/enrage live (Entity.CheckPhaseTransition + damageOutMultiplier) `M` — 5d409fa
- [x] 1.3 DoT respects element (Burn HEALS the fire-absorbing Dragon) `M` BUG — 9eb864b
- [x] 1.4 Dragon Physical resist + Break strips armor (staggered ⇒ resist→neutral) `S` — 9eb864b
- [ ] 1.5 Party-buff duration round-scoped (not per-owner-turn) `M`
- [x] 1.6 Dark Sight truly untargetable (TargetingSystem excludes Stealthed) `S` — b4dd31e
- [ ] 1.7 Guardian Taunt redirect/cover `M`
- [ ] 1.8 Behavioral Warrior stances (lifesteal/execute, guardian cover) `M`
- [ ] 1.9 Magic Guard (MP-as-shield) + author Mage_MagicGuard `M`
- [x] 1.10 Frozen shatter bonus on physical (x1.6) `S` — b4dd31e
- [ ] 1.11 Wet+Lightning ⇒ Frozen reliably `S`
- [ ] 1.12 Conditional finisher payoffs (guaranteedCrit/bonusVsFlag/execute) `M`
- [ ] 1.13 Telegraphs for Black Mage & Evil Warrior `M`
- [ ] 1.14 Soul Arrow defense-ignore + Mage school-nuke follows attunement `S`
- [x] 1.15 Extra turns hero-only + logged `S` — 5d409fa
- [x] 1.16 Break window >=2 turns `S` — 5d409fa

## THEME 2 — UI / UX (make the depth visible)
- [x] 2.1 Status-effect badges on boss + party (colour + abbrev + turns) `M` — 259bbad
- [ ] 2.2 Turn-order tracker `M`
- [ ] 2.3 Manual target picker with highlighting `M`
- [ ] 2.4 Per-ability tooltips (+ absorb warning) `M`
- [ ] 2.5 Damage-range preview (fix README overclaim; absorb in hit%) `M`
- [ ] 2.6 Richer scrollable combat log `M`
- [x] 2.7 Numeric HP/MP bar labels + KO marker `S` — b1af537
- [ ] 2.8 Pause-menu Settings panel (shared SettingsPanel) `S`
- [ ] 2.9 Resolution + fullscreen settings `S`
- [ ] 2.10 Delete orphaned HUD result panel + defeat tip `S`
- [ ] 2.11 Character-select primary-stat + dynamic synergy hints `M`
- [ ] 2.12 Bar lerp+chip, stance/attunement chips, Continue Run `S-M`

## THEME 3 — Juice / Animation
- [~] 3.1 Animator-driven animation (procedural CombatantMotion done; add AnimatorController + death anim) `M`
- [ ] 3.2 Per-ability VFX + SFX identity (spawn ability.vfxPrefab, play sfxId) `M`
- [ ] 3.3 Telegraph clarity (charging VFX + rising audio) `M`
- [ ] 3.4 Post-FX pulses on crit/Break (chromatic/zoom) `M`
- [ ] 3.5 Cinemachine Impulse shake `S`
- [ ] 3.6 Hit-stop / time-scale robustness `S`
- [ ] 3.7 Ambient life (embers, camera breathe) `M`
- [ ] 3.8 Floating text → TMP + DoT/status popups `S`

## THEME 4 — Audio
- [ ] 4.1 Phase-2 music intensify + Break stinger `M`
- [ ] 4.2 Fill missing SFX + route by element/ability `S`

## THEME 5 — Content / AI
- [ ] 5.1 Per-boss Ink intros & outros + run intro `M`
- [ ] 5.2 ChaoticAI Reality Warp + adaptivity `M`
- [ ] 5.3 AggressiveAI real utility scoring `S`
- [ ] 5.4 Archer Puppet as a real summoned decoy `L`
- [ ] 5.5 Every trio can exploit Ice / clear each boss `M`
- [ ] 5.6 Surface class verbs + DEX-crit `S`

## THEME 6 — Code quality / architecture
- [ ] 6.1 Data-drive synergy table + move magic numbers to BalanceConfig `M`
- [ ] 6.2 Single TargetingSystem.Resolve (kill 3 duplicate switches) `S`
- [ ] 6.3 OnValidate / Awake null-safety `M`
- [ ] 6.4 Complete the event-channel set (OnHealed/OnStatusApplied/OnStaggerBuilt/...) `M`
- [ ] 6.5 Real state-object FSM + de-dupe the two battle loops `L`
- [ ] 6.6 Command pacing (IEnumerator Resolve) + CompositeCommand + cheat Commands `L`
- [ ] 6.7 Split god classes (BattleHUD/JuiceController) + shared ProceduralTextures `M`
- [ ] 6.8 Comment UI/juice constants; asmdef tidy `S`

## THEME 7 — Tests & Docs
- [~] 7.1 Boss-roster + AI-validity + synergy/pity tests (BossRosterTests added) `M`
- [~] 7.2 Scoring/grade + accolades (basic S/A/B/C done; add accolades/time/BalanceConfig thresholds) `M`
- [ ] 7.3 Update README (remove overclaims, document new systems) `S`

## Deferred (need user-generated 3D models)
- 3D model import, Humanoid rig, Animator from clips, swap billboards, lighting/APV.
