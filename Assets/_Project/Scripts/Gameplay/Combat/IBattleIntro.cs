namespace RPGArena.Combat
{
    // A tiny seam so the BattleController can consult the narrative layer WITHOUT depending on
    // the Narrative assembly (which depends on Gameplay — referencing it back would be a cycle).
    // The NarrativeRunner implements this; the controller finds it by interface at battle start.
    public interface IBattleIntro
    {
        // True once the pre-fight intro has finished (or immediately if there is no intro).
        bool IsIntroDone { get; }
        // The player's pre-fight choice, read once after the intro to alter the opening (§13.2).
        bool StartTelegraph { get; }   // taunt => the Dragon opens by charging
        bool RevealWeak { get; }       // study => reveal the weakness in the HUD now

        // Play the closing beat with the real battle result.
        void PlayOutro(bool won, bool brokeBoss, int heroesLost);
    }
}
