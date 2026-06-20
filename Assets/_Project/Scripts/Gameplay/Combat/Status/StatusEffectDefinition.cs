using UnityEngine;

namespace RPGArena.Combat.Status
{
    // The category of a status, which drives how the systems treat it.
    public enum StatusKind { Buff, Debuff, DoT, Flag, Control }

    // Synergy/flag identities. TRIMMED per CLAUDE.md Appendix E.3: Bleed is dropped and
    // Stun is merged into Frozen (one skip-turn control). Flags (Wet/Oiled/Marked) are the
    // setup that one class applies and another detonates (§4.12).
    public enum StatusFlag { None, Wet, Oiled, Marked, Frozen, Defending, Stealthed, Weaken, MagicGuard, Taunting }

    // DATA ONLY. A status effect is a designer-authored asset; the runtime container applies
    // and ticks it (§4.10). The synergy resolver reads its flag.
    [CreateAssetMenu(menuName = "RPGArena/Status Effect", fileName = "StatusEffect")]
    public class StatusEffectDefinition : ScriptableObject
    {
        [Header("Identity")]
        public string displayName;
        public Sprite icon;
        public Color tint = Color.white;          // colour-codes the floating numbers / icon

        [Header("Behaviour")]
        public StatusKind kind = StatusKind.Buff;
        public StatusFlag flag = StatusFlag.None;
        public int durationTurns = 3;
        public bool stacks;
        public int maxStacks = 1;

        [Header("Damage over time (applied at turn start)")]
        [Tooltip("Burn: a fraction of MAX HP each turn.")]
        public float perTurnPercentMaxHP;
        [Tooltip("Poison: a flat amount each turn.")]
        public int perTurnFlatDamage;
        public ElementType dotElement = ElementType.Physical;

        [Header("Stat modifiers while active")]
        public int attackMod, defenseMod, speedMod;
        public float accuracyMod, evasionMod;

        [Header("Control")]
        [Tooltip("Frozen: the owner loses its next turn.")]
        public bool skipsTurn;
    }
}
