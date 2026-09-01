using System;
using System.Collections.Generic;
using Odyssey.Application.Commands;
using Odyssey.Application.Results;
using Odyssey.Domain.Character;
using Odyssey.Domain.Identity;
using Odyssey.Domain.Time;

namespace Odyssey.Application.Persistence
{
    /// <summary>
    /// ODY-S04-103: ADR-023 section 4.1's local Draft -- a client-owned
    /// record in personal-profile storage (<see cref="LocalProfileHandle"/>),
    /// not an ADR-022 Character aggregate instance. It has no
    /// <see cref="CampaignId"/>, no <see cref="CharacterId"/>, and does not
    /// participate in <c>DomainEvents</c>/<c>CharacterHistoryProjection</c> --
    /// so, unlike <see cref="ICharacterRepository"/>, this port does not
    /// route through <c>SqliteSavingPipeline</c>; it is ordinary CRUD over
    /// its own personal-profile table.
    /// </summary>
    public interface ILocalCharacterDraftRepository
    {
        Result<LocalCharacterDraftRecord> CreateLocalCharacterDraft(LocalProfileHandle profile, CreateLocalCharacterDraftRequest request, CommandId commandId, CorrelationId correlationId);

        Result<LocalCharacterDraftRecord> GetLocalCharacterDraft(LocalProfileHandle profile, LocalCharacterDraftId draftId, CorrelationId correlationId);
    }

    /// <summary>
    /// ODY-S04-103: product section 8.2's minimum required fields, narrowed to
    /// what a pre-bind local Draft can actually know -- <c>RulesetVersion</c>
    /// is pinned only at <c>BindDraftToCampaign</c> (ADR-023 section 6.2), and
    /// <c>PrimaryOwner</c> is set at bind time as an ordinary Draft-to-
    /// Character field (backlog section 2.2), so neither is required here.
    /// </summary>
    public sealed class CreateLocalCharacterDraftRequest
    {
        public CreateLocalCharacterDraftRequest(CharacterKind characterKind, string name, string anatomyProfileRef, CharacterTemplateId? personalTemplateId)
        {
            if (!Enum.IsDefined(typeof(CharacterKind), characterKind)) throw new ArgumentOutOfRangeException(nameof(characterKind));
            if (string.IsNullOrWhiteSpace(name) || name.Length > 128) throw new ArgumentException("Name is not safe.", nameof(name));
            if (string.IsNullOrWhiteSpace(anatomyProfileRef)) throw new ArgumentException("AnatomyProfileRef is required.", nameof(anatomyProfileRef));

            CharacterKind = characterKind;
            Name = name;
            AnatomyProfileRef = anatomyProfileRef;
            PersonalTemplateId = personalTemplateId;
        }

        public CharacterKind CharacterKind { get; }
        public string Name { get; }
        public string AnatomyProfileRef { get; }

        /// <summary>Must reference an existing <see cref="TemplateScope.Personal"/> template in the same profile, if set (ADR-023 section 5.3).</summary>
        public CharacterTemplateId? PersonalTemplateId { get; }
    }

    /// <summary>
    /// ODY-S04-103: the local Draft's current-state row.
    /// <see cref="TemplateId"/>/<see cref="TemplateVersionAtCopyTime"/>/
    /// <see cref="SeedCopy"/> are already the fresh-identifier deep copy
    /// (ADR-023 section 5.3), computed once, here, at Draft-creation time --
    /// <see cref="ICharacterRepository.BindDraftToCampaign"/> carries them
    /// through unchanged rather than re-copying.
    /// </summary>
    public sealed class LocalCharacterDraftRecord
    {
        public LocalCharacterDraftRecord(
            LocalCharacterDraftId draftId,
            UserId ownerUserId,
            CharacterKind characterKind,
            string name,
            string anatomyProfileRef,
            CharacterTemplateId? templateId,
            long? templateVersionAtCopyTime,
            IReadOnlyList<CopiedCharacterSeedItem> seedCopy,
            UtcInstant createdAt)
        {
            if (!draftId.IsValid) throw new ArgumentException("DraftId is required.", nameof(draftId));
            if (!ownerUserId.IsValid) throw new ArgumentException("OwnerUserId is required.", nameof(ownerUserId));
            if (!Enum.IsDefined(typeof(CharacterKind), characterKind)) throw new ArgumentOutOfRangeException(nameof(characterKind));
            if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("Name is required.", nameof(name));
            if (string.IsNullOrWhiteSpace(anatomyProfileRef)) throw new ArgumentException("AnatomyProfileRef is required.", nameof(anatomyProfileRef));

            DraftId = draftId;
            OwnerUserId = ownerUserId;
            CharacterKind = characterKind;
            Name = name;
            AnatomyProfileRef = anatomyProfileRef;
            TemplateId = templateId;
            TemplateVersionAtCopyTime = templateVersionAtCopyTime;
            SeedCopy = seedCopy ?? throw new ArgumentNullException(nameof(seedCopy));
            CreatedAt = createdAt;
        }

        public LocalCharacterDraftId DraftId { get; }
        public UserId OwnerUserId { get; }
        public CharacterKind CharacterKind { get; }
        public string Name { get; }
        public string AnatomyProfileRef { get; }
        public CharacterTemplateId? TemplateId { get; }
        public long? TemplateVersionAtCopyTime { get; }
        public IReadOnlyList<CopiedCharacterSeedItem> SeedCopy { get; }
        public UtcInstant CreatedAt { get; }
    }
}
