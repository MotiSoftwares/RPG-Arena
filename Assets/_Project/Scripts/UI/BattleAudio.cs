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
            var a = Audio; if (a == null) return;
            if (!r.hit) a.PlaySfx("miss");
            else if (r.isHeal || r.absorbed) a.PlaySfx("heal");
            else if (r.crit) a.PlaySfx("crit");
            else a.PlaySfx("hit");
        }

        private void OnBreak(Entity _) => Audio?.PlaySfx("break");
        private void OnTelegraph(Ability _) => Audio?.PlaySfx("telegraph");
    }
}
