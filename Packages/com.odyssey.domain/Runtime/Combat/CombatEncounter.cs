using System;
using Odyssey.Domain.Identity;

namespace Odyssey.Domain.Combat
{
    public enum CombatEncounterStatus { Open = 1, ClosedNoEligibleParticipants = 2 }
    public enum CombatPhase { TurnOpen = 1, Closed = 2 }
    public enum CombatLifecycleEventKind { Created = 1, RoundStarted = 2, TurnStarted = 3, TurnEnded = 4, ParticipantSkipped = 5, ClosedNoEligibleParticipants = 6 }

    /// <summary>Stable supplied order, not an initiative calculation.</summary>
    public readonly struct CombatParticipant
    {
        public CombatParticipant(CharacterId characterId, int order)
        {
            if (!characterId.IsValid) throw new ArgumentException("CharacterId is required.", nameof(characterId));
            if (order < 0) throw new ArgumentOutOfRangeException(nameof(order));
            CharacterId = characterId; Order = order;
        }
        public CharacterId CharacterId { get; }
        public int Order { get; }
    }
}
