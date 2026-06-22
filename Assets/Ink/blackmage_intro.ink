// blackmage_intro.ink — shown at battle setup, BEFORE the Black Mage fight.
// Mirrors the Dragon's deep Ink<->combat hook (CLAUDE.md §13): the choice writes a variable AND
// calls an EXTERNAL the game binds, so the narrative changes how the fight opens.

VAR mock_mage = false
VAR study_mage = false

EXTERNAL StartWithTelegraph()   // provoke him into opening with a channelled nuke (telegraphed)
EXTERNAL RevealWeakness()       // surface his Holy weakness / Dark resistance in the HUD now

-> blackmage_intro

=== blackmage_intro ===
The air curdles. Violet fire gutters around a figure of tattered robes and a skull-topped staff.
"Champions. How quaint. I have unmade gods — you will be a footnote."
* [Mock him — "All that power, and still bound to this arena."]
    ~ mock_mage = true
    His eyes blaze. Power gathers at his fingertips, far sooner than it should.
    ~ StartWithTelegraph()
    -> begin
* [Study him — read the currents of his magic.]
    ~ study_mage = true
    You see it: the dark recoils from holy light, and answers dark with contempt. Light will hurt him.
    ~ RevealWeakness()
    -> begin
* [Stand firm. Let him come.]
    The Black Mage smiles, and the shadows lengthen. Battle is joined.
    -> begin

=== begin ===
-> DONE
