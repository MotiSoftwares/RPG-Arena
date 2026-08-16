# Gameplay Video — Recording Script

**Target length:** 8 minutes (rubric asks 5–10)
**Format:** screen recording of the game + your voice-over
**Upload:** YouTube (Unlisted is fine), then paste the link into `README.md` where it says `<PASTE LINK HERE>`

---

## Before you record

1. Build or open the project, and **play one full practice run first** so you are not fighting the game
   while also talking. Know which hero you will use for each beat below.
2. Record at **1920×1080**. OBS Studio (free) → *Sources ▸ Game Capture* → pick the game window. If you
   use Windows Game Bar instead, press **Win + Alt + R** with the game focused.
3. Record **microphone + desktop audio**, and pull the game volume down to roughly 30% in the pause menu
   so the music sits under your voice instead of over it.
4. **Do not read this word-for-word in a flat voice.** These are talking points in the right order.
   Saying it in your own words is better — and it is what the rubric is actually measuring.

The rubric line is *"Narrate the video, explaining about what is happening **and about the project's
inner workings**."* Roughly half of this script is therefore about code and architecture, not just
about playing. Do not skip Part 5 — that is where the marks are.

---

## Part 1 — Opening (0:00 – 0:45)

**Show:** the main menu, title art visible. Slowly hover the buttons.

> "Hi, my name is Moti Yeshayahu, and this is *Arena of the Algorithms* — a turn-based boss-rush RPG
> built in Unity 6 with the Universal Render Pipeline.
>
> The whole game is built around one design rule that I wrote down before I wrote any code:
> **spamming the basic attack has to lose.** Not 'be weaker' — actually lose. Every system you're about
> to see exists to serve that one rule, and I'll show you at the end how I enforce it automatically
> with a test that fails the build if a basic-attack-only party can win.
>
> A run is a gauntlet of bosses. You take three heroes in, and what carries between fights is your
> health, your gold, and the rule-changing upgrades you pick up."

**Show:** click *How to Play*, scroll it briefly, back out. Click *Play* → character select.

---

## Part 2 — Character select & the run layer (0:45 – 1:30)

**Show:** the character-select screen. Hover each hero so their skills show. Pick your party. Show the
difficulty toggle.

> "You pick three heroes out of the roster, and the pick matters mechanically, not just visually —
> the combos are *cross-class*, so a party without a Mage literally cannot reach the game's strongest
> damage line and has to win a different way. I'll come back to that.
>
> Difficulty is here too. Hard is the default, and it's what I balanced against."

*(If you want a beat here: mention that Hard multiplies enemy damage output, and that you had to fix
this — the first version scaled a base stat that barely fed into the final number, so 'Hard' was
only about three percent harder, and the Black Mage was completely immune to it because he deals
magic damage.)*

**Show:** confirm → the Ink intro dialogue appears.

---

## Part 3 — Narrative choice with mechanical weight (1:30 – 2:15)

**Show:** the pre-fight dialogue. Read the choices on screen. Pick **"Study him"**.

> "Before the fight there's a short dialogue. This is written in **Ink** — Inkle Studios' narrative
> scripting language — which is my external framework integration.
>
> The important part is that this is **not** flavour text. The choices call back into C#. If I mock the
> boss, he opens the fight already charging his big attack — I've traded safety for tempo. If I study
> him, like I'm doing now, the HUD reveals his elemental weakness immediately, so I can plan my combo
> from round one.
>
> And it goes both ways: after the fight, the game pushes the result *back into* Ink's variable store —
> whether I won, whether I broke him, how many heroes I lost — so the epilogue reacts to *how* I won,
> not just that I won."

---

## Part 4 — The combat loop (2:15 – 5:15)

This is the longest section. Play naturally and narrate what you're doing.

### 4a. The phase structure (2:15 – 2:45)

**Show:** the round starting. Hover between heroes without committing.

