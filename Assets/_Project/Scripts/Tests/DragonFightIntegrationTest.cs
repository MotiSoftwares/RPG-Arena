using NUnit.Framework;
using RPGArena.Combat.Demo;

namespace RPGArena.Tests
{
    // End-to-end check: a full Mage-vs-Dragon fight runs the whole engine to a clear outcome
    // and the signature systems fire (Ice weakness, Stagger break). The fight log is echoed to
    // the Console so it can be read/pasted as evidence (CLAUDE.md §8 Milestone 1).
    public class DragonFightIntegrationTest
    {
        [Test]
        public void Mage_Vs_Dragon_Resolves_And_Core_Systems_Fire()
        {
            string log = DragonFightDemo.Run(seed: 7, echoToConsole: false);
            UnityEngine.Debug.Log("=== DRAGON FIGHT DEMO LOG ===\n" + log);

            // Terminates with a clear outcome (no infinite loop / hang).
            Assert.IsTrue(log.Contains("OUTCOME=Victory") || log.Contains("OUTCOME=Defeat"),
                "The fight must resolve to Victory or Defeat.");

            // The Ice weakness must have been exploited at least once.
            StringAssert.Contains("WEAK!", log);

            // The Stagger/Break mechanic must have triggered during the fight.
            StringAssert.Contains("BREAK!", log);
        }
    }
}
