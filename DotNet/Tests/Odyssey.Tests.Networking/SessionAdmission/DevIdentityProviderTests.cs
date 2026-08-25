using NUnit.Framework;
using Odyssey.Application.Identity;
using Odyssey.Application.Results;

namespace Odyssey.Tests.Networking.SessionAdmission
{
    /// <summary>ODY-S02-009: ADR-018 section 5's approved dev/mock identity boundary.</summary>
    public sealed class DevIdentityProviderTests
    {
        [Test]
        public void AssignHost_IsStableAcrossCalls()
        {
            Assert.That(DevIdentityProvider.AssignHost(), Is.EqualTo(DevIdentityProvider.AssignHost()));
        }

        [Test]
        public void AssignJoiningActor_ValidSlots_ReturnDistinctStableUserIds()
        {
            Result<Odyssey.Domain.Identity.UserId> first = DevIdentityProvider.AssignJoiningActor(0);
            Result<Odyssey.Domain.Identity.UserId> second = DevIdentityProvider.AssignJoiningActor(1);

            Assert.That(first.IsSuccess, Is.True);
            Assert.That(second.IsSuccess, Is.True);
            Assert.That(first.Value, Is.Not.EqualTo(second.Value));
            Assert.That(first.Value, Is.Not.EqualTo(DevIdentityProvider.AssignHost()));
            Assert.That(DevIdentityProvider.AssignJoiningActor(0).Value, Is.EqualTo(first.Value), "the same slot must always resolve to the same dev UserId (deterministic pool)");
        }

        [Test]
        public void AssignJoiningActor_OutOfRangeSlot_ReturnsTypedFailure_NoException()
        {
            Result<Odyssey.Domain.Identity.UserId> result = DevIdentityProvider.AssignJoiningActor(99);

            Assert.That(result.IsFailure, Is.True);
            Assert.That(result.Error.Code, Is.EqualTo(ErrorCodes.IdentityDevSlotOutOfRange));
        }
    }
}
