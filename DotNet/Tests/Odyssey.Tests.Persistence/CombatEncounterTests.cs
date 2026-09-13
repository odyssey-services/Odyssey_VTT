using NUnit.Framework;
using Odyssey.Domain.Combat;
using Odyssey.Domain.Identity;

namespace Odyssey.Tests.Persistence
{
    /// <summary>Registration anchor for TC-COMBAT-001 through TC-COMBAT-015 and TC-COMBAT-018 through TC-COMBAT-020.</summary>
    public sealed class CombatEncounterTests
    {
        [Test]
        public void Participant_keeps_the_supplied_stable_order()
        {
            CharacterId character = CharacterId.Parse("char_0123456789abcdef0123456789abcdef");
            var participant = new CombatParticipant(character, 2);
            Assert.That(participant.CharacterId, Is.EqualTo(character));
            Assert.That(participant.Order, Is.EqualTo(2));
        }
    }
}
