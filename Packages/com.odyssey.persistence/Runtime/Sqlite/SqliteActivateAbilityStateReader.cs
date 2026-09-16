using System;
using Odyssey.Application.Content;
using Odyssey.Application.Persistence;
using Odyssey.Application.Results;
using Odyssey.Application.Time;
using Odyssey.Domain.Character;
using Odyssey.Domain.Content;
using Odyssey.Domain.Identity;

namespace Odyssey.Persistence.Sqlite
{
    /// <summary>
    /// ODY-S06-106: read-only composition of existing SQLite-backed authoritative stores for ability
    /// activation -- the exact same shape `SqliteAttackStateReader` (`ODY-S05-603`) already established.
    ///
    /// Resolves `AbilityDefinitionId -> real AbilityDefinition` through `CharacterAbility.
    /// ActivationDefinitionRef` (`ODY-S06-106` doработка, product-owner-ordered after independent
    /// verification rejected PR #162's own original bridge -- reusing `SourceRef` for this purpose, which
    /// violated that field's own documented provenance-only contract). `AbilityDefinitionId` (`ODY-S04-108`)
    /// remains a genuine architecture gap this task found: it is a lightweight, fixture-only Ruleset key
    /// with no real backing catalog table anywhere in this codebase (confirmed by its own doc comment and
    /// by direct code read of every `IContentCatalogRepository` query method -- none accept anything but the
    /// real content catalog's own opaque `ContentDefinitionId`), and the real content catalog's own
    /// `ContentDefinition` table has no indexed human-key column either (confirmed by direct schema read --
    /// `Name` is a free-text display string, not a validated/unique key). The fix keeps `CharacterAbility.
    /// SourceRef`'s own contract (provenance only, null for `GMGrant`/`ProgressionPurchase`) fully intact
    /// and introduces a genuinely separate, additive `ActivationDefinitionRef` field instead, set only via
    /// `ICharacterRepository.LinkAbilityActivationSource` (MainGM-only). An ability with no
    /// `ActivationDefinitionRef` set (e.g. any ability acquired the ordinary way and never linked) simply
    /// cannot be activated -- an honest rejection, not a crash.
    /// </summary>
    public sealed class SqliteActivateAbilityStateReader : IActivateAbilityStateReader
    {
        private readonly ICharacterRepository _characters;
        private readonly IContentCatalogRepository _catalog;
        private readonly IWallClock _clock;

        public SqliteActivateAbilityStateReader(ICharacterRepository characters, IContentCatalogRepository catalog, IWallClock clock)
        {
            _characters = characters ?? throw new ArgumentNullException(nameof(characters));
            _catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
            _clock = clock ?? throw new ArgumentNullException(nameof(clock));
        }

        public Result<ActivateAbilityState> Read(CampaignHandle campaign, ActivateAbilityIntent intent, CorrelationId correlationId)
        {
            Result<CharacterRecord> actor = _characters.GetCharacter(campaign, intent.ActorId, correlationId);
            if (actor.IsFailure || actor.Value.CampaignId != campaign.CampaignId) return Result<ActivateAbilityState>.Failure(actor.IsFailure ? actor.Error : Rejected(correlationId));

            CharacterAbility? ability = null;
            foreach (CharacterAbility candidate in actor.Value.Abilities)
            {
                if (candidate.CharacterAbilityId.Equals(intent.CharacterAbilityId)) { ability = candidate; break; }
            }

            if (ability == null) return Result<ActivateAbilityState>.Failure(Rejected(correlationId));
            if (!ability.IsEnabled) return Result<ActivateAbilityState>.Failure(Rejected(correlationId));

            if (ability.ActivationDefinitionRef == null || !ability.ActivationDefinitionRef.Value.IsValid)
            {
                return Result<ActivateAbilityState>.Failure(Rejected(correlationId));
            }

            ContentDefinitionRef definitionRef = ability.ActivationDefinitionRef.Value;
            Result<ContentDefinitionRecord> fetched = _catalog.GetContentDefinition(campaign, definitionRef.DefinitionId, correlationId);
            if (fetched.IsFailure) return Result<ActivateAbilityState>.Failure(fetched.Error);
            ContentDefinitionRecord content = fetched.Value;
            if (content.Version != definitionRef.Version || content.Status != ContentDefinitionStatus.Published || content.DefinitionType != ContentDefinitionType.Ability)
            {
                return Result<ActivateAbilityState>.Failure(Rejected(correlationId));
            }

            Result<AbilityDefinition> decoded = TypedDefinitionCodec.DecodeAbility(content.DefinitionType, content.PropertiesJson, correlationId);
            if (decoded.IsFailure) return Result<ActivateAbilityState>.Failure(decoded.Error);

            // ADR-030 section 7.1: a null MechanicsPayloadRef is a legitimate "no mechanics implemented
            // yet" ability (ODY-S05-105's own established convention) -- an empty envelope, not an error.
            MechanicsPrimitiveEnvelope primitives;
            if (decoded.Value.MechanicsPayloadRef == null)
            {
                primitives = new MechanicsPrimitiveEnvelope(1, Array.Empty<Odyssey.Domain.Content.MechanicsPrimitive>());
            }
            else
            {
                Result<MechanicsPrimitiveEnvelope> decodedPrimitives = MechanicsPayloadCodec.DecodePrimitives(decoded.Value.MechanicsPayloadRef, correlationId);
                if (decodedPrimitives.IsFailure) return Result<ActivateAbilityState>.Failure(decodedPrimitives.Error);
                primitives = decodedPrimitives.Value;
            }

            return Result<ActivateAbilityState>.Success(new ActivateAbilityState(actor.Value, ability, decoded.Value, primitives));
        }

        public Result<bool> CanControlActor(CampaignHandle campaign, CharacterId actorId, UserId userId, CorrelationId correlationId)
        {
            Result<CharacterRecord> character = _characters.GetCharacter(campaign, actorId, correlationId);
            if (character.IsFailure) return Result<bool>.Failure(character.Error);
            return Result<bool>.Success(character.Value.CampaignId == campaign.CampaignId && CharacterOwnershipAssignment.IsAssignedCharacter(character.Value.Ownership, userId, _clock.GetUtcNow()));
        }

        private static Error Rejected(CorrelationId correlationId) => Error.Create(ErrorCodes.ApplicationValidationInvalid, ErrorCategory.Precondition, SafeReasonCode.ActionNotAllowed, UserMessageKey.Parse("errors.ability.invalid_state"), RetryDirective.DoNotRetry, correlationId);
    }
}
