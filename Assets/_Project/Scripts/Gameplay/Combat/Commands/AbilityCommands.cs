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

                // CONTROL DIMINISHING RETURNS. Frozen skips the victim's turn and lasts 2 of ITS
                // turns, but Frost Touch has no cooldown and costs 8 MP against 4 MP/turn regen plus
                // an 8 MP refund on the free basic — so re-applying it every round was MP-positive
                // and the boss could be locked out of the ENTIRE fight from round one. With the phase
                // rewrite handing the player free ordering, Wet->Freeze inside one phase is
                // guaranteed, which turned the marquee combo into a win button: no boss counterplay
                // survives (Devour needs a turn, Fury never lands, stagger decay is out-paced).
                //
                // A freeze now puts the target on a thaw cooldown, so control is a burst window you
                // spend and re-earn instead of a state you hold. Player-facing and readable: the log
                // says why it failed.
                if (isControl)
                {
                    if (target.controlLockTurns > 0)
                    {
                        ctx.Log($"      {Name(target)} shrugs off the cold — still thawing ({target.controlLockTurns})");
                        continue;
                    }
                    int lockTurns = ctx.balance != null ? ctx.balance.controlLockTurns : 4;
                    target.controlLockTurns = Mathf.Max(s.durationTurns, lockTurns);
                }

                target.Status.Apply(s);
                ctx.Log($"      {Name(target)} gains {s.displayName}");

                var mods = ctx.mods;
                if (mods == null) continue;

                // PERMAFROST boon: a Freeze also lands the Marked flag, so the FROST line reaches
                // Brittle without anyone spending a turn on the Mark.
                if (s.flag == StatusFlag.Frozen && mods.FreezeMarks && !target.Status.Has(mods.freezeAlsoApplies))
                {
                    target.Status.Apply(mods.freezeAlsoApplies);
                    ctx.Log($"      {Name(target)} is {mods.freezeAlsoApplies.displayName} — the frost bites deep (Permafrost)");
                }

                // TARPITS boon: coatings linger. Extend rather than re-apply — Apply() only refreshes
                // to the base duration, which would make the boon a no-op.
                if (mods.bonusCoatingTurns > 0 && (s.flag == StatusFlag.Wet || s.flag == StatusFlag.Oiled))
                    target.Status.ExtendByFlag(s.flag, mods.bonusCoatingTurns);
            }
        }
    }

    // Attack / MultiHit — the core damage command.
    public class AttackCommand : AbilityCommand
    {
        public AttackCommand(ActionRequest req) : base(req) { }

        // STEADY raises the roll floor to the whiff threshold, so the special simply cannot go bad;
        // ALL IN throws away whatever safety the ability was authored with. PRESS uses the authored
        // floor unchanged, which is what every AI and headless caller gets.
        private float RiskFloorFor(Ability a, BattleContext ctx)
        {
            var cfg = ctx != null ? ctx.balance : null;
            if (cfg == null) return a.riskFloor;
            switch (req.stake)
            {
                case RiskStake.Steady: return Mathf.Max(a.riskFloor, cfg.stakeSteadyFloor);
                case RiskStake.AllIn: return 0f;
                default: return a.riskFloor;
            }
        }

        // STEADY pays a flat damage tax on top of its narrowed bands. Measured over the full d20:
        // floor-ing the roll out of Backfire/Whiff is itself worth ~3% EV, so without a tax the
        // "safe" stake had BOTH the best expected damage and the lowest variance and PRESS was
        // strictly dominated — i.e. not a choice at all. Now safety costs you real damage.
        private float StakePowerMult(BattleContext ctx)
        {
            if (!req.ability.rollsRiskDie || req.stake != RiskStake.Steady) return 1f;
            var cfg = ctx != null ? ctx.balance : null;
            return cfg != null ? cfg.stakeSteadyDamageMult : 1f;
        }

        private float RiskScaleFor(BattleContext ctx)
        {
            var cfg = ctx != null ? ctx.balance : null;
            if (cfg == null) return 1f;
            switch (req.stake)
            {
                case RiskStake.Steady: return cfg.stakeSteadyScale;
                case RiskStake.AllIn: return cfg.stakeAllInScale;
                default: return 1f;
            }
        }

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
                        basePower = a.power * req.Mult * StakePowerMult(ctx),   // ×1 unless a live action-command adjusted it
                        isMagic = a.isMagic, forceHit = a.autoHit,
                        isBreakSkill = a.HasTag("BreakSkill"), hitTier = a.hitTier,
                        rollsRiskDie = a.rollsRiskDie, riskFloor = RiskFloorFor(a, ctx),
                        riskStakeScale = RiskScaleFor(ctx), backfireKind = a.backfireKind
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

        // The setup flags a devourer can eat. Frozen is deliberately NOT here: freezing is the
        // party's escape hatch against this boss, and leaving it edible would remove their only
        // safe play instead of asking them to change plan.
        private static readonly StatusFlag[] SetupFlags =
            { StatusFlag.Wet, StatusFlag.Oiled, StatusFlag.Marked };

        public override void Resolve(BattleContext ctx)
        {
            if (!PayCosts(ctx)) return;
            var a = req.ability; var caster = req.caster;

            if (a.consumesSetupFlags) { Devour(ctx, caster, a); return; }

            if (a.telegraphsAbility != null)
            {
                caster.telegraphedAbility = a.telegraphsAbility;
                ctx.Log($"  >>> {Name(caster)} is charging {a.telegraphsAbility.displayName}! (defend, break it, or dodge) <<<");
                ctx.RaiseTelegraph(a.telegraphsAbility);

                // The charge is NOT a free turn any more: if the move has power, the boss lashes out
                // while gathering it (a tail flick at a fraction of a real hit). Every enemy turn now
                // deals visible damage — "the boss stood there doing nothing" rounds are gone, and
                // the telegraph still reads loud because the REAL payload lands next turn.
                if (a.power > 0f && req.targets != null && req.targets.Length > 0
                    && req.targets[0] != null && req.targets[0].team != caster.team && req.targets[0].IsAlive)
                {
                    var t = req.targets[0];
                    var info = new DamageInfo
                    {
                        source = caster, target = t, ability = a, element = a.element,
                        basePower = a.power * req.Mult, isMagic = a.isMagic,
                        forceHit = a.autoHit, hitTier = a.hitTier
                    };
                    var r = ctx.damage.Compute(info);
                    ctx.damage.Apply(r, ctx);
                    ctx.lastActionResults.Add(r);
                    ctx.Log($"      ...and lashes out at {Name(t)} mid-charge ({(r.hit ? r.amount.ToString() : "graze")})!");
                }
            }
            else
            {
                ctx.Log($"  {DescribeForLog()}");
            }
        }

        // DEVOUR — the Black Mage's identity. The Dragon rewards stacking setups; this boss PUNISHES
        // it, eating the party's coatings and marks to heal himself and stoke his own Fury. The same
        // habit that beats boss #1 feeds boss #2, so the player has to notice and change plan:
        // spend setups immediately, or freeze him (Frozen is inedible) instead of stockpiling.
        private void Devour(BattleContext ctx, Entity caster, Ability a)
        {
            // Eat off whoever the move was aimed at; fall back to the boss's own target pool.
            var victim = (req.targets != null && req.targets.Length > 0 && req.targets[0] != null)
                ? req.targets[0] : null;
            if (victim == null && ctx.heroes.Count > 0) victim = ctx.heroes[0];

            int eaten = 0;
            // The setups the party lays live on the BOSS (they are debuffs on him), so a devourer
            // eats them off ITSELF — that is the point: he swallows the work you did to him.
            var plate = caster.Status;
            foreach (var f in SetupFlags) eaten += plate.RemoveByFlag(f);

            if (eaten == 0)
            {
                ctx.Log($"  {Name(caster)} reaches for your magic and finds nothing to devour.");
                return;
            }

            int heal = Mathf.RoundToInt(caster.stats.maxHP * Mathf.Max(0f, a.devourHealPercentMaxHP) * eaten);
            if (heal > 0) caster.Heal(heal);
            int fury = Mathf.Max(0, a.devourFuryPerFlag) * eaten;
            if (fury > 0 && ctx.balance != null)
                caster.rageStacks = Mathf.Min(caster.rageStacks + fury, ctx.balance.rageMaxStacks);

            ctx.Log($"  >>> {Name(caster)} DEVOURS {eaten} of your setup(s)! +{heal} HP, Fury +{fury} <<<");
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