> "A round is two phases. In the **player phase** I choose *which* hero acts — in any order I like, each
> one once. That ordering is a real decision: I can set up with one hero and detonate with another in
> the same round.
>
> Then the **enemy phase**: minions first, boss last. Every enemy acts every single round — there are no
> dead turns and nothing ever silently skips."

### 4b. Action commands (2:45 – 3:30)

**Show:** attack with the Warrior. Let the timing bar sweep and hit it. Then get hit and use the block
window. Try to land a PERFECT on camera, and if you miss one, keep it — a miss is honest.

> "Every damaging skill opens a **timing bar**. The needle sweeps out and back, and hitting the gold
> band multiplies the damage. This isn't cosmetic — it feeds into the same damage pipeline as a
> multiplier on the skill's base power.
>
> And it's symmetric: when the enemy attacks *me*, I get a **block window**. Notice the needle speed is
> randomised each time, so I can't just learn a rhythm and press on a metronome.
>
> There's a third one — after a landed hit there's a chance of a surprise **follow-up** prompt, with a
> very short window, for a bonus hit."

### 4c. The combo web — the heart of the game (3:30 – 4:20)

**Show:** run the FROST line deliberately. Thief *Water Bomb* (or Mage *Blizzard*) → **WET**. Then Mage
ice skill → **FREEZE**. Then any physical hit → **SHATTER**. Point out the `>>SHATTER` tag on the card
before you commit.

> "Here's the core of it. Statuses combine across heroes.
>
> First I apply **Wet**. Now watch the skill cards — the game tells me the combo is live before I
> commit, so this is a puzzle, not a memory test. Ice on a wet target **freezes** it. And a physical hit
> on a frozen target **shatters** — that's two-point-three times damage.
>
> That took three skills from two different heroes across two rounds, and it does roughly what six
> basic attacks would. That's the design rule made concrete.
>
> There are two other lines at lower payoff, and they're deliberately an **exclusive ladder**, not
> additive. I tried additive first and measured it — a fully loaded target was taking about five-point-
> eight times damage and fights collapsed to under four rounds. So they don't stack any more."

*(Optional, if you froze the boss:)*

> "One more thing here — freezing is on a cooldown. In an earlier version freezing had no cooldown and
> was cheap enough to re-apply every round, which locked the boss out of the entire fight. It was a win
> button, not a combo. Now landing a stun starts a thaw timer that outlasts the freeze, so it's about
> fifty percent uptime and it costs me two hero actions per cycle."

### 4d. Break, Fury and Overdrive (4:20 – 4:50)

**Show:** the Break meter filling. Break the boss if you can. Show the Overdrive meter and spend it.

> "Two meters run underneath all of this. Every hit builds **Break** on the boss. Meanwhile the boss
> gains **Fury** every turn, and its damage climbs. Breaking it vents that Fury to zero and strips its
> resistance — so the Break meter is a clock. Ignoring it is a slow loss.
>
> And my party has a shared **Overdrive** meter, charged by combos and support actions. When it's full
> it's a *choice* of three different spends, not a single button — damage, instant Break progress, or a
> party heal."

### 4e. Ultimates, positioning, items (4:50 – 5:15)

**Show:** use a hero's fifth skill (the risk-die ultimate). Show the charge-up, then the **two** timing
bars. Then swap a hero to the back row. Then use an item.

> "Each hero's fifth skill is a **risk-die ultimate** — it rolls a d20, from a backfire all the way up to
> a jackpot. And it asks for **two** timing bars instead of one, so the biggest button in the game is
> also the hardest one to execute.
>
> I can also move heroes between the front and back row mid-fight. Back row reduces single-target melee
> damage — but *not* area attacks, so turtling everybody is punished by the boss's AoE.
>
> And items — bought with gold that the minions drop."

---

## Part 5 — Inner workings (5:15 – 7:30)

**Show:** alt-tab to the Unity Editor, or to VS Code. Have these open in tabs beforehand:
`BattleManager.cs`, `DamagePipeline.cs`, the `.asmdef` files, the Test Runner window, `AIBehavior`
assets in the Project window.

### 5a. Architecture (5:15 – 6:00)

