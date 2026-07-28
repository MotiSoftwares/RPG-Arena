using System.Collections.Generic;
using RPGArena.Characters;
using RPGArena.Combat.Commands;

namespace RPGArena.Combat
{
    // The battle orchestrator FSM (§4.6). For M1 it runs SYNCHRONOUSLY to completion so the
    // whole fight is headlessly verifiable (it logs every turn, damage, element reaction,
    // stagger build/break, telegraph, and win/lose). The named phases below mirror §4.6; the
    // coroutine/state-object form with animation timing is layered on in M3 when the player
    // UI and presentation exist.
    public class BattleManager
    {
        public enum Outcome { InProgress, Victory, Defeat }

        private BattleContext ctx;
        private readonly HashSet<Entity> announcedDead = new();
        private const int MaxRounds = 60;   // safety cap so a bad loop can never hang the game

        public Outcome RunToCompletion(BattleContext context)
        {
            ctx = context;
            announcedDead.Clear();
            BattleSetup();

            for (int round = 1; round <= MaxRounds; round++)
            {
                RoundStart(round);
                var order = ctx.turns.BuildRoundOrder(AllCombatants(), ctx.rng,
                                                      ctx.balance.maxExtraTurnsPerEntityPerRound);
                while (order.Count > 0)
                {
                    var actor = order.Dequeue();
                    if (actor == null || !actor.IsAlive) continue;

                    TurnStart(actor);
                    if (actor.CanAct)
                    {
                        var cmd = Decide(actor, out var used);   // AwaitInput / EnemyDecision (brains in M1)
                        if (cmd != null) ResolveAction(actor, cmd, used, order);
                        else ctx.Log($"  {actor.displayName} has no affordable action — passes.");
                    }
                    else
                    {
                        ctx.Log($"  {actor.displayName} cannot act (frozen / staggered) — turn skipped.");
                    }

                    CheckDeaths();
                    ctx.boss?.CheckPhaseTransition(ctx);   // per-action (matches BattleController)
                    TurnEnd(actor);

                    var outcome = Evaluate();
                    if (outcome != Outcome.InProgress) { Finish(outcome); return outcome; }
                }
                RoundEnd(round);
            }

            ctx.Log("[Battle] Exceeded max rounds — declaring Defeat (safety cap).");
            Finish(Outcome.Defeat);
            return Outcome.Defeat;
        }

        // --- Phases (named per §4.6) --------------------------------------------------
        private void BattleSetup()
        {
            ctx.Log($"=== BATTLE START: party of {ctx.heroes.Count} vs {ctx.boss.displayName} ===");
            ctx.onBattleStarted?.Raise();
        }

        private void RoundStart(int round) => ctx.Log($"--- Round {round} ---");

        private void TurnStart(Entity actor)
        {
            int dot = actor.TickStartOfTurn();
            if (dot > 0)
                ctx.Log($"  {actor.displayName} suffers {dot} damage-over-time (HP {actor.currentHP}/{actor.stats.maxHP})");
            ctx.RaiseTurnStarted(actor);
            ctx.Log($"  > {actor.displayName}'s turn  (HP {actor.currentHP}/{actor.stats.maxHP}, MP {actor.currentMP}/{actor.stats.maxMP})");
        }

        // AwaitInput / EnemyDecision: in M1 every combatant decides via its brain.
        private ICommand Decide(Entity actor, out Ability used)
        {
            used = null;
            if (actor.Brain == null) return null;
            var opponents = actor.team == Team.Heroes ? new List<Entity> { ctx.boss } : ctx.heroes;
            var ability = actor.Brain.DecideAction(ctx, actor, opponents, out var target);
            if (ability == null) return null;
            used = ability;
            var targets = ResolveTargets(actor, ability, target, opponents);
            return CommandFactory.Build(new ActionRequest(ability, actor, targets));
        }

