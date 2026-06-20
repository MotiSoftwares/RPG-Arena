using System.Collections.Generic;
using UnityEngine;
using RPGArena.Combat;

namespace RPGArena.Characters
{
    // DATA ONLY: one ScriptableObject per hero class (§4.5). Spawning an Entity copies these
    // base stats so the runtime combatant never mutates the shared asset.
    [CreateAssetMenu(menuName = "RPGArena/Character Definition", fileName = "Character")]
    public class CharacterDefinition : ScriptableObject
    {
        [Header("Identity")]
        public string className = "Hero";
        [TextArea] public string classFlavor;
        public Sprite portrait;
        public GameObject modelPrefab;
        public PrimaryStat primaryStat = PrimaryStat.STR;

        [Header("Stats & kit")]
        public StatBlock baseStats = new();
        public List<Ability> abilities = new();
        public ElementProfile elementProfile;
    }
}