**Show:** the assembly definition files, and the Project window folder structure.

> "Now the inside. The project is split into **eight assemblies** with a strict one-way dependency rule:
> Core, then Gameplay, then UI. Gameplay is never allowed to name a UI type.
>
> That sounds like bureaucracy, but it buys me the single most valuable thing in the project: the combat
> logic is **headless**. `BattleManager` and `DamagePipeline` run a complete fight with no scene, no
> camera, no prefabs — which is why I can test and balance it.
>
> Where combat genuinely needs presentation — camera focus, ability effects, the intro dialogue — it goes
> through small interfaces that the UI layer implements. So the fight runs perfectly well with no
> presentation attached at all."

### 5b. Data-driven design (6:00 – 6:30)

**Show:** an `Ability` asset in the inspector, then `BalanceConfig`, then the `AIBehavior` assets.

> "Almost nothing is hardcoded. Every skill is a **ScriptableObject** — power, element, cost, statuses,
> visual effect. Every tuning constant in the game lives in one `BalanceConfig` asset.
>
> The enemy brains are ScriptableObjects too — they're a strategy pattern. Adding a new boss brain is a
> new asset, not a new branch in a switch statement. The Dragon runs a telegraphed rotation. The Black
> Mage **devours** the setup statuses I place on him, healing and gaining Fury — which specifically
> punishes the strategy that beats boss one.
>
> Each brain also previews its next move to the HUD, and that preview function has to be side-effect
> free — if it drew a random number, the preview would lie to the player."

### 5c. Testing as a design gate (6:30 – 7:10)

**Show:** the Unity Test Runner with all tests green. Scroll the test names.

> "This is the part I'm most pleased with. There are **54 EditMode tests**, and they aren't only
> regression cover — they're the design document, executable.
>
> `SpamLosesTests` **fails if a basic-attack-only party wins.** That rule I stated in the first thirty
> seconds isn't a note in a document I can drift away from — it's a build failure.
>
> `PartyTrioTests` checks that *every* party composition can still clear the boss, so I can't balance
> the game around one favourite team. And `RiskDiceTests` verifies that no risk option strictly
> dominates another — that the safe choice and the greedy choice are actually a trade-off.
>
> I also balanced by **measurement, not by feel**: running dozens of simulated fights headlessly and
> reading the aggregate. That's how I found that the Black Mage's signature mechanic was only firing in
> four fights out of twenty-four — playing it once, I'd never have noticed."

### 5d. Cheat manager (7:10 – 7:30)

**Show:** the cheats `.asmdef` with the define constraint, then the cheat panel in-game.

> "Last one — the cheat manager, for testing. It lives in its own assembly, and the assembly definition
> carries a constraint so the compiler only includes it in the Editor and in development builds. It's
> not an `#if` around the body that someone could forget — the entire assembly cannot ship by accident."

---

## Part 6 — Close (7:30 – 8:00)

**Show:** back to the game, finish the boss, show the victory / boon-pick screen. Then the pause menu
with the settings sliders, then quit cleanly to the menu.

> "Winning gives me a **boon** — and I deliberately wrote these to change *rules* rather than just add
> stats, because a stat card makes the same fight easier while a rule card makes it a different fight.
> My health carries into the next boss, with a floor so a run can never become mathematically
> unwinnable.
>
> Pause has full settings, driving the audio mixer buses directly.
>
> That's *Arena of the Algorithms*. Everything is in the repository — the code, the tests, and an
> engineering log documenting the decisions and the bugs I root-caused along the way. Thanks for
> watching."

---

## Checklist before uploading

- [ ] Length is between 5 and 10 minutes
- [ ] Your voice is audible over the game music the whole way through
- [ ] You showed: menu, character select, dialogue choice, a full combo, a timed strike, a block, an
      ultimate, the pause menu, and a win
- [ ] You showed **code/Editor**, not only gameplay (Part 5)
- [ ] Uploaded to YouTube, visibility **Public or Unlisted** (not Private — the marker must be able to
      open it)
- [ ] Link pasted into `README.md`
