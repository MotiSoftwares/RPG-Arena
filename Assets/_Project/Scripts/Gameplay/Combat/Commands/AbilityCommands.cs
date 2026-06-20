using UnityEngine;
using RPGArena.Characters;
using RPGArena.Combat.Status;

namespace RPGArena.Combat.Commands
{
    // Shared cost/status handling for every ability-driven command.
    public abstract class AbilityCommand : ICommand
    {
        protected readonly ActionRequest req;
        protected AbilityCommand(ActionRequest req) { this.req = req; }

        public abstract void Resolve(BattleContext ctx);
        public virtual string DescribeForLog() => $"{Name(req.caster)} uses {req.ability.displayName}";
        protected static string Name(Entity e) => e != null ? e.displayName : "?";

        // Spend MP, regen a little (basics), and start the cooldown. False if unaffordable.
        protected bool PayCosts(BattleContext ctx)
        {
            var a = req.ability; var c = req.caster;
            if (a.mpCost > 0 && !c.TrySpendMP(a.mpCost))
            {
                ctx.Log($"  {Name(c)} cannot afford {a.displayName} (MP {c.currentMP}/{a.mpCost})");
                return false;
            }
            if (a.mpRegenOnUse > 0) c.RegenMP(a.mpRegenOnUse);
            c.StartCooldown(a);
            return true;
        }

        // Apply the ability's statuses. Control statuses (Frozen) only land when a synergy
        // forces them (Wet+Ice / Wet+Lightning); other statuses always apply.
        protected void ApplyStatuses(BattleContext ctx, Entity target, bool synergyForced)
        {
            var list = req.ability.statusesToApply;
            if (list == null) return;
            foreach (var s in list)
            {
                if (s == null) continue;
                bool isControl = s.skipsTurn || s.flag == StatusFlag.Frozen;
                if (isControl && !synergyForced) continue;
                target.Status.Apply(s);
                ctx.Log($"      {Name(target)} gains {s.displayName}");
            }
        }
    }

    // Attack / MultiHit — the core damage command.
    public class AttackCommand : AbilityCommand
    {
        public AttackCommand(ActionRequest req) : base(req) { }

        public override void Resolve(BattleContext ctx)
        {
            if (!PayCosts(ctx)) return;
            var a = req.ability; var caster = req.caster;
            int hits = a.effectType == EffectType.MultiHit ? Mathf.Max(1, a.hits) : 1;

            foreach (var target in req.targets)
            {
                if (target == null || !target.IsAlive) continue;
                var syn = SynergyResolver.Resolve(target.Status, a.element);
                if (!string.IsNullOrEmpty(syn.note)) ctx.Log($"      synergy: {syn.note}");

                for (int h = 0; h < hits; h++)
                {
                    var info = new DamageInfo
                    {
                        source = caster, target = target, ability = a, element = a.element,
                        basePower = a.power, isMagic = a.isMagic, forceHit = a.autoHit,
                        isBreakSkill = a.HasTag("BreakSkill"), hitTier = a.hitTier
                    };
                    var result = ctx.damage.Compute(info);
                    ctx.damage.Apply(result, ctx);
                    ctx.lastActionResults.Add(result);
                    LogHit(ctx, result, target);
                }

                ApplyStatuses(ctx, target, syn.forceStatusApply);
            }
        }

        private void LogHit(BattleContext ctx, DamageResult r, Entity target)
        {
            if (!r.hit) { ctx.Log($"      -> {Name(target)}: MISS (chance {r.hitChance:P0})"); return; }
            string tag = r.absorbed ? "ABSORBED→healed" :
                         r.reaction == ElementReaction.Weak ? "WEAK!" :
                         r.reaction == ElementReaction.Resist ? "resist" :
                         r.reaction == ElementReaction.Immune ? "immune" : "";
            string crit = r.crit ? " CRIT" : "";
            string verb = r.isHeal ? "heals" : "takes";
            string extra = string.IsNullOrEmpty(tag) ? "" : " " + tag;
            ctx.Log($"      -> {Name(target)} {verb} {r.amount}{extra}{crit}  (roll {r.damageRoll:0.00}, HP {target.currentHP}/{target.stats.maxHP})");
        }
    }

    // Heal — restore ally HP scaled by the caster's MagicAttack.
    public class HealCommand : AbilityCommand
    {
        public HealCommand(ActionRequest req) : base(req) { }

        public override void Resolve(BattleContext ctx)
        {
            if (!PayCosts(ctx)) return;
            var a = req.ability; var caster = req.caster;
            foreach (var target in req.targets)
            {
                if (target == null || !target.IsAlive) continue;
                int heal = Mathf.RoundToInt(caster.MagicAttack * Mathf.Max(0f, a.power));
                int before = target.currentHP;
                target.Heal(heal);
                ctx.Log($"      -> {Name(target)} healed {target.currentHP - before} (HP {target.currentHP}/{target.stats.maxHP})");
            }
        }
    }

    // Buff / Debuff / ApplyStatus / Defend / Stance — apply the ability's statuses to targets.
    public class ApplyStatusCommand : AbilityCommand
    {
        public ApplyStatusCommand(ActionRequest req) : base(req) { }

        public override void Resolve(BattleContext ctx)
        {
            if (!PayCosts(ctx)) return;
            ctx.Log($"  {DescribeForLog()}");
            foreach (var target in req.targets)
            {
                if (target == null || !target.IsAlive) continue;
                ApplyStatuses(ctx, target, true);   // status abilities apply directly
            }
        }
    }

    // Boss telegraph/charge — commits the boss to a big attack next turn and warns the party.
    public class BossMoveCommand : AbilityCommand
    {
        public BossMoveCommand(ActionRequest req) : base(req) { }

        public override void Resolve(BattleContext ctx)
        {
            if (!PayCosts(ctx)) return;
            var a = req.ability; var caster = req.caster;
            if (a.telegraphsAbility != null)
            {
                caster.telegraphedAbility = a.telegraphsAbility;
                ctx.Log($"  >>> {Name(caster)} is charging {a.telegraphsAbility.displayName}! (defend, break it, or dodge) <<<");
                ctx.RaiseTelegraph(a.telegraphsAbility);
            }
            else
            {
                ctx.Log($"  {DescribeForLog()}");
            }
        }
    }

    // Builds the right command for an action based on the ability's effect type (§4.3).
    public static class CommandFactory
    {
        public static ICommand Build(ActionRequest req)
        {
            switch (req.ability.effectType)
            {
                case EffectType.Heal: return new HealCommand(req);
                case EffectType.BossMove: return new BossMoveCommand(req);
                case EffectType.Buff:
                case EffectType.Debuff:
                case EffectType.ApplyStatus:
                case EffectType.Defend:
                case EffectType.Stance:
                    return new ApplyStatusCommand(req);
                default:
                    return new AttackCommand(req);   // Attack, MultiHit, Composite
            }
        }
    }
}
