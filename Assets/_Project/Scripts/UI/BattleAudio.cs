using UnityEngine;
using RPGArena.Core;
using RPGArena.Characters;
using RPGArena.Combat;
using RPGArena.Combat.Events;

namespace RPGArena.UI
{
    // Presentation-side bridge from combat events to the audio service. It lives in UI (which
    // already depends on Gameplay), so the Audio assembly never needs a Gameplay reference. It
    // translates each result into an SFX id and starts the battle music; the AudioManager routes
    // everything through the AudioMixer.
    public class BattleAudio : MonoBehaviour
    {
        [Header("Channels (subscribed)")]
        public DamageResultChannel onDamageDealt;
        public EntityChannel onStaggerBroken;
        public AbilityChannel onBossTelegraph;
        public Core.Events.VoidChannel onBattleStarted, onBattleWon, onBattleLost;

        private IAudioService Audio => GameBootstrap.Instance != null ? GameBootstrap.Instance.Audio : null;

        // Mirror of JuiceController.AttackBeat's shared impact timeline so SFX land on the VISIBLE
        // contact (not ~0.2s early on the raw event) and multi-hits play one-per-beat, not as a stack.
        private static float nextAudioBeat;

        private void OnEnable()
        {
            onDamageDealt?.Subscribe(OnDamage);
            onStaggerBroken?.Subscribe(OnBreak);
            onBossTelegraph?.Subscribe(OnTelegraph);
            onBattleStarted?.Subscribe(OnStart);
            onBattleWon?.Subscribe(_ => Audio?.PlaySfx("victory"));
            onBattleLost?.Subscribe(_ => Audio?.PlaySfx("defeat"));
        }

        private void OnDisable()
        {
            onDamageDealt?.Unsubscribe(OnDamage);
            onStaggerBroken?.Unsubscribe(OnBreak);
            onBossTelegraph?.Unsubscribe(OnTelegraph);
            onBattleStarted?.Unsubscribe(OnStart);
        }

        private void Start()
        {
            // Start the battle theme (the channel may have fired before we subscribed).
            Audio?.PlayMusic("battle");
        }

        private void OnStart(bool _) => Audio?.PlayMusic("battle");

        private void OnDamage(DamageResult r)
        {
            if (Audio == null) return;
            string id = !r.hit ? "miss" : (r.isHeal || r.absorbed) ? "heal" : r.crit ? "crit" : "hit";
            // Replicate JuiceController's per-hit schedule: a shared 0.24s beat (so a 3-hit combo's
            // sounds space out with its visuals) plus the melee/magic wind-up before contact.
            float start = Mathf.Max(Time.time, nextAudioBeat);
            nextAudioBeat = start + 0.24f;
            float windup = (r.hit && r.ability != null && !r.ability.isMagic && r.ability.targetRule != TargetRule.AllEnemies) ? 0.17f : 0.22f;
            StartCoroutine(DelayedSfx(id, (start - Time.time) + windup));
        }

        private System.Collections.IEnumerator DelayedSfx(string id, float delay)
        {
            if (delay > 0f) yield return new WaitForSeconds(delay);
            Audio?.PlaySfx(id);
        }

        private void OnBreak(Entity _)
        {
            var a = Audio; if (a == null) return;
            a.PlaySfx("break");
            a.DuckMusic(0.4f, 0.55f, 0.7f);   // dip the loop under the BREAK stinger, then swell back (~covers the slow-mo)
        }
        private void OnTelegraph(Ability _) => Audio?.PlaySfx("telegraph");
    }
}
