using System;
using Odyssey.Application.Commands;
using Odyssey.Application.Persistence;
using Odyssey.Application.Results;
using Odyssey.Domain.Character;
using Odyssey.Domain.Identity;
using RulesAnatomyInitializationRules = Odyssey.Rules.Character.AnatomyInitializationRules;
using RulesResourceInitializationRules = Odyssey.Rules.Character.ResourceInitializationRules;

namespace Odyssey.Application.CharacterAdvancement
{
    /// <summary>
    /// ODY-S09-101: the Application-layer owner of character commands whose values or legality come from
    /// `Odyssey.Rules`. `ADR-024` (lines 192-194) and `ADR-025` (lines 185-187) assign these decisions to
    /// Application ("`Odyssey.Application` owns ... command handlers") and say Persistence "does not decide
    /// whether a purchase ... is legal"; `ADR-001` section 5 forbids `Odyssey.Persistence` from referencing
    /// `Odyssey.Rules` at all. `SqliteCharacterRepository` had nevertheless been consulting Rules directly since
    /// `ODY-S04-105` (see `docs/research/Unity_Rules_Asmdef_Break_Investigation.md`). This service is the place
    /// those calls move to, one block of `SLICE-09` at a time; this first block moves only the two commands that
    /// merely read fixed default values.
    ///
    /// Same shape as `Odyssey.Application.Checks.CheckService` (`ODY-S07-102`): a static class that talks to
    /// Persistence only through an Application-owned port (`ICharacterRepository`) passed in by the caller, uses
    /// `Odyssey.Rules` itself (legal for Application), and needs no composition root or DI container.
    /// </summary>
    public static class CharacterAdvancementService
    {
        /// <summary>
        /// Initializes a new `CharacterResource` with `ODY-S04-109`'s Rules-backed defaults
        /// (`ResourceInitializationRules.DefaultBaseMaximum` / `DefaultMinimumValue` / `DefaultRecoveryRule` -- an
        /// explicitly-flagged test fixture; no `ResourceDefinition` catalog exists yet), then calls
        /// <see cref="ICharacterRepository.InitializeCharacterResource"/> with those plain values. Every other
        /// argument, and the result, are passed through unchanged, so behavior is identical to the pre-`ODY-S09-101`
        /// repository method that read the same constants itself.
        /// </summary>
        public static Result<CharacterRecord> InitializeResourceWithDefaults(
            ICharacterRepository characters,
            CampaignHandle campaign,
            CharacterId characterId,
            ResourceDefinitionId resourceDefinitionId,
            UserId actorUserId,
            bool actorIsMainGm,
            long expectedCharacterResourcesRevision,
            CommandId commandId,
            CorrelationId correlationId)
        {
            if (characters == null) throw new ArgumentNullException(nameof(characters));

            return characters.InitializeCharacterResource(
                campaign,
                characterId,
                resourceDefinitionId,
                RulesResourceInitializationRules.DefaultBaseMaximum,
                RulesResourceInitializationRules.DefaultMinimumValue,
                RulesResourceInitializationRules.DefaultRecoveryRule,
                actorUserId,
                actorIsMainGm,
                expectedCharacterResourcesRevision,
                commandId,
                correlationId);
        }

        /// <summary>
        /// Initializes the character's single `CharacterAnatomy` snapshot with `ODY-S04-109`'s Rules-backed defaults
        /// (`AnatomyInitializationRules.DefaultAnatomyProfileVersion` / `DefaultHumanoidBodyParts()` -- an
        /// explicitly-flagged test fixture), then calls <see cref="ICharacterRepository.InitializeCharacterAnatomy"/>
        /// with those plain values. Every other argument, and the result, are passed through unchanged.
        /// `DefaultHumanoidBodyParts()` is a pure function that builds a fresh list on each call, so evaluating it
        /// here (before the repository's own permission/already-initialized checks) has no observable effect.
        /// </summary>
        public static Result<CharacterRecord> InitializeAnatomyWithDefaults(
            ICharacterRepository characters,
            CampaignHandle campaign,
            CharacterId characterId,
            AnatomyProfileDefinitionId anatomyProfileDefinitionId,
            UserId actorUserId,
            bool actorIsMainGm,
            long expectedCharacterAnatomyRevision,
            CommandId commandId,
            CorrelationId correlationId)
        {
            if (characters == null) throw new ArgumentNullException(nameof(characters));

            return characters.InitializeCharacterAnatomy(
                campaign,
                characterId,
                anatomyProfileDefinitionId,
                RulesAnatomyInitializationRules.DefaultAnatomyProfileVersion,
                RulesAnatomyInitializationRules.DefaultHumanoidBodyParts(),
                actorUserId,
                actorIsMainGm,
                expectedCharacterAnatomyRevision,
                commandId,
                correlationId);
        }
    }
}
