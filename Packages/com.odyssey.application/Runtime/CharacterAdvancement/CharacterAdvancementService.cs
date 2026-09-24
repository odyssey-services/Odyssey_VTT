using System;
using System.Collections.Generic;
using Odyssey.Application.Commands;
using Odyssey.Application.Persistence;
using Odyssey.Application.Results;
using Odyssey.Domain.Character;
using Odyssey.Domain.Identity;
using RulesAbilityCostRules = Odyssey.Rules.Character.AbilityCostRules;
using RulesAnatomyInitializationRules = Odyssey.Rules.Character.AnatomyInitializationRules;
using RulesAttributeCostRules = Odyssey.Rules.Character.AttributeCostRules;
using RulesResourceInitializationRules = Odyssey.Rules.Character.ResourceInitializationRules;
using RulesSkillCostRules = Odyssey.Rules.Character.SkillCostRules;

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

        // ---------------------------------------------------------------------------------------------------
        // ODY-S09-102: the four "cost" commands. Each reads the Character through the port, applies the Rules
        // (cap / recommendation gate / cost), and hands the decision to the repository together with the value
        // it was based on (decidedFromValue / decidedFromLevel). The repository re-checks that value under its
        // transaction lock and rejects a stale decision with CharacterRevisionConflict, on top of the existing
        // MechanicsRevision / entry-level revision gates -- so a change made between this read and the commit
        // can never be silently overwritten.
        //
        // The repository still reports failures in the original order (permission, entry-level revision, stale
        // basis, then the decided cap / recommendation / balance), so a caller sees exactly the failure it saw
        // before this refactor, including when several conditions hold at once. Nothing is written on any failure.
        // ---------------------------------------------------------------------------------------------------

        /// <summary>
        /// Attribute purchase. Reads the attribute's current BaseValue, decides <c>ExceedsNormalCap</c> and
        /// <c>CostForIncrease</c> with <c>AttributeCostRules</c>, then calls
        /// <see cref="ICharacterRepository.PurchaseAttributeIncrease"/>. For a non-increase (<c>toValue</c> not above the
        /// current value) no Rules call is made and the repository raises the same argument error as before.
        /// </summary>
        public static Result<CharacterRecord> PurchaseAttributeIncrease(
            ICharacterRepository characters,
            CampaignHandle campaign,
            CharacterId characterId,
            AttributeDefinitionId attributeDefinitionId,
            long toValue,
            UserId actorUserId,
            bool actorIsMainGm,
            long expectedMechanicsRevision,
            long expectedAttributeRevision,
            CommandId commandId,
            CorrelationId correlationId)
        {
            if (characters == null) throw new ArgumentNullException(nameof(characters));
            if (!attributeDefinitionId.IsValid) throw new ArgumentException("AttributeDefinitionId is required.", nameof(attributeDefinitionId));
            if (toValue < 0) throw new ArgumentOutOfRangeException(nameof(toValue));
            if (!actorUserId.IsValid) throw new ArgumentException("ActorUserId is required.", nameof(actorUserId));
            if (expectedAttributeRevision < 0) throw new ArgumentOutOfRangeException(nameof(expectedAttributeRevision));
            ValidateMechanicsCommand(campaign, characterId, expectedMechanicsRevision, commandId);

            Result<CharacterRecord> read = characters.GetCharacter(campaign, characterId, correlationId);
            if (read.IsFailure) return Result<CharacterRecord>.Failure(read.Error);

            long fromValue = 0;
            foreach (AttributeValue candidate in read.Value.Attributes)
            {
                if (candidate.AttributeDefinitionId.Equals(attributeDefinitionId)) { fromValue = candidate.BaseValue; break; }
            }

            bool exceedsNormalCap = false;
            long cost = 0;
            if (toValue > fromValue)
            {
                exceedsNormalCap = RulesAttributeCostRules.ExceedsNormalCap(toValue);
                cost = RulesAttributeCostRules.CostForIncrease(fromValue, toValue);
            }

            return characters.PurchaseAttributeIncrease(
                campaign, characterId, attributeDefinitionId, toValue, fromValue, exceedsNormalCap, cost,
                actorUserId, actorIsMainGm, expectedMechanicsRevision, expectedAttributeRevision, commandId, correlationId);
        }

        /// <summary>
        /// Skill level purchase. Decides <c>RequiresRecommendation</c> (level 5+ belongs to the recommendation
        /// pipeline) and <c>CostForIncrease</c> with <c>SkillCostRules</c> from the skill's current Level, then calls
        /// <see cref="ICharacterRepository.PurchaseSkillLevel"/>.
        /// </summary>
        public static Result<CharacterRecord> PurchaseSkillLevel(
            ICharacterRepository characters,
            CampaignHandle campaign,
            CharacterId characterId,
            SkillDefinitionId skillDefinitionId,
            long toLevel,
            UserId actorUserId,
            bool actorIsMainGm,
            long expectedMechanicsRevision,
            long expectedSkillRevision,
            CommandId commandId,
            CorrelationId correlationId)
        {
            if (characters == null) throw new ArgumentNullException(nameof(characters));
            if (!skillDefinitionId.IsValid) throw new ArgumentException("SkillDefinitionId is required.", nameof(skillDefinitionId));
            if (toLevel < 0) throw new ArgumentOutOfRangeException(nameof(toLevel));
            if (!actorUserId.IsValid) throw new ArgumentException("ActorUserId is required.", nameof(actorUserId));
            if (expectedSkillRevision < 0) throw new ArgumentOutOfRangeException(nameof(expectedSkillRevision));
            ValidateMechanicsCommand(campaign, characterId, expectedMechanicsRevision, commandId);

            Result<CharacterRecord> read = characters.GetCharacter(campaign, characterId, correlationId);
            if (read.IsFailure) return Result<CharacterRecord>.Failure(read.Error);

            long fromLevel = ReadSkillLevel(read.Value, skillDefinitionId);

            bool requiresRecommendation = RulesSkillCostRules.RequiresRecommendation(toLevel);
            long cost = 0;
            if (!requiresRecommendation && toLevel > fromLevel)
            {
                cost = RulesSkillCostRules.CostForIncrease(fromLevel, toLevel);
            }

            return characters.PurchaseSkillLevel(
                campaign, characterId, skillDefinitionId, toLevel, fromLevel, requiresRecommendation, cost,
                actorUserId, actorIsMainGm, expectedMechanicsRevision, expectedSkillRevision, commandId, correlationId);
        }

        /// <summary>
        /// Skill advancement recommendation request. Decides the reserved amount (<c>SkillCostRules.CostForIncrease</c>
        /// from the skill's current Level to <c>targetLevel</c>), then calls
        /// <see cref="ICharacterRepository.RequestSkillAdvancedRecommendation"/>.
        /// </summary>
        public static Result<AdvancementRecommendationRecord> RequestSkillAdvancedRecommendation(
            ICharacterRepository characters,
            CampaignHandle campaign,
            CharacterId characterId,
            SkillDefinitionId skillDefinitionId,
            long targetLevel,
            IReadOnlyList<CriticalSuccessEvidenceId> evidenceIds,
            UserId actorUserId,
            bool actorIsMainGm,
            long expectedMechanicsRevision,
            CommandId commandId,
            CorrelationId correlationId)
        {
            if (characters == null) throw new ArgumentNullException(nameof(characters));
            if (!skillDefinitionId.IsValid) throw new ArgumentException("SkillDefinitionId is required.", nameof(skillDefinitionId));
            if (targetLevel < 1) throw new ArgumentOutOfRangeException(nameof(targetLevel));
            if (evidenceIds == null) throw new ArgumentNullException(nameof(evidenceIds));
            if (!actorUserId.IsValid) throw new ArgumentException("ActorUserId is required.", nameof(actorUserId));
            ValidateMechanicsCommand(campaign, characterId, expectedMechanicsRevision, commandId);

            Result<CharacterRecord> read = characters.GetCharacter(campaign, characterId, correlationId);
            if (read.IsFailure) return Result<AdvancementRecommendationRecord>.Failure(read.Error);

            long fromLevel = ReadSkillLevel(read.Value, skillDefinitionId);
            long reservedAmount = targetLevel > fromLevel ? RulesSkillCostRules.CostForIncrease(fromLevel, targetLevel) : 0;

            return characters.RequestSkillAdvancedRecommendation(
                campaign, characterId, skillDefinitionId, targetLevel, fromLevel, reservedAmount, evidenceIds,
                actorUserId, actorIsMainGm, expectedMechanicsRevision, commandId, correlationId);
        }

        /// <summary>
        /// Ability acquisition. For <see cref="SourceKind.ProgressionPurchase"/> the cost is decided here
        /// (<c>AbilityCostRules.CostForAcquisition</c>, a constant that needs no Character state) and passed to
        /// <see cref="ICharacterRepository.AcquireAbility"/>; for every other <see cref="SourceKind"/> Rules is not
        /// involved and the repository ignores the cost argument.
        /// </summary>
        public static Result<CharacterRecord> AcquireAbility(
            ICharacterRepository characters,
            CampaignHandle campaign,
            CharacterId characterId,
            AbilityDefinitionId abilityDefinitionId,
            SourceKind sourceKind,
            string? sourceRef,
            RankMode rankMode,
            long? numericRank,
            string? namedRankKey,
            string configuration,
            UserId actorUserId,
            bool actorIsMainGm,
            long? expectedMechanicsRevision,
            long expectedCharacterAbilitiesRevision,
            CommandId commandId,
            CorrelationId correlationId)
        {
            if (characters == null) throw new ArgumentNullException(nameof(characters));

            long cost = sourceKind == SourceKind.ProgressionPurchase ? RulesAbilityCostRules.CostForAcquisition() : 0;

            return characters.AcquireAbility(
                campaign, characterId, abilityDefinitionId, sourceKind, sourceRef, rankMode, numericRank, namedRankKey,
                configuration, cost, actorUserId, actorIsMainGm, expectedMechanicsRevision, expectedCharacterAbilitiesRevision,
                commandId, correlationId);
        }

        // Same argument guards, in the same order, that the repository applied before it read anything -- so an
        // invalid call still throws the same exception it always did instead of reaching the read.
        private static void ValidateMechanicsCommand(CampaignHandle campaign, CharacterId characterId, long expectedMechanicsRevision, CommandId commandId)
        {
            if (campaign == null) throw new ArgumentNullException(nameof(campaign));
            if (!characterId.IsValid) throw new ArgumentException("CharacterId is required.", nameof(characterId));
            if (expectedMechanicsRevision < 1) throw new ArgumentOutOfRangeException(nameof(expectedMechanicsRevision));
            if (!commandId.IsValid) throw new ArgumentException("CommandId is required.", nameof(commandId));
        }

        private static long ReadSkillLevel(CharacterRecord character, SkillDefinitionId skillDefinitionId)
        {
            foreach (CharacterSkill candidate in character.Skills)
            {
                if (candidate.SkillDefinitionId.Equals(skillDefinitionId)) return candidate.Level;
            }

            return 0;
        }

        // ---------------------------------------------------------------------------------------------------
        // ODY-S09-103: respec. The plan (which purchases are returned, what is repurchased and at what cost) used to be
        // computed inside SqliteCharacterRepository; its only Rules use was the cost of each fresh purchase. The whole
        // computation now lives here, unchanged in outcome. Preview is a pure read and never touches a revision gate;
        // Apply recomputes the plan from a fresh read on every call (it takes no preview from the caller, so there is
        // nothing stale to trust -- CAP-INV-004) and hands it to the repository together with the MechanicsRevision the
        // read was made at. The repository re-checks that revision (and the plan against the locked purchase history)
        // under its transaction lock, so a change made between this read and the commit is rejected, not overwritten.
        // ---------------------------------------------------------------------------------------------------

        /// <summary>
        /// Read-only respec preview: reads the Character and its purchase history and computes the plan. No write, no
        /// revision argument -- the same semantics the repository query had. The two reads are not one transaction
        /// (they were two autocommit SELECTs before too); the result is advisory and <see cref="ApplyCharacterRespec"/>
        /// never uses it.
        /// </summary>
        public static Result<CharacterRespecPreview> PreviewCharacterRespec(
            ICharacterRepository characters,
            CampaignHandle campaign,
            CharacterId characterId,
            IReadOnlyList<CharacterRespecTarget> targets,
            CorrelationId correlationId)
        {
            if (characters == null) throw new ArgumentNullException(nameof(characters));
            if (campaign == null) throw new ArgumentNullException(nameof(campaign));
            if (!characterId.IsValid) throw new ArgumentException("CharacterId is required.", nameof(characterId));
            if (targets == null || targets.Count == 0) throw new ArgumentException("At least one target is required.", nameof(targets));

            Result<CharacterRecord> character = characters.GetCharacter(campaign, characterId, correlationId);
            if (character.IsFailure) return Result<CharacterRespecPreview>.Failure(character.Error);

            Result<IReadOnlyList<AdvancementPurchase>> purchases = characters.GetAdvancementPurchases(campaign, characterId, correlationId);
            if (purchases.IsFailure) return Result<CharacterRespecPreview>.Failure(purchases.Error);

            return ComputeRespecPlan(character.Value, purchases.Value, targets, correlationId);
        }

        /// <summary>
        /// Applies a respec: builds the plan from a fresh read (character first, then purchase history, so any mutation
        /// in between bumps the revision the plan is tagged with), then always calls
        /// <see cref="ICharacterRepository.ApplyCharacterRespec"/>. Nothing here short-circuits: reason / permission /
        /// not-found / unsupported-kind / revision failures are all reported by the repository in the same order as
        /// before. If a read fails, or the plan cannot be built (an unsupported target kind), the repository is still
        /// called -- with an empty plan and, for a failed read, a revision no Character can have (0) -- and produces the
        /// failure the old single-method implementation produced.
        /// </summary>
        public static Result<CharacterRecord> ApplyCharacterRespec(
            ICharacterRepository characters,
            CampaignHandle campaign,
            CharacterId characterId,
            IReadOnlyList<CharacterRespecTarget> targets,
            string reasonCode,
            UserId actorUserId,
            bool actorIsMainGm,
            long expectedMechanicsRevision,
            CommandId commandId,
            CorrelationId correlationId)
        {
            if (characters == null) throw new ArgumentNullException(nameof(characters));
            if (campaign == null) throw new ArgumentNullException(nameof(campaign));
            if (!characterId.IsValid) throw new ArgumentException("CharacterId is required.", nameof(characterId));
            if (targets == null || targets.Count == 0) throw new ArgumentException("At least one target is required.", nameof(targets));
            if (!actorUserId.IsValid) throw new ArgumentException("ActorUserId is required.", nameof(actorUserId));
            if (!commandId.IsValid) throw new ArgumentException("CommandId is required.", nameof(commandId));
            if (expectedMechanicsRevision < 1) throw new ArgumentOutOfRangeException(nameof(expectedMechanicsRevision));

            var emptyPlan = new CharacterRespecPreview(new List<CharacterRespecPlanEntry>(), 0, 0);
            CharacterRespecPreview plan = emptyPlan;
            long decidedMechanicsRevision = 0;

            Result<CharacterRecord> character = characters.GetCharacter(campaign, characterId, correlationId);
            if (character.IsSuccess)
            {
                Result<IReadOnlyList<AdvancementPurchase>> purchases = characters.GetAdvancementPurchases(campaign, characterId, correlationId);
                if (purchases.IsSuccess)
                {
                    decidedMechanicsRevision = character.Value.Revisions.MechanicsRevision;
                    Result<CharacterRespecPreview> computed = ComputeRespecPlan(character.Value, purchases.Value, targets, correlationId);
                    if (computed.IsSuccess) plan = computed.Value;
                }
            }

            return characters.ApplyCharacterRespec(
                campaign, characterId, targets, plan, decidedMechanicsRevision, reasonCode,
                actorUserId, actorIsMainGm, expectedMechanicsRevision, commandId, correlationId);
        }

        /// <summary>
        /// The respec plan: reads each addressed target's authoritative CURRENT value from the Character's own
        /// Attributes / Skills (never from the purchase history), returns every currently-Applied purchase of an
        /// addressed target as a Return entry, and adds one Spend entry per target whose DesiredValue exceeds zero,
        /// priced from zero with the attribute / skill cost rules. Moved verbatim from the repository
        /// (ODY-S04-107); an AbilityAcquisition target is rejected (ODY-S04-108 section 1.3).
        /// </summary>
        private static Result<CharacterRespecPreview> ComputeRespecPlan(CharacterRecord current, IReadOnlyList<AdvancementPurchase> allPurchases, IReadOnlyList<CharacterRespecTarget> targets, CorrelationId correlationId)
        {
            var entries = new List<CharacterRespecPlanEntry>();
            long totalReturned = 0;
            long totalSpent = 0;

            foreach (CharacterRespecTarget target in targets)
            {
                long currentValue;
                if (target.OperationKind == AdvancementOperationKind.AttributeIncrease)
                {
                    AttributeDefinitionId targetId = AttributeDefinitionId.Parse(target.TargetDefinitionId);
                    AttributeValue? existing = null;
                    foreach (AttributeValue candidate in current.Attributes)
                    {
                        if (candidate.AttributeDefinitionId.Equals(targetId)) { existing = candidate; break; }
                    }

                    currentValue = existing?.BaseValue ?? 0;
                }
                else if (target.OperationKind == AdvancementOperationKind.SkillLevelPurchase)
                {
                    SkillDefinitionId targetId = SkillDefinitionId.Parse(target.TargetDefinitionId);
                    CharacterSkill? existing = null;
                    foreach (CharacterSkill candidate in current.Skills)
                    {
                        if (candidate.SkillDefinitionId.Equals(targetId)) { existing = candidate; break; }
                    }

                    currentValue = existing?.Level ?? 0;
                }
                else
                {
                    return Result<CharacterRespecPreview>.Failure(PersistenceFailures.CharacterAdvancementOperationKindNotSupported(correlationId));
                }

                if (currentValue == target.DesiredValue) continue;

                foreach (AdvancementPurchase purchase in allPurchases)
                {
                    if (purchase.Status != AdvancementPurchaseStatus.Applied) continue;
                    if (purchase.OperationKind != target.OperationKind) continue;
                    if (!string.Equals(purchase.TargetDefinitionId, target.TargetDefinitionId, StringComparison.Ordinal)) continue;

                    entries.Add(new CharacterRespecPlanEntry(CharacterRespecPlanAction.Return, target.OperationKind, target.TargetDefinitionId, purchase.Cost, purchase.PurchaseId));
                    totalReturned += purchase.Cost;
                }

                if (target.DesiredValue > 0)
                {
                    long cost = target.OperationKind == AdvancementOperationKind.AttributeIncrease
                        ? RulesAttributeCostRules.CostForIncrease(0, target.DesiredValue)
                        : RulesSkillCostRules.CostForIncrease(0, target.DesiredValue);

                    entries.Add(new CharacterRespecPlanEntry(CharacterRespecPlanAction.Spend, target.OperationKind, target.TargetDefinitionId, cost, null));
                    totalSpent += cost;
                }
            }

            return Result<CharacterRespecPreview>.Success(new CharacterRespecPreview(entries, totalReturned, totalSpent));
        }
    }
}
