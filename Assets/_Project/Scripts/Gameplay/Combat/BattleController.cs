using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using RPGArena.Core;
using RPGArena.Core.Events;
using RPGArena.Characters;
using RPGArena.Combat.Commands;
using RPGArena.Combat.Events;

namespace RPGArena.Combat
{
    // The live, real-time battle driver for the playable game (the §4.6 FSM realised as a
    // coroutine). It reuses the M1 combat systems but, on a hero's turn, AWAITS player input
    // submitted by the HUD; the boss uses its Strategy AI brain. It raises the ScriptableObject
    // event channels the HUD/audio/juice subscribe to, and never reaches into presentation
    // directly. (The synchronous BattleManager remains for headless tests.)
    public class BattleController : MonoBehaviour
    {
        [Header("Content")]
        public BalanceConfig balance;
        public BossDefinition boss;
        public List<CharacterDefinition> roster = new();              // all selectable classes
        public List<string> defaultParty = new() { "Warrior", "Mage", "Thief" };

        [Header("Event channels (presentation subscribes)")]
        public VoidChannel onBattleStarted, onBattleWon, onBattleLost;
        public EntityChannel onTurnStarted, onTurnEnded, onEntityDied, onStaggerBroken;
        public DamageResultChannel onDamageDealt;
        public AbilityChannel onBossTelegraph;

        [Header("Pacing")]
        public float actionDelay = 0.55f;

        public BattleContext Context { get; private set; }
        public Entity ActiveHero { get; private set; }                // whose input we await (null otherwise)
        public bool AwaitingInput => ActiveHero != null && pendingAction == null;
        public BattleManager.Outcome Result { get; private set; } = BattleManager.Outcome.InProgress;

        private ActionRequest? pendingAction;

        private void Start() => StartCoroutine(RunBattle());

        // The HUD calls this when the active hero chooses an ability + target.
        public void SubmitAction(Ability ability, Entity target)
        {
            if (ActiveHero == null || ability == null) return;
            pendingAction = new ActionRequest(ability, ActiveHero, ResolveTargets(ActiveHero, ability, target));
        }

        // Convenience for a "pass/defend with no target" action.
        public void SubmitAction(Ability ability) => SubmitAction(ability, null);

        private IEnumerator RunBattle()
        {
            BuildContext();
            Context.Log($"=== {boss.bossName} appears! ===");
            onBattleStarted?.Raise();
            yield return Wait();

            for (int round = 1; round <= 60 && Result == BattleManager.Outcome.InProgress; round++)
            {
                var order = Context.turns.BuildRoundOrder(All(), Context.rng, balance.maxExtraTurnsPerEntityPerRound);
                while (order.Count > 0 && Result == BattleManager.Outcome.InProgress)
                {
                    var actor = order.Dequeue();
                    if (actor == null || !actor.IsAlive) continue;

                    int dot = actor.TickStartOfTurn();
                    if (dot > 0) Context.Log($"{actor.displayName} takes {dot} damage over time.");
                    onTurnStarted?.Raise(actor);

                    if (actor.CanAct)
                    {
                        ICommand cmd = null;

                        if (actor.Brain != null)
                        {
                            // AI-controlled (the boss).
                            var opponents = actor.team == Team.Heroes ? new List<Entity> { Context.boss } : Context.heroes;
                            var ability = actor.Brain.DecideAction(Context, actor, opponents, out var tgt);
                            if (ability != null)
                                cmd = CommandFactory.Build(new ActionRequest(ability, actor, ResolveTargets(actor, ability, tgt)));
                        }
                        else
                        {
                            // Player hero: open the action menu and wait for the HUD to submit.
                            ActiveHero = actor;
                            pendingAction = null;
                            while (pendingAction == null) yield return null;
                            cmd = CommandFactory.Build(pendingAction.Value);
                            ActiveHero = null;
                            pendingAction = null;
                        }

                        if (cmd != null)
                        {
                            Context.lastActionResults.Clear();
                            Context.Log(cmd.DescribeForLog());
                            cmd.Resolve(Context);

                            // Action economy: a weakness hit or crit grants one capped bonus turn.
                            foreach (var r in Context.lastActionResults)
                                if (r.hit && (r.reaction == ElementReaction.Weak || r.crit))
                                { Context.turns.TryGrantExtraTurn(actor, order); break; }

                            yield return Wait();
                        }
                    }
                    else
                    {
                        Context.Log($"{actor.displayName} is frozen/staggered — turn skipped.");
                        yield return Wait();
                    }

                    actor.TickEndOfTurn();
                    onTurnEnded?.Raise(actor);
                    CheckDeaths();
                    Result = Evaluate();
                }
            }

            if (Result == BattleManager.Outcome.Victory) { Context.Log($"=== VICTORY! {boss.bossName} is slain. ==="); onBattleWon?.Raise(); }
            else { Context.Log("=== DEFEAT. The party has fallen. ==="); onBattleLost?.Raise(); }
        }

