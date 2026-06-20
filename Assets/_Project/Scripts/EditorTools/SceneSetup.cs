#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using RPGArena.Core;
using RPGArena.Core.Events;
using RPGArena.Characters;
using RPGArena.Combat;
using RPGArena.Combat.Events;
using RPGArena.UI;

namespace RPGArena.EditorTools
{
    // Wires the BattleArena scene reproducibly: drops a BattleController + BattleHUD and assigns
    // every content/channel reference by loading the authored assets, then saves the scene. Far
    // more reliable than hand-assigning ~20 serialized references via the MCP bridge, and it can
    // be re-run any time the wiring changes.
    public static class SceneSetup
    {
        private const string SO = "Assets/_Project/ScriptableObjects/";

        [MenuItem("RPGArena/Setup Battle Scene")]
        public static void SetupBattleArena()
        {
            var scene = EditorSceneManager.OpenScene("Assets/_Project/Scenes/BattleArena.unity", OpenSceneMode.Single);

            var balance = L<BalanceConfig>("Config/BalanceConfig.asset");
            var dragon = L<BossDefinition>("Bosses/Dragon.asset");
            var roster = new List<CharacterDefinition>
            {
                L<CharacterDefinition>("Characters/Warrior.asset"),
                L<CharacterDefinition>("Characters/Mage.asset"),
                L<CharacterDefinition>("Characters/Thief.asset"),
                L<CharacterDefinition>("Characters/Archer.asset"),
            };
            var cStarted = L<VoidChannel>("Events/OnBattleStarted.asset");
            var cWon = L<VoidChannel>("Events/OnBattleWon.asset");
            var cLost = L<VoidChannel>("Events/OnBattleLost.asset");
            var cTurnS = L<EntityChannel>("Events/OnTurnStarted.asset");
            var cTurnE = L<EntityChannel>("Events/OnTurnEnded.asset");
            var cDied = L<EntityChannel>("Events/OnEntityDied.asset");
            var cBreak = L<EntityChannel>("Events/OnStaggerBroken.asset");
            var cDmg = L<DamageResultChannel>("Events/OnDamageDealt.asset");
            var cTele = L<AbilityChannel>("Events/OnBossTelegraph.asset");

            DestroyIfExists("BattleSystem");
            DestroyIfExists("BattleHUD");

            var sysGo = new GameObject("BattleSystem");
            var ctrl = sysGo.AddComponent<BattleController>();
            ctrl.balance = balance; ctrl.boss = dragon; ctrl.roster = roster;
            ctrl.onBattleStarted = cStarted; ctrl.onBattleWon = cWon; ctrl.onBattleLost = cLost;
            ctrl.onTurnStarted = cTurnS; ctrl.onTurnEnded = cTurnE; ctrl.onEntityDied = cDied;
            ctrl.onStaggerBroken = cBreak; ctrl.onDamageDealt = cDmg; ctrl.onBossTelegraph = cTele;

            var hudGo = new GameObject("BattleHUD");
            var hud = hudGo.AddComponent<BattleHUD>();
            hud.controller = ctrl;
            hud.onTurnStarted = cTurnS; hud.onStaggerBroken = cBreak; hud.onEntityDied = cDied;
            hud.onDamageDealt = cDmg; hud.onBossTelegraph = cTele;
            hud.onBattleWon = cWon; hud.onBattleLost = cLost;

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("[SceneSetup] BattleArena wired: BattleController + BattleHUD + 9 channels + content.");
        }

        private static T L<T>(string rel) where T : Object
        {
            var a = AssetDatabase.LoadAssetAtPath<T>(SO + rel);
            if (a == null) Debug.LogError($"[SceneSetup] Missing asset: {SO + rel}");
            return a;
        }

        private static void DestroyIfExists(string name)
        {
            var go = GameObject.Find(name);
            if (go) Object.DestroyImmediate(go);
        }
    }
}
#endif
