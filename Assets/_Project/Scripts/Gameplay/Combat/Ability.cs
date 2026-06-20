using UnityEngine;
using RPGArena.Combat.Status;

namespace RPGArena.Combat
{
    // What an ability fundamentally does. The engine builds a matching Command from this.
    public enum EffectType { Attack, MultiHit, Heal, Buff, Debuff, ApplyStatus, Stance, Defend, BossMove, Composite }

    // Who an ability can be aimed at.
    public enum TargetRule { SingleEnemy, AllEnemies, SingleAlly, AllAllies, Self, Summon }

    // Hit reliability tier for the informed-gamble RNG (CLAUDE.md Appendix E.1). Reliable
    // skills (basics, AoE, the Archer) effectively never miss; Risky ones (big nukes) can.
    public enum HitTier { Reliable, Standard, Risky }

    // DATA ONLY. An ability is a designer-authored ScriptableObject; its behaviour lives in
    // a Command (the Slay-the-Spire model, §4.3). Adding content never means new engine code.
    [CreateAssetMenu(menuName = "RPGArena/Ability", fileName = "Ability")]
    public class Ability : ScriptableObject
    {
        [Header("Identity")]
        public string displayName;
        [TextArea] public string description;
        public Sprite icon;

        [Header("Rules")]
        public EffectType effectType = EffectType.Attack;
        public TargetRule targetRule = TargetRule.SingleEnemy;
        public ElementType element = ElementType.Physical;
        [Tooltip("Uses MagicAttack instead of physical Attack for damage.")]
        public bool isMagic;
        [Tooltip("1.0 == 100% of the relevant offense stat.")]
        public float power = 1.0f;
        [Tooltip("Number of hits for MultiHit abilities.")]
        public int hits = 1;
        public int mpCost = 0;
        public int cooldown = 0;
        [Tooltip("Basic attacks restore a little MP so there is always a tempo choice.")]
        public int mpRegenOnUse = 0;

        [Header("RNG (Appendix E.1)")]
        public HitTier hitTier = HitTier.Reliable;
        [Tooltip("Healing and many AoE skills skip the accuracy roll entirely.")]
        public bool autoHit = false;

        [Header("Status / synergy")]
        public StatusEffectDefinition[] statusesToApply;

        [Header("Boss move")]
        [Tooltip("For a Charging/telegraph move: the attack it commits to next turn (e.g. Flame Breath).")]
        public Ability telegraphsAbility;

        [Header("Stat modifiers (Buff / Debuff)")]
        public int attackMod, defenseMod, speedMod;
        public float accuracyMod, evasionMod;
        public int buffDurationTurns = 3;

        [Header("Presentation (read by listeners, never by logic)")]
        public GameObject vfxPrefab;
        public string sfxId;
        public string animationTrigger = "Attack";

        [Header("Tags")]
        [Tooltip("e.g. BreakSkill, Ranged, Setup, Finisher")]
        public string[] tags;

        // Convenience: does this ability carry a given tag (case-insensitive)?
        public bool HasTag(string tag)
        {
            if (tags == null) return false;
            for (int i = 0; i < tags.Length; i++)
                if (string.Equals(tags[i], tag, System.StringComparison.OrdinalIgnoreCase)) return true;
            return false;
        }
    }
}
