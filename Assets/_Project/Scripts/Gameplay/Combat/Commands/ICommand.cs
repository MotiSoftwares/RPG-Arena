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

        public ActionRequest(Ability ability, Entity caster, params Entity[] targets)
        {
            this.ability = ability;
            this.caster = caster;
            this.targets = targets;
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
