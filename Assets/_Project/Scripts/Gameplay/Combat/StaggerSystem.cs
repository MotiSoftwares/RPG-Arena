using RPGArena.Characters;

namespace RPGArena.Combat
{
    // The signature Break mechanic (§4.9). Pressure fills a boss's meter; on fill it BREAKS:
    // it loses turns and takes a big damage multiplier (applied in the pipeline). Breaking the
    // Dragon mid-charge cancels Flame Breath — the key skill expression (§7.2).
    public class StaggerSystem
    {
        // Add stagger to a boss; cross the threshold => Break. Ignored if already staggered.
        public void Build(Entity boss, float amount, BattleContext ctx)
        {
            if (boss == null || !boss.isBoss || boss.isStaggered || amount <= 0f) return;
            boss.staggerMeter += amount;
            ctx.Log($"    stagger +{amount:0} ({boss.staggerMeter:0}/{boss.staggerThreshold:0})");
            if (boss.staggerMeter >= boss.staggerThreshold)
                Break(boss, ctx);
        }

        public void Break(Entity boss, BattleContext ctx)
        {
            if (boss == null || boss.isStaggered) return;
            boss.isStaggered = true;
            boss.staggeredTurnsRemaining = ctx.BossStaggeredTurns;
            boss.staggerMeter = boss.staggerThreshold;
            ctx.bossEverBroken = true;      // remembered for the narrative outro
            ctx.Log($"    *** BREAK! {boss.displayName} is staggered for {boss.staggeredTurnsRemaining} turn(s) ***");

            // The Break VENTS the dragon's Searing Fury — the whole point of building the meter is to
            // dump its escalating damage back to zero (plus the stagger burst window). Ignore the meter
            // and the Fury runs away; this is why spamming basics (which barely build stagger) loses.
            if (boss.rageStacks > 0)
            {
                ctx.Log($"    >>> the Break VENTS {boss.displayName}'s Fury ({boss.rageStacks} stacks)! <<<");
                boss.rageStacks = 0;
            }

            // The signature payoff: breaking the boss mid-charge cancels its telegraphed
            // attack (e.g. the Dragon's Flame Breath fizzles out, §7.2).
            if (boss.telegraphedAbility != null)
            {
                ctx.Log($"    >>> {boss.telegraphedAbility.displayName} CANCELLED by the Break! <<<");
                boss.telegraphedAbility = null;
            }

            ctx.RaiseStaggerBroken(boss);
        }
    }
}
