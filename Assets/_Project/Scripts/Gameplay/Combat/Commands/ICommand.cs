using RPGArena.Characters;

namespace RPGArena.Combat.Commands
{
    // Carries everything one action needs: the Ability + caster + target(s). Built by the
    // BattleManager from the chosen ability (§4.3).
    public struct ActionRequest
    {
        public Ability ability;
        public Entity caster;
        public Entity[] targets;

        // Action-command result supplied by the LIVE game's presentation layer: a timed strike
        // lands ×1.18, a sloppy one ×0.9, a BRACED enemy blow ×0.7. Headless battles never set
        // it — 0 (an unset struct) reads as 1 via Mult, so tests and AI paths are bit-identical.
        public float timingMult;
        public float Mult => timingMult <= 0f ? 1f : timingMult;

        public ActionRequest(Ability ability, Entity caster, params Entity[] targets)
            : this(ability, caster, 1f, targets) { }

        public ActionRequest(Ability ability, Entity caster, float timingMult, params Entity[] targets)
        {
            this.ability = ability;
            this.caster = caster;
            this.targets = targets;
            this.timingMult = timingMult;
        }
    }

    // The Command pattern. M1 resolves synchronously (a logged simulation); presentation
    // sequencing (animation/VFX/hit-stop timing) layers on top in later milestones. Keeping
    // every action behind this interface gives uniform logging + trivial cheat/test hooks.
    public interface ICommand
    {
        void Resolve(BattleContext ctx);
        string DescribeForLog();
    }
}
