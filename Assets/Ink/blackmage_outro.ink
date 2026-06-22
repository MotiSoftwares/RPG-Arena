// blackmage_outro.ink — closing lines after the Black Mage fight, set from the real result (§13).

VAR won = false
VAR broke_boss = false
VAR heroes_lost = 0

-> outro

=== outro ===
{ won:
    The Black Mage's form unravels, robes collapsing around a fading violet light.
    { broke_boss: You shattered his concentration mid-cast — even his oblivion could not finish. }
    { heroes_lost > 0:
        Some of you paid the toll in shadow. Their names will outlast his.
    - else:
        And the darkness took none of you. A perfect light.
    }
    "A footnote... after all," he whispers, and is unmade.
- else:
    His laughter is the last thing you hear as the dark folds over the party.
    The Arena of the Algorithms claims another band of champions. Rise, and try again.
}
-> END
