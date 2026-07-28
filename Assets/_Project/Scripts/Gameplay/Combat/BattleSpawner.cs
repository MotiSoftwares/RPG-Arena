using UnityEngine;
using RPGArena.Core;
using RPGArena.Characters;
using RPGArena.Combat.AI;

namespace RPGArena.Combat
{
    // Builds runtime Entity combatants from content definitions, deep-copying their stats so
    // the shared assets are never mutated (§4.5). Reused by the real battle setup and by tests.
    public static class BattleSpawner
    {
        public static Entity SpawnHero(CharacterDefinition def, BalanceConfig cfg, AIBehavior brain = null, Transform parent = null)
        {
            var go = new GameObject($"Hero_{def.className}");
            if (parent) go.transform.SetParent(parent);
            var e = go.AddComponent<Entity>();
            e.displayName = def.className;
            e.team = Team.Heroes;
            e.isBoss = false;
            e.primaryStat = def.primaryStat;
            e.staggerThreshold = float.MaxValue;   // heroes don't stagger
            e.stageSprite = def.stageSprite;
            e.modelPrefab = def.modelPrefab;
            e.Initialize(def.baseStats, cfg, def.elementProfile, def.abilities, brain);
            return e;
        }

        // A boss's add: enemy-team, non-boss (no stagger/threat mechanics), carries its drop payout.
        public static Entity SpawnMinion(MinionDefinition def, BalanceConfig cfg, int index, Transform parent = null)
        {
            var go = new GameObject($"Minion_{def.displayName}_{index + 1}");
            if (parent) go.transform.SetParent(parent);
            var e = go.AddComponent<Entity>();
            e.displayName = def.displayName + (index > 0 ? $" {(char)('A' + index)}" : " A");
            e.team = Team.Enemies;
            e.isBoss = false;
            e.primaryStat = def.primaryStat;
            e.staggerThreshold = float.MaxValue;   // adds don't stagger
            e.modelPrefab = def.modelPrefab;
            e.goldDrop = def.goldDrop;
            e.Initialize(def.baseStats, cfg, def.elementProfile, def.abilities, def.aiBehavior);
            return e;
        }

        public static Entity SpawnBoss(BossDefinition def, BalanceConfig cfg, Transform parent = null)
        {
            var go = new GameObject($"Boss_{def.bossName}");
            if (parent) go.transform.SetParent(parent);
            var e = go.AddComponent<Entity>();
            e.displayName = def.bossName;
            e.team = Team.Enemies;
            e.isBoss = true;
            e.primaryStat = def.primaryStat;
            e.staggerThreshold = def.staggerThreshold;
            e.staggerDecayPerTurn = def.staggerDecayPerTurn;   // set BEFORE Initialize
            e.stageSprite = def.stageSprite;
            e.modelPrefab = def.modelPrefab;
            e.phases = def.phases;
            e.Initialize(def.baseStats, cfg, def.elementProfile, def.abilities, def.aiBehavior);
            return e;
        }
    }
}
