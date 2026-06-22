// evilwarrior_outro.ink — closing lines after the Evil Warrior fight, set from the real result (§13).

VAR won = false
VAR broke_boss = false
VAR heroes_lost = 0

-> outro

=== outro ===
{ won:
    The dark knight sinks to one knee, greatsword ringing against the stone as it falls.
    { broke_boss: You cracked the cursed plate and struck the man inside — armour means nothing, broken. }
    { heroes_lost > 0:
        He took some of you with him. Honour the fallen; they bought this ending.
    - else:
        And he could not fell a single one of you. Stand tall.
    }
    "Hope... wins, then," he rasps, almost relieved, and is still.
- else:
    His blade finds the last of you, and the throne hall falls silent.
    The Arena of the Algorithms claims another band of champions. Rise, and try again.
}
-> END