        // --- setup --------------------------------------------------------------------
        private void BuildContext()
        {
            Context = new BattleContext
            {
                balance = balance,
                rng = new System.Random(),
                echoToConsole = true,
                BossStaggeredTurns = boss.staggeredTurns,
                damage = new DamagePipeline(balance, new System.Random()),
                stagger = new StaggerSystem(),
                turns = new TurnSystem(),
                onBattleStarted = onBattleStarted, onBattleWon = onBattleWon, onBattleLost = onBattleLost,
                onTurnStarted = onTurnStarted, onTurnEnded = onTurnEnded, onEntityDied = onEntityDied,
                onStaggerBroken = onStaggerBroken, onDamageDealt = onDamageDealt, onBossTelegraph = onBossTelegraph
            };

            // Party: from the run state's selection, else the default trio.
            var chosen = GameBootstrap.Instance?.Run?.partyClassNames;
            if (chosen == null || chosen.Count == 0) chosen = defaultParty;
            foreach (var className in chosen)
            {
                var def = roster.Find(c => c != null && c.className == className);
                if (def != null) Context.heroes.Add(BattleSpawner.SpawnHero(def, balance, null)); // null brain => player
            }
            Context.boss = BattleSpawner.SpawnBoss(boss, balance);

            PlaceCombatants();
        }

        // Stage the combatants on the orthographic arena: heroes on the left facing the boss
        // on the right. Each gets a placeholder capsule "body" (swapped for real rigged models
        // when those are imported) so the fight is visible. Colours distinguish the classes.
        private static readonly Color[] HeroPalette =
        {
            new Color(0.85f, 0.3f, 0.3f), new Color(0.3f, 0.5f, 0.9f),
            new Color(0.6f, 0.35f, 0.8f), new Color(0.35f, 0.75f, 0.4f)
        };

        private void PlaceCombatants()
        {
            for (int i = 0; i < Context.heroes.Count; i++)
            {
                var h = Context.heroes[i];
                h.transform.position = new Vector3(-5f + i * 1.7f, 0f, i * 0.4f);
                h.transform.rotation = Quaternion.Euler(0, 90, 0);
                AttachBody(h.gameObject, HeroPalette[i % HeroPalette.Length], 1f, 1.9f);
            }
            if (Context.boss != null)
            {
                Context.boss.transform.position = new Vector3(4.5f, 0f, 0.6f);
                Context.boss.transform.rotation = Quaternion.Euler(0, -90, 0);
                AttachBody(Context.boss.gameObject, new Color(0.5f, 0.12f, 0.12f), 2.3f, 3.4f);
            }
        }

        private static void AttachBody(GameObject host, Color color, float width, float height)
        {
            if (host.transform.Find("Body") != null) return;
            var body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            body.name = "Body";
            var col = body.GetComponent<Collider>(); if (col) Destroy(col);
            body.transform.SetParent(host.transform, false);
            body.transform.localScale = new Vector3(width, height * 0.5f, width);
            body.transform.localPosition = new Vector3(0, height * 0.5f, 0);
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader != null)
            {
                var mat = new Material(shader);
                if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color);
                body.GetComponent<MeshRenderer>().sharedMaterial = mat;
            }
        }

        // --- helpers ------------------------------------------------------------------
        private Entity[] ResolveTargets(Entity actor, Ability ability, Entity single)
        {
            var opponents = actor.team == Team.Heroes ? new List<Entity> { Context.boss } : Context.heroes;
            switch (ability.targetRule)
            {
                case TargetRule.AllEnemies: return TargetingSystem.AllAlive(opponents).ToArray();
                case TargetRule.AllAllies:
                    var allies = actor.team == Team.Heroes ? Context.heroes : new List<Entity> { Context.boss };
                    return TargetingSystem.AllAlive(allies).ToArray();
                case TargetRule.Self: return new[] { actor };
                case TargetRule.SingleAlly: return new[] { single != null ? single : actor };
                default:
                    var t = single != null ? single : TargetingSystem.FirstAlive(opponents);
                    return t != null ? new[] { t } : new Entity[0];
            }
        }

        private List<Entity> All()
        {
            var all = new List<Entity>(Context.heroes);
            if (Context.boss != null) all.Add(Context.boss);
            return all;
        }

        private readonly HashSet<Entity> announced = new();
        private void CheckDeaths()
        {
            foreach (var e in All())
                if (!e.IsAlive && announced.Add(e)) { Context.Log($"{e.displayName} has fallen."); onEntityDied?.Raise(e); }
        }

        private BattleManager.Outcome Evaluate()
        {
            if (Context.BossDead) return BattleManager.Outcome.Victory;
            if (Context.AllHeroesDead) return BattleManager.Outcome.Defeat;
            return BattleManager.Outcome.InProgress;
        }

        private WaitForSeconds Wait() => new WaitForSeconds(actionDelay);
    }
}