        // Expand the chosen ability + single target into the concrete target list.
        private Entity[] ResolveTargets(Entity actor, Ability ability, Entity single, List<Entity> opponents)
        {
            switch (ability.targetRule)
            {
                case TargetRule.AllEnemies:
                    return TargetingSystem.AllAlive(opponents).ToArray();
                case TargetRule.AllAllies:
                    var allies = actor.team == Team.Heroes ? ctx.heroes : new List<Entity> { ctx.boss };
                    return TargetingSystem.AllAlive(allies).ToArray();
                case TargetRule.Self:
                    return new[] { actor };
                case TargetRule.SingleAlly:
                    return new[] { single != null ? single : actor };
                default: // SingleEnemy / Summon
                    var t = single != null ? single : TargetingSystem.FirstAlive(opponents);
                    return t != null ? new[] { t } : new Entity[0];
            }
        }

        private void ResolveAction(Entity actor, ICommand cmd, Ability used, Queue<Entity> order)
        {
            ctx.lastActionResults.Clear();
            ctx.Log($"  {cmd.DescribeForLog()}");
            cmd.Resolve(ctx);

            // Action economy: one action per hero per round (no "+1 More" bonus turn). Weakness/combo
            // pays off through Valor + damage, not an extra turn.
            if (actor.team == Characters.Team.Heroes)
                ChargeSystem.AwardFor(used, ctx.lastActionResults, ctx);   // party Valor (null-safe; headless = no-op)
        }

        private void CheckDeaths()
        {
            foreach (var e in AllCombatants())
            {
                if (e.IsAlive) continue;
                // MIRRORED INVARIANT — the same guard runs in BattleController.CheckDeaths.
                if (BoonSystem.TrySecondWind(e, ctx)) continue;
                if (announcedDead.Add(e))
                {
                    ctx.Log($"  X {e.displayName} has fallen.");
                    ctx.RaiseEntityDied(e);
                }
            }
        }

        private void TurnEnd(Entity actor)
        {
            actor.TickEndOfTurn();
            if (actor.team == Characters.Team.Heroes) ctx.charge?.ConsumeHeroTurn(ctx);   // count down an active Overdrive (null-safe)
            if (actor.isBoss) GainBossFury(actor);
            ctx.RaiseTurnEnded(actor);
        }

        // Searing Fury: the boss escalates each of its turns (vented to 0 by a Break). Shared by the
        // headless loop here and the live BattleController so the mechanic is identical in tests + game.
        private void GainBossFury(Entity boss)
        {
            int before = boss.rageStacks;
            boss.rageStacks = System.Math.Min(boss.rageStacks + 1, ctx.balance.rageMaxStacks);
            if (boss.rageStacks > before)
                ctx.Log($"    {boss.displayName}'s Searing Fury rises to {boss.rageStacks} (+{boss.rageStacks * ctx.balance.rageDamagePerStack * 100f:0}% damage — BREAK it to vent!)");

            // MIRRORED INVARIANT — the same call exists in BattleController's live loop.
            // The Evil Warrior's guard re-settles: unconverted stagger bleeds off each of his turns,
            // so chip-and-turtle can never bank a Break. A no-op for bosses with decay 0.
            boss.DecayStagger(ctx);
        }

        // Boss phase transitions (enrage at HP thresholds, §7.1). For M1 the DragonCycleAI
        // reads HP directly to shorten its cycle; this stays a hook for explicit phase effects
        // (new abilities, attack buffs) added with the other bosses in M6.
        private void RoundEnd(int round) => ctx.boss?.CheckPhaseTransition(ctx);

        private Outcome Evaluate()
        {
            if (ctx.BossDead) return Outcome.Victory;
            if (ctx.AllHeroesDead) return Outcome.Defeat;
            return Outcome.InProgress;
        }

        private void Finish(Outcome outcome)
        {
            if (outcome == Outcome.Victory)
            {
                ctx.Log($"=== VICTORY! {ctx.boss.displayName} is slain. ===");
                ctx.onBattleWon?.Raise();
            }
            else
            {
                ctx.Log("=== DEFEAT. The party has fallen. ===");
                ctx.onBattleLost?.Raise();
            }
        }

        private List<Entity> AllCombatants()
        {
            var all = new List<Entity>(ctx.heroes);
            if (ctx.boss != null) all.Add(ctx.boss);
            return all;
        }
    }
}
