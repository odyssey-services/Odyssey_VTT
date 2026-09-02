using System;

namespace Odyssey.Domain.Character
{
    /// <summary>
    /// ODY-S04-101: 10_Characters_And_Progression section 6.1's CharacterKind
    /// (PC/NPC/Creature). CAP-INV-001: a Vehicle or other interactive object is
    /// never one of these -- it reuses compatible components in its own,
    /// separate aggregate, not this enum.
    /// </summary>
    public enum CharacterKind
    {
        PlayerCharacter = 1,
        NonPlayerCharacter = 2,
        Creature = 3
    }

    /// <summary>
    /// ODY-S04-101: 10_Characters_And_Progression section 7's six-state
    /// lifecycle. This task implements only the structural values and the
    /// generic transition-table shape (<see cref="CharacterLifecycleTransitions"/>)
    /// -- which command may legally invoke a given edge (for example, that
    /// Dead only becomes reachable through a completed Rules Engine
    /// FatalDamagePending workflow or an explicit GMOverride, ADR-025 section 6)
    /// is business logic left entirely to ODY-S04-110/111.
    /// </summary>
    public enum CharacterLifecycleStatus
    {
        Draft = 1,
        Active = 2,
        Inactive = 3,
        Retired = 4,
        Dead = 5,
        Archived = 6
    }

    /// <summary>
    /// ODY-S04-111: ADR-025 section 6.1's structural discriminator for
    /// "who issued this transition into <see cref="CharacterLifecycleStatus.Dead"/>" --
    /// this codebase has no general <c>IssuerKind</c>/`HostSystem` actor
    /// mechanism anywhere yet (confirmed by search), and no real Rules
    /// Engine <c>FatalDamagePending</c> workflow exists to become a genuine
    /// <c>HostSystem</c> caller. This enum makes the two legal paths
    /// structurally exclusive at the call site -- there is no third value a
    /// plain owner/controller could pass to reach <c>Dead</c>
    /// (`CAP-INV-008`).
    /// </summary>
    public enum LifecycleDeathIssuerKind
    {
        /// <summary>ADR-002 section 6.4's `IssuerKind=HostSystem` -- accepted as a structurally legal entry point for a future Rules Engine `FatalDamagePending` workflow (not implemented by this task); no user permission check applies on this path, since it does not represent a user-issued command at all.</summary>
        HostSystemFatalDamageCompletion = 1,

        /// <summary>`IssuerKind=User`, `ActorUserId`=MainGM -- an explicit GM override. MainGM-only, checked like every other MainGM-gated command in this codebase.</summary>
        GMOverride = 2
    }

    /// <summary>
    /// ODY-S04-101: 10_Characters_And_Progression section 7.2's MVP
    /// ApprovalState. Submitted/ChangesRequested/Rejected are not stable
    /// values (ADR-023 section 7.4, already decided, not reopened here) --
    /// review feedback is commands/comments while the Character remains Draft.
    /// </summary>
    public enum CharacterApprovalState
    {
        Draft = 1,
        Approved = 2
    }

