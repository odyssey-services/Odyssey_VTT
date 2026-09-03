using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Odyssey.Domain.Character;
using Odyssey.Domain.Identity;
using Odyssey.Domain.Time;

namespace Odyssey.Rules.Character
{
    /// <summary>
    /// ODY-S04-113: a caller-supplied fixture naming which definition IDs
    /// the TARGET Ruleset version recognizes, per category. TEST FIXTURE
    /// SHAPE ONLY -- no real Ruleset-content-catalog mechanism exists
    /// anywhere in this codebase (confirmed by search across
    /// <c>Odyssey.Rules</c>/<c>Odyssey.Content</c> before writing this file),
    /// exactly the same gap <see cref="AttributeCostRules"/>'s own doc
    /// comment already records for cost/cap data. A future Ruleset-catalog
    /// task replaces the caller's own construction of this object with a
    /// real content-driven lookup, without changing
    /// <see cref="RulesetMigrationRules.BuildPlan"/>'s own call site.
    /// </summary>
    public sealed class RulesetDefinitionCatalog
    {
        public RulesetDefinitionCatalog(
            IReadOnlyCollection<string> recognizedAttributeDefinitionIds,
            IReadOnlyCollection<string> recognizedSkillDefinitionIds,
            IReadOnlyCollection<string> recognizedAbilityDefinitionIds,
            IReadOnlyCollection<string> recognizedResourceDefinitionIds)
        {
            RecognizedAttributeDefinitionIds = recognizedAttributeDefinitionIds ?? throw new ArgumentNullException(nameof(recognizedAttributeDefinitionIds));
            RecognizedSkillDefinitionIds = recognizedSkillDefinitionIds ?? throw new ArgumentNullException(nameof(recognizedSkillDefinitionIds));
            RecognizedAbilityDefinitionIds = recognizedAbilityDefinitionIds ?? throw new ArgumentNullException(nameof(recognizedAbilityDefinitionIds));
            RecognizedResourceDefinitionIds = recognizedResourceDefinitionIds ?? throw new ArgumentNullException(nameof(recognizedResourceDefinitionIds));
        }

        public IReadOnlyCollection<string> RecognizedAttributeDefinitionIds { get; }
        public IReadOnlyCollection<string> RecognizedSkillDefinitionIds { get; }
        public IReadOnlyCollection<string> RecognizedAbilityDefinitionIds { get; }
        public IReadOnlyCollection<string> RecognizedResourceDefinitionIds { get; }
    }

    /// <summary>ODY-S04-113: product section 25's category names, restricted to the four this task actually maps -- see this task's own ExecPlan section 5 for why Anatomy is excluded.</summary>
    public enum RulesetDefinitionCategory
    {
        Attribute = 1,
        Skill = 2,
        Ability = 3,
        Resource = 4
    }

    /// <summary>ADR-025 section 7.2: one resolved identity mapping. This task never populates a real value transformation -- <see cref="SourceDefinitionId"/> always equals <see cref="TargetDefinitionId"/> for every entry this task's own code produces.</summary>
    public sealed class RulesetDefinitionMapping
    {
        public RulesetDefinitionMapping(RulesetDefinitionCategory category, string sourceDefinitionId, string targetDefinitionId)
        {
            Category = category;
            SourceDefinitionId = sourceDefinitionId ?? throw new ArgumentNullException(nameof(sourceDefinitionId));
            TargetDefinitionId = targetDefinitionId ?? throw new ArgumentNullException(nameof(targetDefinitionId));
        }

        public RulesetDefinitionCategory Category { get; }
        public string SourceDefinitionId { get; }
        public string TargetDefinitionId { get; }
    }

    /// <summary>ADR-025 section 7.2: one definition the target Ruleset's own catalog does not recognize -- surfaced for the GM to decide, never silently dropped or guessed at (section 4 of this task's own ExecPlan).</summary>
    public sealed class RulesetUnresolvedDecision
    {
        public RulesetUnresolvedDecision(RulesetDefinitionCategory category, string definitionId, string reason)
        {
            Category = category;
            DefinitionId = definitionId ?? throw new ArgumentNullException(nameof(definitionId));
            Reason = reason ?? throw new ArgumentNullException(nameof(reason));
        }

        public RulesetDefinitionCategory Category { get; }
        public string DefinitionId { get; }
        public string Reason { get; }
    }

