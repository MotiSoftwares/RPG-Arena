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
            // Attunement-following basics (the Mage's Magic Bolt) take the caster's school.
            ElementType element = a.followsAttunement ? caster.currentAttunement : a.element;

            foreach (var target in req.targets)
            {
                if (target == null || !target.IsAlive) continue;
                var syn = SynergyResolver.Resolve(target.Status, element);
                if (!string.IsNullOrEmpty(syn.note)) ctx.Log($"      synergy: {syn.note}");

                for (int h = 0; h < hits; h++)
                {
                    var info = new DamageInfo
                    {
                        source = caster, target = target, ability = a, element = element,
                        basePower = a.power * req.Mult,   // ×1 unless a live action-command adjusted it
                        isMagic = a.isMagic, forceHit = a.autoHit,
                        isBreakSkill = a.HasTag("BreakSkill"), hitTier = a.hitTier,
                        rollsRiskDie = a.rollsRiskDie, riskFloor = a.riskFloor, backfireKind = a.backfireKind
                    };
                    var result = ctx.damage.Compute(info);
                    ctx.damage.Apply(result, ctx);
                    ctx.lastActionResults.Add(result);
                    LogHit(ctx, result, target);
                    // EmboldenBoss backfire: a bad gamble buffs the dragon. (SelfRecoil/Fizzle are
                    // handled inside the pipeline — self-damage in Apply, reduced damage by the band.)
                    if (result.risked && result.riskBand == RiskBand.Backfire
                        && a.backfireKind == BackfireKind.EmboldenBoss && a.backfireStatus != null && ctx.boss != null)
                    {
                        ctx.boss.Status.Apply(a.backfireStatus);
                        ctx.Log("      BACKFIRE! The dragon is emboldened!");
                    }
                }

                ApplyStatuses(ctx, target, syn.forceStatusApply);
            }
        }

        private void LogHit(BattleContext ctx, DamageResult r, Entity target)
        {
            if (!r.hit) { ctx.Log($"      -> {Name(target)}: MISS (chance {r.hitChance:P0})"); return; }
            if (r.glanced) { ctx.Log($"      -> {Name(target)} takes {r.amount} GRAZE (chance {r.hitChance:P0}, HP {target.currentHP}/{target.stats.maxHP})"); return; }
            string tag = r.absorbed ? "ABSORBED→healed" :
                         r.reaction == ElementReaction.Weak ? "WEAK!" :
                         r.reaction == ElementReaction.Resist ? "resist" :
                         r.reaction == ElementReaction.Immune ? "immune" : "";
            string crit = r.crit ? " CRIT" : "";
            string verb = r.isHeal ? "heals" : "takes";
            string extra = string.IsNullOrEmpty(tag) ? "" : " " + tag;
            string die = r.risked ? $"  [d20 {r.riskFace}: {r.riskBand}]" : "";
            ctx.Log($"      -> {Name(target)} {verb} {r.amount}{extra}{crit}{die}  (roll {r.damageRoll:0.00}, HP {target.currentHP}/{target.stats.maxHP})");
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
                int heal = Mathf.RoundToInt(caster.MagicAttack * Mathf.Max(0f, a.power)) + Mathf.Max(0, a.flatPower);
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

    // Stance / attunement toggle (§5.9): the Mage cycles its elemental school; the Warrior
    // swaps between mutually-exclusive Berserk/Guardian stance statuses.
    public class StanceCommand : AbilityCommand
    {
        public StanceCommand(ActionRequest req) : base(req) { }

        public override void Resolve(BattleContext ctx)
        {
            if (!PayCosts(ctx)) return;
            var a = req.ability; var caster = req.caster;
            switch (a.stanceAction)
            {
                case StanceAction.CycleAttunement: CycleAttunement(ctx, caster, a); break;
                case StanceAction.ToggleStatus: ToggleStance(ctx, caster, a); break;
                default: ctx.Log($"  {Name(caster)} shifts stance."); break;
            }
        }

        private void CycleAttunement(BattleContext ctx, Entity caster, Ability a)
        {
            var opts = a.attunementOptions;
            if (opts == null || opts.Length == 0) return;
            int idx = System.Array.IndexOf(opts, caster.currentAttunement);
            caster.currentAttunement = opts[(idx + 1) % opts.Length];
            ctx.Log($"  {Name(caster)} attunes to {caster.currentAttunement}.");
        }

        private void ToggleStance(BattleContext ctx, Entity caster, Ability a)
        {
            var stances = a.stanceStatuses;
            if (stances == null || stances.Length == 0) return;
            int active = -1;
            for (int i = 0; i < stances.Length; i++)
                if (stances[i] != null && caster.Status.Has(stances[i])) active = i;
            foreach (var s in stances) if (s != null) caster.Status.Remove(s);
            var next = stances[(active + 1) % stances.Length];
            if (next != null) { caster.Status.Apply(next); ctx.Log($"  {Name(caster)} switches to {next.displayName}."); }
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
                case EffectType.Stance: return new StanceCommand(req);
                case EffectType.Buff:
                case EffectType.Debuff:
                case EffectType.ApplyStatus:
                case EffectType.Defend:
                    return new ApplyStatusCommand(req);
                default:
                    return new AttackCommand(req);   // Attack, MultiHit, Composite
            }
        }
    }
}
