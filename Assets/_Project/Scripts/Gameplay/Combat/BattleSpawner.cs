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
            e.Initialize(def.baseStats, cfg, def.elementProfile, def.abilities, brain);
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
            e.stageSprite = def.stageSprite;
            e.phases = def.phases;
            e.Initialize(def.baseStats, cfg, def.elementProfile, def.abilities, def.aiBehavior);
            return e;
        }
    }
}
