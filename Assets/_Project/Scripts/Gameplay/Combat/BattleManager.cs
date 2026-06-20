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
                        var cmd = Decide(actor);            // AwaitInput / EnemyDecision (brains in M1)
                        if (cmd != null) ResolveAction(actor, cmd, order);
                        else ctx.Log($"  {actor.displayName} has no affordable action — passes.");
                    }
                    else
                    {
                        ctx.Log($"  {actor.displayName} cannot act (frozen / staggered) — turn skipped.");
                    }

                    CheckDeaths();
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
        private ICommand Decide(Entity actor)
        {
            if (actor.Brain == null) return null;
            var opponents = actor.team == Team.Heroes ? new List<Entity> { ctx.boss } : ctx.heroes;
            var ability = actor.Brain.DecideAction(ctx, actor, opponents, out var target);
            if (ability == null) return null;
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

        private void ResolveAction(Entity actor, ICommand cmd, Queue<Entity> order)
        {
            ctx.lastActionResults.Clear();
            ctx.Log($"  {cmd.DescribeForLog()}");
            cmd.Resolve(ctx);

            // Action economy (§5.5): a weakness hit or a crit grants one capped "1 More".
            bool weaknessOrCrit = false;
            foreach (var r in ctx.lastActionResults)
                if (r.hit && (r.reaction == ElementReaction.Weak || r.crit)) { weaknessOrCrit = true; break; }
            if (weaknessOrCrit && ctx.turns.TryGrantExtraTurn(actor, order))
                ctx.Log($"    +1 MORE! {actor.displayName} earns a bonus turn (weakness/crit).");
        }

        private void CheckDeaths()
        {
            foreach (var e in AllCombatants())
                if (!e.IsAlive && announcedDead.Add(e))
                {
                    ctx.Log($"  X {e.displayName} has fallen.");
                    ctx.RaiseEntityDied(e);
                }
        }

        private void TurnEnd(Entity actor)
        {
            actor.TickEndOfTurn();
            ctx.RaiseTurnEnded(actor);
        }

        // Boss phase transitions (enrage at HP thresholds, §7.1). For M1 the DragonCycleAI
        // reads HP directly to shorten its cycle; this stays a hook for explicit phase effects
        // (new abilities, attack buffs) added with the other bosses in M6.
        private void RoundEnd(int round) { }

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
