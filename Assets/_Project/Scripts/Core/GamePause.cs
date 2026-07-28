namespace RPGArena.Core
{
    // The single, assembly-neutral answer to "is the game paused?". It lives in Core because BOTH
    // sides need it and neither may reference the other: the UI's PauseMenu owns Time.timeScale and
    // publishes here, while Gameplay (the real-time action commands) and presentation (the juice
    // layer's hit-stop watchdog) read it so nothing resolves — or force-unpauses — behind the menu.
    //
    // Why a flag and not a Time.timeScale check: the juice layer legitimately parks timeScale at 0
    // for hit-stop, so "clock stopped" does NOT mean "player paused".
    public static class GamePause
    {
        public static bool IsPaused { get; private set; }

        public static void Set(bool paused) => IsPaused = paused;
    }
}
