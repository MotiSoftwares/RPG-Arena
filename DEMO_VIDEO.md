# Demo video — shotlist & narration script (5–10 min)

This is the one deliverable a tool can't produce. The architecture is built from nameable
patterns precisely so you can explain it confidently. Record clean audio; run a **development
build** or the Editor so you can show the Cheat panel.

---

## 1. Pitch & menus (≈45s)
- **Show:** Main Menu → Character Select.
- **Say:** "Arena of the Algorithms is a 2.5D turn-based boss-rush RPG. You pick three of four
  classic classes and fight a gauntlet of three intelligent bosses. The depth comes from the
  interaction of simple systems — elemental weakness, a stagger/break meter, cross-class status
  combos, and resource management — not from a wall of stats."
- Pick **Warrior + Mage + Thief** (the forgiving, combo-friendly trio). Confirm.

## 2. The Dragon — narrate a smart line (≈2.5 min)  ← the core showcase
- **Ink intro:** choose **"Study it"** → point out the HUD now shows **Weak: Ice · Absorbs: Fire**.
  "The narrative choice changed the game state — this is the Ink integration writing into combat."
- **Teach the lesson:** cast **Fireball** once → "It *absorbed* it and healed. Don't burn the fire
  dragon." Then re-attune to **Ice** and hit for a big **WEAK!** number.
- **Combo:** Thief **Water Bomb** (*Wet*) → Mage **Ice Lance** → **Frozen**. Show the stagger bar
  filling on weakness hits.
- **The signature moment:** when the Dragon uses **Charging Breath** (telegraph banner appears),
  race to fill the Stagger meter → **BREAK!** "Breaking it mid-charge **cancels Flame Breath** —
  that's the skill expression." Then unload (Blizzard / Crushing Blow) in the Break window — show
  hit-stop, screen shake, the floating numbers, slow-mo.
- Mention the **hit %** shown on a Risky skill before you commit (the informed-gamble RNG).

## 3. Class identity & survival (≈1 min)
- Show the Warrior **Berserk/Guardian** stance toggle and a **Guardian Taunt** before a hit.
- Show the Mage **Heal** keeping the glass party alive.
- Pick up a **boon** on the victory screen → point out the party-wide upgrade ("roguelite layer").

## 4. Architecture walkthrough (≈2 min)  ← screen-share the project
- "Content is **ScriptableObjects** — here are the abilities, characters, bosses, AI, statuses,
  boons. Adding content is data, not code."
- "Actions are **Command** objects; the battle is a **finite-state machine**; boss AI is the
  **Strategy** pattern — three swappable brains. Systems talk through **ScriptableObject event
  channels**, so the UI/audio/juice only react." 
- Show the **combat log** as live evidence of the Command pattern's uniform logging.
- Open the **Test Runner** → run the edit-mode suite (damage formula, elemental matrix, stagger,
  synergies, all four trios clear the Dragon) → all green.

## 5. The other two bosses + bonus tour (≈1.5 min)
- Quick clips of **Black Mage** (cursing/debuffing chaos — bring the Archer's never-miss shots /
  cleanse) and **Evil Warrior** (focuses your weakest hero, weak to Lightning, resists Physical).
- Open the **Cheat panel** (`` ` ``): god mode, force-Break, set boss HP — "Editor/dev only; the
  whole assembly is stripped from release builds."
- Note the **AudioMixer** settings sliders.

## 6. PM evidence (≈30s)
- Show the **GitHub** repo: the milestone epics/issues and the commit history (the sprint log),
  and the `README.md`.

---

### One-line claims you can make truthfully
- 7 distinct gameplay mechanics (rubric needs 5). · 4 classes, 3 bosses, full run flow + boons.
- Clean pattern-based architecture (Command / State / Strategy / SO-data + event channels).
- Bonus: Cheat Manager, three Strategy AIs, deep Ink integration, plus the creativity/juice layer.
- Edit-mode test suite; AudioMixer audio; Animator-free billboard art for the 2.5D look.
