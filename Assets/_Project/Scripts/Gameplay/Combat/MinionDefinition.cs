using System.Collections.Generic;
using UnityEngine;
using RPGArena.Characters;
using RPGArena.Combat.AI;

namespace RPGArena.Combat
{
    // A boss's add: a small enemy spawned alongside it in LIVE battles only (the headless
    // balance tests stay a pure trio-vs-boss so the PartyTrio gate keeps meaning). Minions
    // take real turns via their AI brain (SequenceAI gives them the authored attack rotation),
    // die without ending the battle, and pay out gold/item drops.
    [CreateAssetMenu(menuName = "RPGArena/Minion Definition")]
    public class MinionDefinition : ScriptableObject
    {
        [Header("Identity")]
        public string displayName = "Minion";
        public PrimaryStat primaryStat = PrimaryStat.STR;

        [Header("Combat")]
        public StatBlock baseStats = new();
        public List<Ability> abilities = new();
        public ElementProfile elementProfile;
        public AIBehavior aiBehavior;

        [Header("Presentation")]
        public GameObject modelPrefab;
        [Tooltip("Auto-scale the model to this world height (0 = keep native size).")]
        public float modelHeight = 2.2f;
        [Tooltip("Optional controller override when the model prefab's own animator doesn't match the battle triggers.")]
        public RuntimeAnimatorController animatorOverride;

        [Header("Drops")]
        public int goldDrop = 25;
        public ItemDefinition itemDrop;
        [Range(0f, 1f)] public float itemDropChance = 0.25f;
    }
}
