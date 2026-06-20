// dragon_intro.ink — shown at battle setup, BEFORE the Dragon fight.
// The choice writes a gameplay-affecting variable AND calls an EXTERNAL the game binds, so the
// narrative changes how combat opens (CLAUDE.md §13). This is the deep Ink<->combat hook.

VAR taunt_dragon = false
VAR study_dragon = false

// Bound by NarrativeRunner; these reach into the BattleController's opening state.
EXTERNAL StartWithTelegraph()   // the Dragon opens by charging — riskier, faster, but telegraphed
EXTERNAL RevealWeakness()       // surface the Dragon's Ice weakness / Fire-absorb in the HUD now

-> dragon_intro

=== dragon_intro ===
The cavern shakes. An ancient dragon uncoils from a lake of fire, eyes like furnace doors.
"Another band of champions? You will make fine ash."
* [Taunt it — "Your fire is nothing to us."]
    ~ taunt_dragon = true
    Its nostrils flare. It rears back, already gathering flame in its throat.
    ~ StartWithTelegraph()
    -> begin
* [Study it — read its stance before you strike.]
    ~ study_dragon = true
    You watch how the flames coil around it: fire FEEDS this beast — but ice will bite deep.
    ~ RevealWeakness()
    -> begin
* [Say nothing. Draw your weapons.]
    The dragon's roar shakes dust from the ceiling. Battle is joined.
    -> begin

=== begin ===
-> DONE