    /// <summary>
    /// ODY-S04-113: 10_Characters_And_Progression section 25's
    /// <c>CharacterRulesetMigrationPlan</c> tree. <see cref="ValueChanges"/>
    /// is always empty in this task's own code -- no cross-Ruleset-version
    /// value-transformation algorithm is decided by any ADR or invented
    /// here (this task's own ExecPlan section 4/5); the field exists only
    /// to match product's own structural shape for a future task that does
    /// populate it. The four <c>ExpectedXRevision</c> fields and
    /// <see cref="PreviewHash"/> exist purely to let
    /// <c>ApplyCharacterRulesetMigration</c> detect a stale/tampered plan
    /// (`CAP-INV-004`) -- not part of product section 25's own tree, an
    /// additive implementation detail.
    /// </summary>
    public sealed class CharacterRulesetMigrationPlan
    {
        public CharacterRulesetMigrationPlan(
            CharacterId characterId,
            string sourceRulesetId,
            string sourceRulesetVersion,
            string targetRulesetId,
            string targetRulesetVersion,
            IReadOnlyList<RulesetDefinitionMapping> definitionMappings,
            IReadOnlyList<RulesetUnresolvedDecision> unresolvedDecisions,
            long expectedMechanicsRevision,
            long expectedCharacterAbilitiesRevision,
            long expectedCharacterResourcesRevision,
            string previewHash)
        {
            if (!characterId.IsValid) throw new ArgumentException("CharacterId is required.", nameof(characterId));
            if (string.IsNullOrWhiteSpace(sourceRulesetId)) throw new ArgumentException("SourceRulesetId is required.", nameof(sourceRulesetId));
            if (string.IsNullOrWhiteSpace(sourceRulesetVersion)) throw new ArgumentException("SourceRulesetVersion is required.", nameof(sourceRulesetVersion));
            if (string.IsNullOrWhiteSpace(targetRulesetId)) throw new ArgumentException("TargetRulesetId is required.", nameof(targetRulesetId));
            if (string.IsNullOrWhiteSpace(targetRulesetVersion)) throw new ArgumentException("TargetRulesetVersion is required.", nameof(targetRulesetVersion));
            if (string.IsNullOrWhiteSpace(previewHash)) throw new ArgumentException("PreviewHash is required.", nameof(previewHash));

            CharacterId = characterId;
            SourceRulesetId = sourceRulesetId;
            SourceRulesetVersion = sourceRulesetVersion;
            TargetRulesetId = targetRulesetId;
            TargetRulesetVersion = targetRulesetVersion;
            DefinitionMappings = definitionMappings ?? throw new ArgumentNullException(nameof(definitionMappings));
            UnresolvedDecisions = unresolvedDecisions ?? throw new ArgumentNullException(nameof(unresolvedDecisions));
            ValueChanges = Array.Empty<string>();
            ExpectedMechanicsRevision = expectedMechanicsRevision;
            ExpectedCharacterAbilitiesRevision = expectedCharacterAbilitiesRevision;
            ExpectedCharacterResourcesRevision = expectedCharacterResourcesRevision;
            PreviewHash = previewHash;
        }

        public CharacterId CharacterId { get; }
        public string SourceRulesetId { get; }
        public string SourceRulesetVersion { get; }
        public string TargetRulesetId { get; }
        public string TargetRulesetVersion { get; }

        /// <summary>Always empty -- see this class's own doc comment.</summary>
        public IReadOnlyList<string> ValueChanges { get; }
        public IReadOnlyList<RulesetDefinitionMapping> DefinitionMappings { get; }
        public IReadOnlyList<RulesetUnresolvedDecision> UnresolvedDecisions { get; }
        public long ExpectedMechanicsRevision { get; }
        public long ExpectedCharacterAbilitiesRevision { get; }
        public long ExpectedCharacterResourcesRevision { get; }
        public string PreviewHash { get; }

        /// <summary>Product section 25's own tree names a <c>Status</c> leaf; derived, never independently set, so it can never drift from <see cref="UnresolvedDecisions"/>'s own content.</summary>
        public bool HasUnresolvedDecisions => UnresolvedDecisions.Count > 0;
    }

