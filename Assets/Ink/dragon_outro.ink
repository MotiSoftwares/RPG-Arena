// dragon_outro.ink — shown after the fight. The runner sets these variables from the actual
// battle result, so the closing lines reference what really happened (CLAUDE.md §13).

VAR won = false
VAR broke_boss = false
VAR heroes_lost = 0

-> outro

=== outro ===
{ won:
    The dragon shudders and collapses, its inner fire guttering out into smoke.
    { broke_boss: You cracked its guard and struck where it could not defend — a true hunter's kill. }
    { heroes_lost > 0:
        Not all of you walked out of the ash. The fallen earned this victory too.
    - else:
        And not one of you fell. Flawless.
    }
    "Impossible..." it breathes, and is still.
- else:
    The flames wash over you, and the cavern goes white, then dark.
    The Arena of the Algorithms claims another band of champions. Rise, and try again.
}
-> END
