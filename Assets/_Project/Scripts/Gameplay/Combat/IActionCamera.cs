using RPGArena.Characters;

namespace RPGArena.Combat
{
    // The presentation seam for "push the camera in on whoever is acting RIGHT NOW".
    //
    // Same trick as IBattleIntro: the assembly dependency runs UI -> Gameplay only, so the battle
    // loop can never name JuiceController directly. It finds this interface on whatever object
    // implements it and stays ignorant of the presentation layer.
    //
    // Why the loop drives it at all: the punch-in used to ride the OnTurnStarted channel, which for
    // a hero fires when their action MENU opens. The player then spends several seconds choosing a
    // skill, by which time the camera has already leaned in and eased back out — so heroes never
    // appeared to get a close-up while enemies did. Damaging actions are focused from the damage
    // beat; this exists for the ones that never produce a DamageResult (heals, buffs, stances).
    public interface IActionCamera
    {
        void FocusOnActor(Entity actor);
    }
}
