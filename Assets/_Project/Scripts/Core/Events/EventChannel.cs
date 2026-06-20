using System;
using System.Collections.Generic;
using UnityEngine;

namespace RPGArena.Core.Events
{
    // Generic ScriptableObject "event bus" — the decoupling backbone (CLAUDE.md §4.2/§4.13).
    // Core logic RAISES events on these assets; presentation (UI/audio/juice) SUBSCRIBES,
    // so the two layers never hold direct references to each other.
    //
    // This open generic is abstract on purpose: Unity cannot create an asset from an open
    // generic ScriptableObject. Each payload type gets a tiny concrete subclass with
    // [CreateAssetMenu] (VoidChannel, IntChannel, ...) which CAN be made into an .asset.
    public abstract class EventChannel<T> : ScriptableObject
    {
        // Listeners exist only at runtime; they are never serialized into the asset.
        private readonly List<Action<T>> listeners = new();

        // Register a listener, guarding against accidental double-subscription.
        public void Subscribe(Action<T> callback)
        {
            if (callback != null && !listeners.Contains(callback))
                listeners.Add(callback);
        }

        // Remove a listener; safe even if it was never registered.
        public void Unsubscribe(Action<T> callback)
        {
            listeners.Remove(callback);
        }

        // Fire the event to every listener. Iterate backwards so a listener that
        // unsubscribes itself (or gets destroyed) mid-callback can't break the loop.
        public void Raise(T payload)
        {
            for (int i = listeners.Count - 1; i >= 0; i--)
                listeners[i]?.Invoke(payload);
        }

        // A ScriptableObject instance survives across editor play sessions, so wipe any
        // stale listeners when the asset (re)loads to avoid leaking dead delegates.
        protected virtual void OnEnable() => listeners.Clear();
    }
}
