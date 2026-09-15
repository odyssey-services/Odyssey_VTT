using NUnit.Framework;
using System.IO;
using Odyssey.Domain.Combat;

namespace Odyssey.Tests.Architecture
{
    public sealed class CombatScopeTests
    {
        [Test]
        public void Timeline_vocabulary_has_no_attack_outcome()
        {
            Assert.That(System.Enum.IsDefined(typeof(CombatLifecycleEventKind), CombatLifecycleEventKind.TurnStarted), Is.True);
            Assert.That(System.Enum.GetNames(typeof(CombatLifecycleEventKind)), Does.Not.Contain("AttackResolved"));
        }

        [Test]
        public void Production_scope_has_no_attack_rng_ui_or_network_surface()
        {
            var directory = new DirectoryInfo(Directory.GetCurrentDirectory());
            while (directory != null && !Directory.Exists(Path.Combine(directory.FullName, "Packages"))) directory = directory.Parent;
            Assert.That(directory, Is.Not.Null);
            string root = directory!.FullName;
            string source = File.ReadAllText(Path.Combine(root, "Packages", "com.odyssey.persistence", "Runtime", "Sqlite", "SqliteCombatEncounterRepository.cs")) +
                            File.ReadAllText(Path.Combine(root, "Packages", "com.odyssey.application", "Runtime", "Combat", "CombatEncounterContracts.cs"));
            Assert.That(source, Does.Contain("IWallClock"));
            // ODY-S05-611: `using Odyssey.Application.Effects` is now a
            // deliberate, narrow, documented exception -- SqliteCombatEncounterRepository.Advance
            // is the single authorized point in this codebase where a combat
            // round/turn genuinely advances, so it is also the correct place
            // to invoke 605's own unmodified CombatEffectExpiryService for
            // combat-duration-bound ActiveEffect rows (closing 605's own
            // disclosed "no automatic wiring point" gap). This is composition
            // of an already-accepted service, not a reopening of combat
            // encounter timeline's own effect-authoring/stacking/attack
            // scope -- RNG, Networking, UI, Game Log, and attack-specific
            // (DiceRoll/DamageResult) surfaces remain forbidden below exactly
            // as before. See ODY-S05-611's own task contract for the full
            // reasoning.
            foreach (string forbidden in new[] { "using Odyssey.Networking", "using UnityEngine", "using Odyssey.Application.GameLog", "using Odyssey.Application.Random", "DiceRoll", "DamageResult" })
            {
                Assert.That(source, Does.Not.Contain(forbidden));
            }
            foreach (string forbiddenTimeSource in new[] { "DateTime.UtcNow", "DateTime.Now", "Stopwatch", "Environment.TickCount", "UnityEngine.Time" }) Assert.That(source, Does.Not.Contain(forbiddenTimeSource));
        }
    }
}
