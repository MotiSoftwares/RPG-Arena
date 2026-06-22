using UnityEngine;

namespace RPGArena.Core
{
    // One ScriptableObject holding every global tuning constant, so the whole game can
    // be balanced from the Inspector instead of by editing code. No magic numbers live
    // in logic — they all surface here (CLAUDE.md §3.4 "no magic numbers", §A.2).
    [CreateAssetMenu(menuName = "RPGArena/Balance Config", fileName = "BalanceConfig")]
    public class BalanceConfig : ScriptableObject
    {
        [Header("Element multipliers")]
        // How far a weakness or resistance bends incoming damage (×1.5 / ×0.5 default).
        public float weakMult = 1.5f;
        public float resistMult = 0.5f;

        [Header("Defense mitigation")]
        // Percentage mitigation = Defense / (Defense + K): soft, diminishing returns
        // so stacking defense never reaches full immunity.
        public float defenseK = 100f;

        [Header("Stagger / Break")]
        // Damage multiplier applied while a target is Broken — the burst window.
        public float staggerDamageMult = 1.85f;
        // Different hit kinds add different amounts to the stagger meter.
        public float staggerBuildNormalHit = 8f;
        public float staggerBuildWeaknessHit = 20f;
        public float staggerBuildBreakSkill = 30f;

        [Header("Action economy")]
        // Hard cap so weakness/crit "1 More" turns can never loop forever.
        public int maxExtraTurnsPerEntityPerRound = 1;

        [Header("Stat derivation constants (k1..k8)")]
        // Convert primary stats (STR/DEX/INT/LUK) into derived combat stats (§5.2).
        public float k1 = 2f, k2 = 2.2f, k3 = 0.5f, k4 = 1f, k5 = 1f, k6 = 1f, k7 = 0.5f, k8 = 0.4f;

        [Header("RNG — informed-gamble layer (CLAUDE.md Appendix E.1)")]
        // Final damage is multiplied by a random value in this band, so every hit
        // visibly "rolls" weak..strong; a crit pushes the roll to the top of the band.
        public float damageVarianceMin = 0.85f;
        public float damageVarianceMax = 1.15f;

        // Base hit chance per ability reliability tier, before accuracy/evasion are added.
        public float reliableHitBase = 0.99f;
        public float standardHitBase = 0.90f;
        public float riskyHitBase = 0.84f;

        // Hit chance is always clamped to this window so non-reliable skills are never a
        // guaranteed miss, and risky skills are never a guaranteed hit.
        public float hitFloor = 0.50f;
        public float hitCeiling = 0.99f;

        // Anti-feel-bad: after this many misses in a row, the next attack is forced to hit.
        public int missStreakCap = 2;
        [Tooltip("Each point of (Accuracy - Evasion) shifts hit chance by this. Shared by the pipeline AND the HUD preview so they never drift.")]
        public float accuracyToPercent = 0.01f;
        [Tooltip("Incoming damage multiplier while the target is Defending.")]
        public float defendDamageMult = 0.5f;
    }
}
