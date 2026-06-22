// evilwarrior_intro.ink — shown at battle setup, BEFORE the Evil Warrior fight.
// Deep Ink<->combat hook (CLAUDE.md §13): the choice changes how the fight opens.

VAR challenge_warrior = false
VAR study_warrior = false

EXTERNAL StartWithTelegraph()   // goad him into winding up his flurry immediately (telegraphed)
EXTERNAL RevealWeakness()       // surface his Lightning weakness / Physical resistance in the HUD now

-> evilwarrior_intro

=== evilwarrior_intro ===
A dark knight rises from a throne of broken blades, red light bleeding through cracked black plate.
"You reek of hope. I will carve it out of you, one by one."
* [Challenge him — "Face me, coward, if your armour lets you."]
    ~ challenge_warrior = true
    He roars and hauls his greatsword back, muscles coiling for a brutal opening blow.
    ~ StartWithTelegraph()
    -> begin
* [Study him — watch how the cursed armour moves.]
    ~ study_warrior = true
    The plate is thick — steel turns your blades — but it conducts: a storm would find the man inside.
    ~ RevealWeakness()
    -> begin
* [Form up. Protect the weak.]
    The Evil Warrior fixes his gaze on your frailest hero. Battle is joined.
    -> begin

=== begin ===
-> DONE
