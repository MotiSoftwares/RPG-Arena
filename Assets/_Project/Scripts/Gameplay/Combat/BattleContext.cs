using System.Collections.Generic;
using RPGArena.Characters;
using RPGArena.Core;
using RPGArena.Core.Events;
using RPGArena.Combat.Events;

namespace RPGArena.Combat
{
    // Shared services + state handed to FSM states, Commands, and AI brains. Passing this
    // around avoids the static singletons / mutable globals the rubric warns about (§4.6).
    public class BattleContext
    {
        // Combatants.
        public readonly List<Entity> heroes = new();
        public Entity boss;
        // The boss's adds (live battles only; headless tests leave this empty). Victory is still
        // "boss dead" — surviving minions flee with their master slain.
        public readonly List<Entity> minions = new();

        // Boss + living minions: the hero-side target pool.
        public List<Entity> Enemies
        {
            get
            {
                var list = new List<Entity>();
                if (boss != null) list.Add(boss);
                foreach (var m in minions) if (m != null && m.IsAlive) list.Add(m);
                return list;
            }
        }

        // Systems & tuning.
        public BalanceConfig balance;
        public DamagePipeline damage;
        public StaggerSystem stagger;
        public TurnSystem turns;
        public ChargeSystem charge;        // party Valor / Overdrive (null in headless tests — every hook null-guards)
        public System.Random rng;
        public int BossStaggeredTurns = 1;

        // Optional event channels — presentation subscribes; null is fine in headless tests.
        public DamageResultChannel onDamageDealt;
        public EntityChannel onTurnStarted, onTurnEnded, onEntityDied, onStaggerBroken;
        public AbilityChannel onBossTelegraph;
        public VoidChannel onBattleStarted, onBattleWon, onBattleLost;

        // Results of the most recent action — the FSM reads these to award extra turns
        // (weakness/crit) and to check for deaths. Cleared before each action resolves.
        public readonly List<DamageResult> lastActionResults = new();

        // Narrative/HUD hints (§13): set by the Ink "study" choice or the first weakness hit so
        // the HUD can reveal the boss's weakness; bossEverBroken feeds the outro text.
        public bool weaknessRevealed;
        public bool bossEverBroken;

        // Plain-English combat log: doubles as the headless trace and the combat-log UI feed.
        public readonly List<string> log = new();
        public bool echoToConsole = true;

        public void Log(string line)
        {
            log.Add(line);
            if (echoToConsole) UnityEngine.Debug.Log(line);
        }

        // Event raisers (null-safe) so core never touches presentation directly.
        public void RaiseDamage(DamageResult r) => onDamageDealt?.Raise(r);
        public void RaiseStaggerBroken(Entity e) => onStaggerBroken?.Raise(e);
        public void RaiseTurnStarted(Entity e) => onTurnStarted?.Raise(e);
        public void RaiseTurnEnded(Entity e) => onTurnEnded?.Raise(e);
        public void RaiseEntityDied(Entity e) => onEntityDied?.Raise(e);
        public void RaiseTelegraph(Ability a) => onBossTelegraph?.Raise(a);

        // Convenience queries used by the FSM and AI.
        public List<Entity> LivingHeroes => TargetingSystem.AllAlive(heroes);
        public bool AllHeroesDead { get { foreach (var h in heroes) if (h.IsAlive) return false; return true; } }
        public bool BossDead => boss == null || !boss.IsAlive;
    }
}