    /// <summary>
    /// ODY-S04-101: the generic <see cref="CharacterLifecycleStatus"/>
    /// adjacency table from 10_Characters_And_Progression section 7.1,
    /// verbatim:
    /// <code>
    /// Draft -&gt; Active
    /// Active &lt;-&gt; Inactive
    /// Active -&gt; Retired
    /// Inactive -&gt; Retired
    /// Active|Inactive|Retired -&gt; Dead
    /// Draft|Active|Inactive|Retired|Dead -&gt; Archived
    /// Dead -&gt; Active|Inactive|Retired (only via CharacterRestored, ODY-S04-111)
    /// Archived -&gt; previous non-deleted status (only via explicit RestoreFromArchive, ODY-S04-110)
    /// </code>
    /// This is a pure, generic shape check only -- it answers "is this edge
    /// structurally part of the lifecycle graph at all," not "may this actor,
    /// using this command, take this edge right now." The latter is each
    /// business-logic task's own job (ODY-S04-110/111), reusing this table
    /// rather than re-deriving or duplicating it.
    /// </summary>
    public static class CharacterLifecycleTransitions
    {
        public static bool IsValidTransition(CharacterLifecycleStatus from, CharacterLifecycleStatus to)
        {
            if (!Enum.IsDefined(typeof(CharacterLifecycleStatus), from)) throw new ArgumentOutOfRangeException(nameof(from));
            if (!Enum.IsDefined(typeof(CharacterLifecycleStatus), to)) throw new ArgumentOutOfRangeException(nameof(to));

            if (from == to)
            {
                // A no-op "transition" to the same status is not a graph edge;
                // callers that need idempotent no-op handling decide that
                // themselves (this task does not decide it for them).
                return false;
            }

            switch (from)
            {
                case CharacterLifecycleStatus.Draft:
                    return to == CharacterLifecycleStatus.Active || to == CharacterLifecycleStatus.Archived;
                case CharacterLifecycleStatus.Active:
                    return to == CharacterLifecycleStatus.Inactive
                        || to == CharacterLifecycleStatus.Retired
                        || to == CharacterLifecycleStatus.Dead
                        || to == CharacterLifecycleStatus.Archived;
                case CharacterLifecycleStatus.Inactive:
                    return to == CharacterLifecycleStatus.Active
                        || to == CharacterLifecycleStatus.Retired
                        || to == CharacterLifecycleStatus.Dead
                        || to == CharacterLifecycleStatus.Archived;
                case CharacterLifecycleStatus.Retired:
                    return to == CharacterLifecycleStatus.Dead || to == CharacterLifecycleStatus.Archived;
                case CharacterLifecycleStatus.Dead:
                    // Structurally reachable only through CharacterRestored
                    // (ODY-S04-111); Archived remains reachable per the table's
                    // own "Draft|Active|Inactive|Retired|Dead -> Archived" row.
                    return to == CharacterLifecycleStatus.Active
                        || to == CharacterLifecycleStatus.Inactive
                        || to == CharacterLifecycleStatus.Retired
                        || to == CharacterLifecycleStatus.Archived;
                case CharacterLifecycleStatus.Archived:
                    // Structurally reachable only through an explicit
                    // RestoreFromArchive operation (ODY-S04-110); this table
                    // only records that some non-deleted prior status is a
                    // legal edge target, not which one is correct for a given
                    // Character (that requires knowing the actual prior status,
                    // out of this pure function's scope).
                    return to == CharacterLifecycleStatus.Draft
                        || to == CharacterLifecycleStatus.Active
                        || to == CharacterLifecycleStatus.Inactive
                        || to == CharacterLifecycleStatus.Retired;
                default:
                    return false;
            }
        }
    }

    /// <summary>
    /// ODY-S04-101: ADR-022 section 5's fixed set of Character section
    /// revisions, plus the overall <see cref="CharacterRevision"/>. Every
    /// Character has exactly these twelve counters from creation onward, even
    /// though this task only wires real commands for
    /// <see cref="IdentityRevision"/>/<see cref="PresentationRevision"/> --
    /// the remaining columns exist now so later tasks (ODY-S04-102/105-111)
    /// never need a schema migration merely to start using a section ADR-022
    /// already reserved.
    /// </summary>
    public readonly struct CharacterSectionRevisions
    {
        public CharacterSectionRevisions(
            long characterRevision,
            long identityRevision,
            long presentationRevision,
            long customFieldsRevision,
            long mechanicsRevision,
            long attributeValuesRevision,
            long characterSkillsRevision,
            long characterAbilitiesRevision,
            long characterResourcesRevision,
            long characterAnatomyRevision,
            long ownershipRevision,
            long lifecycleRevision,
            long runtimeStateRevision)
        {
            CharacterRevision = characterRevision;
            IdentityRevision = identityRevision;
            PresentationRevision = presentationRevision;
            CustomFieldsRevision = customFieldsRevision;
            MechanicsRevision = mechanicsRevision;
            AttributeValuesRevision = attributeValuesRevision;
            CharacterSkillsRevision = characterSkillsRevision;
            CharacterAbilitiesRevision = characterAbilitiesRevision;
            CharacterResourcesRevision = characterResourcesRevision;
            CharacterAnatomyRevision = characterAnatomyRevision;
            OwnershipRevision = ownershipRevision;
            LifecycleRevision = lifecycleRevision;
            RuntimeStateRevision = runtimeStateRevision;
        }

        public static CharacterSectionRevisions Initial() => new CharacterSectionRevisions(1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1);

        public long CharacterRevision { get; }
        public long IdentityRevision { get; }
        public long PresentationRevision { get; }
        public long CustomFieldsRevision { get; }
        public long MechanicsRevision { get; }
        public long AttributeValuesRevision { get; }
        public long CharacterSkillsRevision { get; }
        public long CharacterAbilitiesRevision { get; }
        public long CharacterResourcesRevision { get; }
        public long CharacterAnatomyRevision { get; }
        public long OwnershipRevision { get; }
        public long LifecycleRevision { get; }
        public long RuntimeStateRevision { get; }
    }
}
