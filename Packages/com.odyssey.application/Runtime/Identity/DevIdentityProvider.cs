using System.Collections.Generic;
using Odyssey.Application.Results;
using Odyssey.Domain.Identity;

namespace Odyssey.Application.Identity
{
    /// <summary>
    /// ODY-S02-009: the approved dev/mock identity boundary ADR-018 section 5
    /// fixes ("approved, детерминированный набор dev UserId для
    /// локальных/CI тестов") -- a small, fixed, deterministic pool of
    /// canonical UserId values, used by the real (not test-only) admission
    /// flow until a real Supabase Auth integration exists (ADR-018 section 5,
    /// explicitly deferred). Not itself test-only code: this is what a real
    /// host/client process uses to identify local actors in the absence of a
    /// live auth provider, per the ADR's own approved boundary.
    /// </summary>
    public static class DevIdentityProvider
    {
        // UserId has no NewId() factory (ADR-018 section 4: "externally
        // assigned") -- these are fixed canonical literals, not generated at
        // runtime, matching the ADR's "fixed, deterministic set" requirement.
        private static readonly UserId HostUser = UserId.Parse("user_00000000000000000000000000000001");
        private static readonly UserId DevUser1 = UserId.Parse("user_00000000000000000000000000000002");
        private static readonly UserId DevUser2 = UserId.Parse("user_00000000000000000000000000000003");
        private static readonly UserId DevUser3 = UserId.Parse("user_00000000000000000000000000000004");

        private static readonly IReadOnlyList<UserId> JoiningPool = new[] { DevUser1, DevUser2, DevUser3 };

        /// <summary>The fixed dev identity assigned to whichever local actor starts a session as host.</summary>
        public static UserId AssignHost() => HostUser;

        /// <summary>
        /// Assigns a dev identity to a joining actor by a stable, caller-supplied
        /// slot index (0-based) -- e.g. "the first player process to join in
        /// this dev/test run gets slot 0". Bounded to the fixed pool size;
        /// out-of-range requests are a typed failure, not an exception, since a
        /// caller-driven index is external input to this port.
        /// </summary>
        public static Result<UserId> AssignJoiningActor(int slot)
        {
            if (slot < 0 || slot >= JoiningPool.Count)
            {
                return Result<UserId>.Failure(IdentityFailures.DevIdentitySlotOutOfRange());
            }

            return Result<UserId>.Success(JoiningPool[slot]);
        }
    }

    public static class IdentityFailures
    {
        public static Error DevIdentitySlotOutOfRange() => Error.Create(
            ErrorCodes.IdentityDevSlotOutOfRange,
            ErrorCategory.Validation,
            SafeReasonCode.InvalidRequest,
            UserMessageKey.Parse("errors.identity.dev_slot_out_of_range"),
            RetryDirective.DoNotRetry,
            CorrelationId.Parse("corr_00000000000000000000000000000000"));
    }
}
