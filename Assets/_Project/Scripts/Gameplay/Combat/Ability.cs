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

    // What a SPECIAL skill's risk-die does on a low (Backfire) roll: hurt the caster, fizzle to
    // ~no damage (MP already spent), or embolden the boss. Only abilities with rollsRiskDie use it.
    public enum BackfireKind { None, SelfRecoil, Fizzle, EmboldenBoss }

    // What a Stance ability does when used (§5.9). The two real stance systems after the
    // E.3 trim are the Mage's attunement cycle and the Warrior's Berserk/Guardian toggle.
    public enum StanceAction { None, CycleAttunement, ToggleStatus }

    // Where a non-projectile vfxPrefab is planted on its target. Purely presentational — logic never
    // reads it. Body is the default because it is the flashier read and it is what most effects want;
    // Ground exists for prefabs authored to erupt from the floor, which look absurd hovering at a
    // 9.8u dragon's chest.
    public enum VfxAnchor { Body, Ground }

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
        [Tooltip("Flat amount added on top of the stat-scaled result (0 for every hero skill). Lets ITEM abilities heal a fixed amount no matter who drinks them.")]
        public int flatPower = 0;

        [Header("RNG (Appendix E.1)")]
        public HitTier hitTier = HitTier.Reliable;
        [Tooltip("Healing and many AoE skills skip the accuracy roll entirely.")]
        public bool autoHit = false;

        [Header("Risk die (the SPECIAL skill — a visible d20 gamble)")]
        [Tooltip("This (and only this) ability rolls the risk die: low = backfire/whiff, high = big/jackpot.")]
        public bool rollsRiskDie = false;
        [Tooltip("Clamp the risk roll to at least this (0..1). A higher floor = a SAFER special that rarely backfires — e.g. the Archer's reliable Arrow Rain closer.")]
        [Range(0f, 1f)] public float riskFloor = 0f;
        [Tooltip("What a Backfire (low roll) does to the caster.")]
        public BackfireKind backfireKind = BackfireKind.None;
        [Tooltip("EmboldenBoss backfire: the buff applied to the boss on a backfire.")]
        public StatusEffectDefinition backfireStatus;

        [Header("Status / synergy")]
        public StatusEffectDefinition[] statusesToApply;

        [Header("Boss move")]
        [Tooltip("For a Charging/telegraph move: the attack it commits to next turn (e.g. Flame Breath).")]
        public Ability telegraphsAbility;

        [Tooltip("DEVOUR: this move EATS the setup flags (Wet/Oiled/Marked) on its target, healing and enraging the caster per flag eaten. Frozen is deliberately immune - it is the party's escape hatch.")]
        public bool consumesSetupFlags = false;
        [Range(0f, 0.25f)] public float devourHealPercentMaxHP = 0.04f;
        public int devourFuryPerFlag = 2;

        [Header("Stance / attunement (effectType = Stance, §5.9)")]
        public StanceAction stanceAction = StanceAction.None;
        [Tooltip("CycleAttunement: the elemental schools to rotate through (Mage).")]
        public ElementType[] attunementOptions;
        [Tooltip("ToggleStatus: the mutually-exclusive stance statuses to rotate (Warrior Berserk/Guardian).")]
        public StatusEffectDefinition[] stanceStatuses;
        [Tooltip("Basics that follow the caster's current attunement (Mage's Magic Bolt).")]
        public bool followsAttunement;

        [Header("Stat modifiers (Buff / Debuff)")]
        public int attackMod, defenseMod, speedMod;
        public float accuracyMod, evasionMod;
        public int buffDurationTurns = 3;

        [Header("Advanced combat (finishers / piercing, §6)")]
        [Tooltip("Soul Arrow etc.: ignore this fraction of the target's Defense (0..1).")]
        [Range(0f, 1f)] public float ignoreDefensePercent = 0f;
        [Tooltip("Assassinate etc.: this attack always crits.")]
        public bool guaranteedCrit = false;
        [Tooltip("Finisher: bonus multiplier when the target carries this flag (None = off).")]
        public StatusFlag bonusVsFlag = StatusFlag.None;
        public float bonusVsFlagMult = 1.5f;
        [Tooltip("Execute: bonus multiplier when the target is at/below this HP fraction (0 = off).")]
        [Range(0f, 1f)] public float executeBelowHpPct = 0f;
        public float executeMult = 1.6f;

        [Header("Presentation (read by listeners, never by logic)")]
        public GameObject vfxPrefab;
        [Tooltip("The vfxPrefab is a directional projectile: spawn it at the CASTER and fly it to the target (instead of blooming it on the target).")]
        public bool vfxIsProjectile;
        [Tooltip("Where a NON-projectile vfxPrefab is planted. Body = on the target's torso, pulled toward the camera so the mesh cannot swallow it — the right choice for impacts, explosions and bursts, and the flashier read. Ground = at the target's feet, for effects authored to erupt from or lie on the floor (magic circles, spikes, meteor/spear rain); those look absurd floating at chest height on a tall boss.")]
        public VfxAnchor vfxAnchor = VfxAnchor.Body;
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

#if UNITY_EDITOR
        // Warn on common authoring mistakes so a bad asset is caught in the Inspector (§3.4).
        private void OnValidate()
        {
            if (effectType == EffectType.BossMove && telegraphsAbility == null && (statusesToApply == null || statusesToApply.Length == 0))
                Debug.LogWarning($"[{name}] BossMove '{displayName}' does nothing: no telegraphsAbility and no statuses.", this);
            if (effectType == EffectType.Stance && stanceAction == StanceAction.CycleAttunement && (attunementOptions == null || attunementOptions.Length == 0))
                Debug.LogWarning($"[{name}] CycleAttunement '{displayName}' has no attunementOptions.", this);
            if (effectType == EffectType.Stance && stanceAction == StanceAction.ToggleStatus && (stanceStatuses == null || stanceStatuses.Length < 2))
                Debug.LogWarning($"[{name}] ToggleStatus '{displayName}' needs >=2 stanceStatuses to toggle.", this);
        }
#endif
    }
}