    /// <summary>
    /// ODY-S04-113: ADR-025 section 9's assignment of the actual mapping
    /// computation to <c>Odyssey.Rules</c>. TEST FIXTURE ONLY -- see
    /// <see cref="RulesetDefinitionCatalog"/>'s own doc comment. This class
    /// deliberately never invents a cross-Ruleset-version value
    /// transformation (this task's own ExecPlan section 4/5); it only
    /// decides, for each currently-purchased definition, whether the target
    /// Ruleset's own catalog still recognizes it.
    /// </summary>
    public static class RulesetMigrationRules
    {
        public static CharacterRulesetMigrationPlan BuildPlan(
            CharacterId characterId,
            string sourceRulesetId,
            string sourceRulesetVersion,
            string targetRulesetId,
            string targetRulesetVersion,
            IReadOnlyList<AttributeValue> attributes,
            IReadOnlyList<CharacterSkill> skills,
            IReadOnlyList<CharacterAbility> abilities,
            IReadOnlyList<CharacterResource> resources,
            RulesetDefinitionCatalog targetCatalog,
            long expectedMechanicsRevision,
            long expectedCharacterAbilitiesRevision,
            long expectedCharacterResourcesRevision)
        {
            if (targetCatalog == null) throw new ArgumentNullException(nameof(targetCatalog));

            var mappings = new List<RulesetDefinitionMapping>();
            var unresolved = new List<RulesetUnresolvedDecision>();

            foreach (AttributeValue attribute in attributes)
            {
                string id = attribute.AttributeDefinitionId.ToString();
                if (targetCatalog.RecognizedAttributeDefinitionIds.Contains(id))
                {
                    mappings.Add(new RulesetDefinitionMapping(RulesetDefinitionCategory.Attribute, id, id));
                }
                else
                {
                    unresolved.Add(new RulesetUnresolvedDecision(RulesetDefinitionCategory.Attribute, id, "Target Ruleset does not recognize this AttributeDefinitionId."));
                }
            }

            foreach (CharacterSkill skill in skills)
            {
                string id = skill.SkillDefinitionId.ToString();
                if (targetCatalog.RecognizedSkillDefinitionIds.Contains(id))
                {
                    mappings.Add(new RulesetDefinitionMapping(RulesetDefinitionCategory.Skill, id, id));
                }
                else
                {
                    unresolved.Add(new RulesetUnresolvedDecision(RulesetDefinitionCategory.Skill, id, "Target Ruleset does not recognize this SkillDefinitionId."));
                }
            }

            foreach (CharacterAbility ability in abilities)
            {
                string id = ability.AbilityDefinitionId.ToString();
                if (targetCatalog.RecognizedAbilityDefinitionIds.Contains(id))
                {
                    mappings.Add(new RulesetDefinitionMapping(RulesetDefinitionCategory.Ability, id, id));
                }
                else
                {
                    unresolved.Add(new RulesetUnresolvedDecision(RulesetDefinitionCategory.Ability, id, "Target Ruleset does not recognize this AbilityDefinitionId."));
                }
            }

            foreach (CharacterResource resource in resources)
            {
                string id = resource.ResourceDefinitionId.ToString();
                if (targetCatalog.RecognizedResourceDefinitionIds.Contains(id))
                {
                    mappings.Add(new RulesetDefinitionMapping(RulesetDefinitionCategory.Resource, id, id));
                }
                else
                {
                    unresolved.Add(new RulesetUnresolvedDecision(RulesetDefinitionCategory.Resource, id, "Target Ruleset does not recognize this ResourceDefinitionId."));
                }
            }

            string previewHash = ComputePreviewHash(characterId, sourceRulesetId, sourceRulesetVersion, targetRulesetId, targetRulesetVersion, mappings, unresolved, expectedMechanicsRevision, expectedCharacterAbilitiesRevision, expectedCharacterResourcesRevision);

            return new CharacterRulesetMigrationPlan(characterId, sourceRulesetId, sourceRulesetVersion, targetRulesetId, targetRulesetVersion, mappings, unresolved, expectedMechanicsRevision, expectedCharacterAbilitiesRevision, expectedCharacterResourcesRevision, previewHash);
        }

        /// <summary>
        /// CAP-INV-004: a deterministic digest over every field the plan's
        /// own correctness depends on. <c>ApplyCharacterRulesetMigration</c>
        /// recomputes this identically from a fresh read of live state and
        /// rejects a mismatch as stale/tampered, before ever calling this
        /// method a second time with a client-supplied plan trusted as final.
        /// </summary>
        public static string ComputePreviewHash(
            CharacterId characterId,
            string sourceRulesetId,
            string sourceRulesetVersion,
            string targetRulesetId,
            string targetRulesetVersion,
            IReadOnlyList<RulesetDefinitionMapping> mappings,
            IReadOnlyList<RulesetUnresolvedDecision> unresolved,
            long expectedMechanicsRevision,
            long expectedCharacterAbilitiesRevision,
            long expectedCharacterResourcesRevision)
        {
            var builder = new StringBuilder();
            builder.Append(characterId).Append('|').Append(sourceRulesetId).Append('|').Append(sourceRulesetVersion).Append('|')
                .Append(targetRulesetId).Append('|').Append(targetRulesetVersion).Append('|')
                .Append(expectedMechanicsRevision).Append('|').Append(expectedCharacterAbilitiesRevision).Append('|').Append(expectedCharacterResourcesRevision).Append('|');

            foreach (RulesetDefinitionMapping mapping in mappings)
            {
                builder.Append('M').Append(mapping.Category).Append(':').Append(mapping.SourceDefinitionId).Append("->").Append(mapping.TargetDefinitionId).Append(';');
            }

            foreach (RulesetUnresolvedDecision decision in unresolved)
            {
                builder.Append('U').Append(decision.Category).Append(':').Append(decision.DefinitionId).Append(';');
            }

            byte[] bytes = Encoding.UTF8.GetBytes(builder.ToString());
            using var sha256 = System.Security.Cryptography.SHA256.Create();
            byte[] hash = sha256.ComputeHash(bytes);
            var hex = new StringBuilder(hash.Length * 2);
            foreach (byte b in hash) hex.Append(b.ToString("x2"));
            return hex.ToString();
        }
    }
}
