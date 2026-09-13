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
        public void Production_scope_has_no_attack_rng_effect_ui_or_network_surface()
        {
            var directory = new DirectoryInfo(Directory.GetCurrentDirectory());
            while (directory != null && !Directory.Exists(Path.Combine(directory.FullName, "Packages"))) directory = directory.Parent;
            Assert.That(directory, Is.Not.Null);
            string root = directory!.FullName;
            string source = File.ReadAllText(Path.Combine(root, "Packages", "com.odyssey.persistence", "Runtime", "Sqlite", "SqliteCombatEncounterRepository.cs")) +
                            File.ReadAllText(Path.Combine(root, "Packages", "com.odyssey.application", "Runtime", "Combat", "CombatEncounterContracts.cs"));
            foreach (string forbidden in new[] { "using Odyssey.Networking", "using UnityEngine", "using Odyssey.Application.GameLog", "using Odyssey.Application.Random", "using Odyssey.Application.Effects", "DiceRoll", "DamageResult" })
            {
                Assert.That(source, Does.Not.Contain(forbidden));
            }
        }
    }
}
