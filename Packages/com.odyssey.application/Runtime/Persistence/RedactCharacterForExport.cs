using System;
using System.Collections.Generic;
using Odyssey.Domain.Character;
using Odyssey.Domain.Time;

namespace Odyssey.Application.Persistence
{
    /// <summary>
    /// ODY-S04-112: ADR-026 section 3.2's export redaction filter -- a pure,
    /// synchronous, connection-free function over an already-loaded
    /// <see cref="CharacterRecord"/>. Deliberately never routes through
    /// ADR-017's live <c>ClientProjection</c>/snapshot-delta machinery (ADR-026
    /// section 8 rule 1) -- a local file export has no active
    /// connection/Membership/Scene context for that pipeline to consume.
    /// </summary>
    public static class RedactCharacterForExport
    {
        /// <summary>
        /// ADR-026 section 5: today, no field on <see cref="CharacterRecord"/>
        /// is classified as GM-only-visible or secret/credential-bearing --
        /// this function's single, named extension point (per ADR-026 section
        /// 8 rule 4) is exactly the branch below: a future GM-only/secret
        /// field is withheld here, conditioned on <paramref name="actorContext"/>,
        /// rather than through a second, parallel redaction path. Never
        /// serializes <see cref="CharacterRecord.Ownership"/>,
        /// <see cref="CharacterRecord.CharacterId"/>, or
        /// <see cref="CharacterRecord.CampaignId"/> (ADR-026 section 4/section
        /// 8 rule 2) -- none of the three appear anywhere in the returned
        /// payload's own shape at all, by construction, not by a runtime check.
        /// </summary>
        public static CharacterExportPayload Redact(CharacterRecord record, ExportActorContext actorContext)
        {
            if (record == null) throw new ArgumentNullException(nameof(record));
            if (actorContext == null) throw new ArgumentNullException(nameof(actorContext));

            // Extension point (ADR-026 section 8 rule 4): a future GM-only or
            // secret field would be conditionally included/excluded right
            // here, based on actorContext.ActorIsMainGm or the actor's
            // ownership/control relationship (already available via
            // record.Ownership) -- no such field exists today (ADR-026
            // section 5), so this function returns the full remaining
            // payload for every actor, honestly reflecting that fact rather
            // than fabricating a redaction against non-existent data.

            var attributes = new List<ExportedAttributeValue>(record.Attributes.Count);
            foreach (AttributeValue attribute in record.Attributes)
            {
                attributes.Add(new ExportedAttributeValue(attribute.AttributeDefinitionId, attribute.BaseValue, attribute.PermanentAdjustment, attribute.SpentDevelopmentPoints));
            }

            var skills = new List<ExportedSkill>(record.Skills.Count);
            foreach (CharacterSkill skill in record.Skills)
            {
                skills.Add(new ExportedSkill(skill.SkillDefinitionId, skill.Level, skill.PermanentAdjustment, skill.SpentDevelopmentPoints));
            }

            var abilities = new List<ExportedAbility>(record.Abilities.Count);
            foreach (CharacterAbility ability in record.Abilities)
            {
                abilities.Add(new ExportedAbility(ability.CharacterAbilityId, ability.AbilityDefinitionId, ability.SourceKind, ability.SourceRef, ability.RankMode, ability.NumericRank, ability.NamedRankKey, ability.IsEnabled, ability.Configuration, ability.UsesState));
            }

            var resources = new List<ExportedResource>(record.Resources.Count);
            foreach (CharacterResource resource in record.Resources)
            {
                resources.Add(new ExportedResource(resource.CharacterResourceId, resource.ResourceDefinitionId, resource.CurrentValue, resource.BaseMaximum, resource.PermanentMaximumAdjustment, resource.MinimumValue, resource.RecoveryRule));
            }

            ExportedAnatomy? anatomy = null;
            if (record.Anatomy != null)
            {
                var bodyParts = new List<ExportedBodyPart>(record.Anatomy.BodyParts.Count);
                foreach (BodyPart bodyPart in record.Anatomy.BodyParts)
                {
                    bodyParts.Add(new ExportedBodyPart(bodyPart.BodyPartId, bodyPart.Name, bodyPart.DamageLimit, bodyPart.AttachedToBodyPartId, bodyPart.Properties));
                }

                var modifications = new List<ExportedPermanentModification>(record.Anatomy.PermanentModifications.Count);
                foreach (PermanentModification modification in record.Anatomy.PermanentModifications)
                {
                    modifications.Add(new ExportedPermanentModification(modification.PermanentModificationId, modification.AttachedToBodyPartId, modification.Kind, modification.Description, modification.AppliedAt));
                }

                anatomy = new ExportedAnatomy(record.Anatomy.AnatomyProfileDefinitionId, record.Anatomy.AnatomyProfileVersion, bodyParts, modifications);
            }

            return new CharacterExportPayload(
                record.CharacterKind,
                record.DisplayName,
                record.PortraitReference,
                record.AnatomyProfileRef ?? string.Empty,
                record.RulesetVersion,
                record.DevelopmentPool.Earned,
                record.DevelopmentPool.Spent,
                attributes,
                skills,
                abilities,
                resources,
                anatomy);
        }

        /// <summary>
        /// ADR-026 section 4's <c>ExportedByRole</c> manifest field --
        /// "MainGM" | "Owner" | "Controller", resolved from the actor's own
        /// relationship to the exported Character's <see cref="CharacterOwnership"/>.
        /// This resolution affects only the manifest's own provenance label,
        /// never <see cref="Redact"/>'s own payload content (section 5:
        /// MainGM and the Character's own owner produce byte-identical
        /// <c>character.json</c> today).
        /// </summary>
        public static string ResolveExportedByRole(CharacterOwnership ownership, ExportActorContext actorContext, UtcInstant now)
        {
            if (ownership == null) throw new ArgumentNullException(nameof(ownership));
            if (actorContext == null) throw new ArgumentNullException(nameof(actorContext));

            if (actorContext.ActorIsMainGm)
            {
                return "MainGM";
            }

            if (ownership.PrimaryOwnerUserId.HasValue && ownership.PrimaryOwnerUserId.Value.Equals(actorContext.ActorUserId))
            {
                return "Owner";
            }

            foreach (var coOwner in ownership.CoOwnerUserIds)
            {
                if (coOwner.Equals(actorContext.ActorUserId)) return "Owner";
            }

            // Permanent/temporary controller, or an actor with no recorded
            // relationship at all -- ADR-026 section 6 explicitly leaves
            // "permission to initiate export" out of this ADR's scope, so an
            // unrelated actor is not rejected here; "Controller" is the
            // closest of the three named literals and is this task's own
            // documented fallback for that case.
            return "Controller";
        }
    }
}
