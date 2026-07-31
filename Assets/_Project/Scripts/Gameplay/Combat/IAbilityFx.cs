using RPGArena.Characters;

namespace RPGArena.Combat
{
    // The presentation seam for "this non-damaging skill just resolved — SELL it".
    //
    // Same trick as IBattleIntro / IActionCamera: the assembly dependency runs UI -> Gameplay only
    // (RPGArena.Gameplay references RPGArena.Core and NOTHING else), so the battle loop can never
    // name JuiceController or ProjectileFlight. It finds this interface on whatever object
    // implements it and stays ignorant of the presentation layer.
    //
    // Why it exists: ApplyStatus / Buff / Debuff skills produce no DamageResult, so they never reach
    // JuiceController.AttackBeat — no impact burst, no boss-scale multiplier, no projectile flight,
    // no flinch, no shake. The Thief's Water Bomb therefore bloomed a ~1-unit water puff at the FEET
    // of a 7.5-unit dragon, at Quaternion.identity, unscaled, with vfxIsProjectile silently ignored,
    // which is why laying a setup flag read as "nothing happened".
    //
    // Implementers must be safe to call with a null/empty target array, must never touch combat
    // logic, and must never be REQUIRED: a bare or harness scene has no implementer and the loop
    // falls back to its own plain feet-bloom.
    public interface IAbilityFx
    {
        // Returns the seconds of presentation this effect needs (0 when instantaneous), so the
        // battle loop can hold the turn until a thrown bomb has actually landed.
        float PlayAbilityFx(Entity caster, Ability ability, Entity[] targets);
    }
}
