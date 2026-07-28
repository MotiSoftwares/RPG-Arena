using System.Collections.Generic;
using UnityEngine;
using RPGArena.Characters;
using RPGArena.Combat.Status;

namespace RPGArena.Combat.AI
{
    // THE BLACK MAGE — the anti-setup boss, and the roster's answer to "boss 1 taught me everything".
    //
    // The Dragon rewards stockpiling: soak it, freeze it, shatter it. This one INVERTS that habit.
    // The moment the party has two setup flags on him he DEVOURS them, healing and stoking his own
    // Fury on exactly the preparation that beats the Dragon. Players who autopilot the Dragon's
    // rhythm feed him; players who notice must change plan — spend each setup the turn it lands,
    // or freeze him instead (Frozen is deliberately inedible, so it stays a real escape hatch).
    //
    // Devour is checked BEFORE any rng draw so PreviewIntent can promise it without desyncing the
    // seeded headless runs — the player is always warned before being punished.
    [CreateAssetMenu(menuName = "RPGArena/AI/Devourer AI", fileName = "DevourerAI")]
    public class DevourerAI : AIBehavior
    {
        [Header("Kit")]
        public Ability darkBolt;
        public Ability darkNova;
        public Ability curse;
        public Ability hex;
        public Ability channel;      // telegraphed heavy
        public Ability oblivion;     // what channel commits to
        public Ability devour;

        [Header("Tuning")]
        [Range(0f, 1f)] public float phase2HpFraction = 0.5f;
        [Range(0f, 1f)] public float channelChance = 0.28f;
        [Tooltip("How many setup flags must be on him before he stops to eat them.")]
        public int devourMinFlags = 2;

        // How many edible setups are currently on this entity. Public so the HUD can warn the
        // player BEFORE they hand him a third course.
        public static int DevourableCount(Entity e)
        {
            if (e == null || e.Status == null) return 0;
            int n = 0;
            if (e.Status.Has(StatusFlag.Wet)) n++;
            if (e.Status.Has(StatusFlag.Oiled)) n++;
            if (e.Status.Has(StatusFlag.Marked)) n++;
            return n;
        }

        public override Ability PreviewIntent(BattleContext ctx, Entity self, IReadOnlyList<Entity> opponents)
        {
            if (self.telegraphedAbility != null) return self.telegraphedAbility;
            if (devour != null && DevourableCount(self) >= devourMinFlags) return devour;
            return null;   // the rest of his book is genuinely random — "???" is the honest answer
        }

        public override Ability DecideAction(BattleContext ctx, Entity self,
                                             IReadOnlyList<Entity> opponents, out Entity target)
        {
            target = null;

            // 1) Committed telegraph fires.
            if (self.telegraphedAbility != null)
            {
                var commit = self.telegraphedAbility;
                self.telegraphedAbility = null;
                return commit;
            }

            // 2) EAT — before any rng draw, so the preview above can never lie.
            if (devour != null && DevourableCount(self) >= devourMinFlags)
                return devour;

            // 3) Otherwise an unpredictable book of dark magic. Unlike the Dragon's readable
            //    rotation, you cannot plan around him — you can only deny him the meal.
            float hpFrac = self.stats.maxHP > 0 ? (float)self.currentHP / self.stats.maxHP : 1f;
            double r = ctx.rng.NextDouble();

            if (hpFrac <= phase2HpFraction && channel != null && r < channelChance)
                return channel;
            if (curse != null && r < 0.50) { target = PickVictim(self, opponents, ctx); return curse; }
            if (darkNova != null && r < 0.72) return darkNova;
            if (darkBolt != null && r < 0.90) { target = PickVictim(self, opponents, ctx); return darkBolt; }
            if (hex != null) { target = PickVictim(self, opponents, ctx); return hex; }

            target = PickVictim(self, opponents, ctx);
            return darkBolt != null ? darkBolt : darkNova;
        }

        private static Entity PickVictim(Entity self, IReadOnlyList<Entity> heroes, BattleContext ctx)
        {
            var pickable = new List<Entity>();
            foreach (var h in heroes)
                if (h != null && h.IsAlive && !h.Status.Has(StatusFlag.Stealthed)) pickable.Add(h);
            if (pickable.Count == 0) return TargetingSystem.RandomAlive(heroes, ctx.rng);
            // He hunts the caster: the lowest-MP hero is the one who has been spending, so pressure
            // lands on whoever is driving the party's combo engine.
            var taunters = new List<Entity>();
            foreach (var h in pickable) if (h.Status.Has(StatusFlag.Taunting)) taunters.Add(h);
            if (taunters.Count > 0) pickable = taunters;
            if (ctx.rng.NextDouble() < 0.30) return pickable[ctx.rng.Next(pickable.Count)];
            Entity best = pickable[0];
            foreach (var h in pickable) if (h.stats.maxMP > 0 && h.currentMP < best.currentMP) best = h;
            return best;
        }
    }
}
