using Odyssey.Domain.Effects;

namespace Odyssey.Rules.Effects
{
    /// <summary>
    /// ODY-S05-503: `ADR-028` §7 rule 3's own `ReplaceIfStronger` comparison --
    /// "the host does not decide 'stronger' itself... `Odyssey.Rules` (already
    /// the deterministic-calculation owner per `ADR-027` §14) compares the new
    /// application's snapshot against the existing row's snapshot... and
    /// returns a boolean the host then acts on." This is genuinely new code,
    /// not a reuse of an existing comparison -- a direct repository search
    /// during this task confirmed <c>Odyssey.Rules</c> had no effect-stacking
    /// or "stronger" comparison of any kind before this file.
    ///
    /// `ADR-028` §18.3 explicitly REJECTED adding a strongly-typed generic
    /// "potency" field to <see cref="EffectMechanicsSnapshot"/>/`EffectDefinition`,
    /// because "no such generic cross-Ruleset potency scale exists anywhere in
    /// this codebase's already-accepted content model, and inventing one here
    /// would silently decide Ruleset-specific game-design semantics this ADR
    /// has no authority over." This class honors that rejection -- it adds no
    /// new field to either DTO.
    ///
    /// It also does not parse <see cref="EffectMechanicsSnapshot.Payload"/> as
    /// JSON to look for some ad-hoc "magnitude"/"power" key, even though
    /// `ADR-028` §7 rule 3's own text says the comparison happens "over the
    /// opaque `MechanicsPayloadRef`-resolved payload": a direct read of
    /// <c>Odyssey.Rules.csproj</c>/<c>Odyssey.Rules.asmdef</c> before writing
    /// this file confirmed the project has no JSON library dependency at all
    /// (it references only <c>Odyssey.Domain</c>) -- adding one would expand
    /// this module's own dependency graph, which is out of this task's scope
    /// (`ODY-S05-503`'s own allowed paths do not include project/assembly
    /// definition files), and no established payload schema exists to parse
    /// anyway (confirmed by search: every `EffectDefinition.MechanicsPayloadRef`
    /// in this codebase's own test fixtures is an opaque reference string like
    /// <c>"burn_snapshot_ref"</c>, never inline mechanics data).
    ///
    /// TEST FIXTURE-LEVEL HEURISTIC, matching
    /// <see cref="Odyssey.Rules.Character.AttributeCostRules"/>'s own explicit
    /// "not production Ruleset balance data" honesty: the only Domain-owned,
    /// always-present, unambiguously-ordered numeric signal already common to
    /// both snapshots is <see cref="EffectMechanicsSnapshot.DefinitionSnapshotVersion"/>
    /// -- the source `EffectDefinition`'s own version number at the moment
    /// each snapshot was captured (`ADR-027` §11). Applying a newer version of
    /// the same effect is treated as "stronger" than one applied from an older
    /// version. This is a real, deterministic, testable comparison over
    /// already-existing structured data -- not an invented potency scale --
    /// but it is still a stand-in for genuine Ruleset-defined potency, which
    /// this codebase does not yet have a schema for. A future task that adds
    /// real Ruleset-driven effect potency must replace this method's own body
    /// without changing <c>ActiveEffectStackingRules.ResolveStacking</c>'s call
    /// site or this method's signature.
    /// </summary>
    public static class EffectStackRules
    {
        /// <summary>
        /// True if <paramref name="candidate"/> should replace
        /// <paramref name="existing"/> under `EffectStackPolicy.ReplaceIfStronger`.
        /// A tie (equal <see cref="EffectMechanicsSnapshot.DefinitionSnapshotVersion"/>)
        /// is deliberately NOT stronger -- the caller then behaves as
        /// `IgnoreNewApplication` (`ADR-028` §7 rule 3), the least destructive
        /// outcome when this heuristic cannot conclusively distinguish the two.
        /// </summary>
        public static bool IsStronger(EffectMechanicsSnapshot candidate, EffectMechanicsSnapshot existing)
        {
            return candidate.DefinitionSnapshotVersion > existing.DefinitionSnapshotVersion;
        }
    }
}
