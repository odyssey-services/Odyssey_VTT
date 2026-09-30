using System;
using System.Collections.Generic;
using Odyssey.Application.Combat;
using Odyssey.Application.Commands;
using Odyssey.Application.Effects;
using Odyssey.Application.GameLog;
using Odyssey.Application.Persistence;
using Odyssey.Application.Results;
using Odyssey.Domain.Combat;
using Odyssey.Domain.Effects;
using Odyssey.Domain.Identity;
using UnityEngine.UIElements;

namespace Odyssey.Unity.Client
{
    /// <summary>A possible stacking conflict: the attack command that raised it and an existing effect on the target.</summary>
    public sealed class StackConflictCandidate
    {
        public StackConflictCandidate(CommandId raisingCommandId, ActiveEffectRecord existing, CharacterId targetId)
        {
            RaisingCommandId = raisingCommandId;
            Existing = existing ?? throw new ArgumentNullException(nameof(existing));
            TargetId = targetId;
        }

        public CommandId RaisingCommandId { get; }
        public ActiveEffectRecord Existing { get; }
        public CharacterId TargetId { get; }
        public string Key => RaisingCommandId + "|" + Existing.Effect.ActiveEffectId;
    }

    // ODY-S11-205: MainGM tools -- attack interventions, stacking conflicts, combat journal + log correction.
    public sealed partial class CombatPanelPresenter
    {
        public const string AttackResolvedEntryType = "AttackResolved";
        public const string AttackCompensatedEntryType = "AttackCompensated";

        private readonly List<AttackOutcomeRecord> _sessionAttacks = new List<AttackOutcomeRecord>();
        private readonly List<GameLogEntryRecord> _journal = new List<GameLogEntryRecord>();
        private readonly List<StackConflictCandidate> _conflicts = new List<StackConflictCandidate>();
        private readonly HashSet<string> _resolvedConflicts = new HashSet<string>(StringComparer.Ordinal);

        /// <summary>Attacks resolved in this session that still wait for the MainGM (refreshed from the backend).</summary>
        public IReadOnlyList<AttackOutcomeRecord> PendingAttacks
        {
            get
            {
                var pending = new List<AttackOutcomeRecord>();
                foreach (AttackOutcomeRecord outcome in _sessionAttacks) if (outcome.OutcomeKind == AttackOutcomeKind.Pending) pending.Add(outcome);
                return pending;
            }
        }

        /// <summary>Journal entries visible to the current actor (audience-filtered by the backend's own rules).</summary>
        public IReadOnlyList<GameLogEntryRecord> Journal => _journal;

        public IReadOnlyList<StackConflictCandidate> ConflictCandidates => _conflicts;

        public Result<AttackOutcomeRecord> ResolveIntervention(CommandId pendingCommandId, AttackInterventionResolution resolution)
        {
            if (!ActorIsMainGm)
            {
                Banner.Show(OdyBannerKind.Warning, "Only the MainGM decides pending attacks.");
                return Result<AttackOutcomeRecord>.Failure(UiGuard.InvalidRequest());
            }

            Result<AttackOutcomeRecord> resolved = UiGuard.Run(() => AttackApplyService.ResolveAttackIntervention(_ports.AttackApply, _context.Campaign, pendingCommandId, resolution, _context.ActorUserId, UiCommandIds.NewCommandId(), UiCommandIds.NewCorrelationId()));
            if (resolved.IsFailure)
            {
                ReportFailure("Intervention", resolved.Error);
                return resolved;
            }

            RememberAttack(resolved.Value);
            Banner.Show(OdyBannerKind.Success, resolution + ": " + DescribeOutcome(resolved.Value));
            Refresh();
            return resolved;
        }

        public OdyConfirmDialog RequestIntervention(CommandId pendingCommandId, AttackInterventionResolution resolution) => OdyConfirmDialog.Show(_context.ModalHost,
            new OdyConfirmOptions(resolution + " this attack?", resolution == AttackInterventionResolution.Approve ? "The attack's damage and effects are applied." : "The attack is not applied.", resolution.ToString()) { Destructive = resolution != AttackInterventionResolution.Approve },
            _ => ResolveIntervention(pendingCommandId, resolution));

        /// <summary>
        /// "Correct log entry": adds a correcting journal entry for an applied attack (reason required). It never
        /// undoes damage or restores resources, and the original entry stays visible.
        /// </summary>
        public Result<AttackCompensationRecord> CorrectLogEntry(CommandId resolveAttackCommandId, string reason, string correctedSummary)
        {
            if (!ActorIsMainGm)
            {
                Banner.Show(OdyBannerKind.Warning, "Only the MainGM corrects the combat log.");
                return Result<AttackCompensationRecord>.Failure(UiGuard.InvalidRequest());
            }

            if (string.IsNullOrWhiteSpace(reason))
            {
                Banner.Show(OdyBannerKind.Error, "A reason is required to correct a log entry.");
                return Result<AttackCompensationRecord>.Failure(UiGuard.InvalidRequest());
            }

            string summary = string.IsNullOrWhiteSpace(correctedSummary) ? "Correction: " + reason.Trim() : correctedSummary.Trim();
            Result<AttackCompensationRecord> corrected = UiGuard.Run(() => AttackApplyService.CompensateAttackOutcome(_ports.AttackApply, _context.Campaign, resolveAttackCommandId, reason.Trim(), summary, _context.ActorUserId, UiCommandIds.NewCommandId(), UiCommandIds.NewCorrelationId()));
            if (corrected.IsFailure)
            {
                ReportFailure("Correct log entry", corrected.Error);
                return corrected;
            }

            Banner.Show(OdyBannerKind.Success, "Correction added to the log. Damage and resources are unchanged.");
            Refresh();
            return corrected;
        }

        public OdyConfirmDialog RequestCorrectLogEntry(CommandId resolveAttackCommandId) => OdyConfirmDialog.Show(_context.ModalHost,
            new OdyConfirmOptions("Correct this log entry?", "A correcting entry is added next to the original. This does not undo the attack: damage and resources stay as they are.", "Add correction") { RequiredTextLabel = "Reason" },
            reason => CorrectLogEntry(resolveAttackCommandId, reason, string.Empty));

        public Result<CombatStackConflictRecord> ResolveStackConflict(StackConflictCandidate candidate, ActiveEffectStackConflictResolution resolution)
        {
            if (candidate == null) throw new ArgumentNullException(nameof(candidate));
            if (!ActorIsMainGm)
            {
                Banner.Show(OdyBannerKind.Warning, "Only the MainGM resolves stacking conflicts.");
                return Result<CombatStackConflictRecord>.Failure(UiGuard.InvalidRequest());
            }

            Result<CombatStackConflictRecord> resolved = UiGuard.Run(() => AttackApplyService.ResolveStackConflict(_ports.AttackApply, _context.Campaign, candidate.RaisingCommandId, candidate.Existing.Effect.ActiveEffectId, resolution, _context.ActorUserId, UiCommandIds.NewCommandId(), UiCommandIds.NewCorrelationId()));
            if (resolved.IsFailure)
            {
                // "not found" / "already resolved": this pair was not (or is no longer) a pending conflict.
                if (resolved.Error.UserMessageKey.ToString() == "errors.persistence.combat_stack_conflict_not_found" || resolved.Error.UserMessageKey.ToString() == "errors.persistence.combat_stack_conflict_already_resolved") _resolvedConflicts.Add(candidate.Key);
                ReportFailure("Stacking conflict", resolved.Error);
                Render();
                return resolved;
            }

            _resolvedConflicts.Add(candidate.Key);
            Banner.Show(OdyBannerKind.Success, "Conflict resolved: " + EnumChoices.Humanize(resolution.ToString()) + ".");
            Refresh();
            return resolved;
        }

        // ---- internals ------------------------------------------------------------------------------

        private void RememberAttack(AttackOutcomeRecord outcome)
        {
            for (int index = 0; index < _sessionAttacks.Count; index++)
            {
                if (_sessionAttacks[index].ResolveAttackCommandId.Equals(outcome.ResolveAttackCommandId))
                {
                    _sessionAttacks[index] = outcome;
                    return;
                }
            }

            _sessionAttacks.Add(outcome);
        }

        private void LoadJournal()
        {
            CorrelationId correlationId = UiCommandIds.NewCorrelationId();
            for (int index = 0; index < _sessionAttacks.Count; index++)
            {
                Result<AttackOutcomeRecord> fresh = _ports.AttackApply.GetOutcome(_context.Campaign, _sessionAttacks[index].ResolveAttackCommandId, correlationId);
                if (fresh.IsSuccess) _sessionAttacks[index] = fresh.Value;
            }

            _journal.Clear();
            Result<IReadOnlyList<GameLogEntryRecord>> listed = _ports.GameLog.ListGameLog(_context.Campaign, correlationId);
            if (listed.IsSuccess)
            {
                RoleSelectionSnapshot role = _context.Selection.Current;
                _journal.AddRange(GameLogReconnectService.GetVisibleEntries(listed.Value, role.ActorUserId, role.Role, _ports.Groups));
            }

            LoadConflictCandidates(correlationId);
        }

        // No conflict listing exists: candidates are existing effects on the targets of this session's attacks that
        // carried effect candidates, keyed by the raising (attack) command. Resolving a non-conflict fails readably.
        private void LoadConflictCandidates(CorrelationId correlationId)
        {
            _conflicts.Clear();
            if (!ActorIsMainGm) return;
            foreach (AttackOutcomeRecord attack in _sessionAttacks)
            {
                if (attack.EffectCandidates.Count == 0) continue;
                var raising = new List<CommandId> { attack.ResolveAttackCommandId };
                if (attack.ResolvedByCommandId.HasValue) raising.Add(attack.ResolvedByCommandId.Value);
                foreach (AttackEffectCandidate candidate in attack.EffectCandidates)
                {
                    Result<IReadOnlyList<ActiveEffectRecord>> effects = _ports.Effects.ListActiveEffectsByTarget(_context.Campaign, _context.Campaign.CampaignId, ActiveEffectTargetRef.ForCharacter(candidate.TargetId), correlationId);
                    if (effects.IsFailure) continue;
                    foreach (ActiveEffectRecord existing in effects.Value)
                    {
                        if (!existing.Effect.EffectDefinitionRef.DefinitionId.Equals(candidate.EffectRef.DefinitionId)) continue;
                        foreach (CommandId command in raising)
                        {
                            var conflict = new StackConflictCandidate(command, existing, candidate.TargetId);
                            if (!_resolvedConflicts.Contains(conflict.Key)) _conflicts.Add(conflict);
                        }
                    }
                }
            }
        }

        private VisualElement BuildInterventionSection()
        {
            VisualElement section = OdyUi.Section("Waiting for the MainGM", "combat-interventions");
            IReadOnlyList<AttackOutcomeRecord> pending = PendingAttacks;
            if (pending.Count == 0)
            {
                section.Add(OdyUi.EmptyState("No attack is waiting for a decision."));
                return section;
            }

            foreach (AttackOutcomeRecord outcome in pending)
            {
                var row = new VisualElement();
                row.AddToClassList(OdyClasses.ListItem);
                var targets = new List<string>();
                foreach (CharacterId target in outcome.TargetIds) targets.Add(NameOf(target));
                var main = new VisualElement();
                main.AddToClassList(OdyClasses.ListItemMain);
                main.Add(OdyUi.Text(NameOf(outcome.ActorId) + " → " + string.Join(", ", targets), OdyClasses.ListItemTitle));
                main.Add(OdyUi.Text(outcome.DamageDeltas.Count == 0 ? "no damage" : DescribeDeltas(outcome.DamageDeltas), OdyClasses.ListItemMeta));
                row.Add(main);
                row.Add(OdyUi.Badge("Pending", OdyStatusKind.Pending));
                if (ActorIsMainGm)
                {
                    CommandId id = outcome.ResolveAttackCommandId;
                    row.Add(OdyUi.Button("Approve", () => RequestIntervention(id, AttackInterventionResolution.Approve), OdyButtonVariant.Primary, "combat-intervention-approve-" + id, small: true));
                    row.Add(OdyUi.Button("Reject", () => RequestIntervention(id, AttackInterventionResolution.Reject), OdyButtonVariant.Danger, "combat-intervention-reject-" + id, small: true));
                    row.Add(OdyUi.Button("Cancel", () => RequestIntervention(id, AttackInterventionResolution.Cancel), OdyButtonVariant.Ghost, "combat-intervention-cancel-" + id, small: true));
                }

                section.Add(row);
            }

            if (!ActorIsMainGm) section.Add(OdyUi.Text("The MainGM approves, rejects or cancels pending attacks.", OdyClasses.FieldHint));
            return section;
        }

        private VisualElement BuildStackConflictSection()
        {
            VisualElement section = OdyUi.Section("Stacking conflicts", "combat-stack-conflicts");
            if (!ActorIsMainGm)
            {
                section.Add(OdyUi.Text("The MainGM resolves effect stacking conflicts.", OdyClasses.FieldHint));
                return section;
            }

            if (_conflicts.Count == 0)
            {
                section.Add(OdyUi.EmptyState("No possible conflicts from this session's attacks."));
                return section;
            }

            section.Add(OdyUi.Text("Candidates from this session's attacks (the backend has no conflict list). Resolving a pair that is not a pending conflict is refused.", OdyClasses.FieldHint));
            foreach (StackConflictCandidate conflict in _conflicts)
            {
                var row = new VisualElement();
                row.AddToClassList(OdyClasses.ListItem);
                row.Add(OdyUi.Text(NameOf(conflict.TargetId) + ": existing effect ×" + conflict.Existing.Effect.StackCount, OdyClasses.ListItemTitle, OdyClasses.Grow));
                row.Add(OdyUi.Badge("Conflict?", OdyStatusKind.Conflict));
                StackConflictCandidate captured = conflict;
                row.Add(OdyUi.Button("Independent", () => ResolveStackConflict(captured, ActiveEffectStackConflictResolution.ApplyAsIndependentInstance), OdyButtonVariant.Secondary, "combat-conflict-independent", small: true));
                row.Add(OdyUi.Button("Replace", () => ResolveStackConflict(captured, ActiveEffectStackConflictResolution.Replace), OdyButtonVariant.Secondary, "combat-conflict-replace", small: true));
                row.Add(OdyUi.Button("Ignore", () => ResolveStackConflict(captured, ActiveEffectStackConflictResolution.Ignore), OdyButtonVariant.Ghost, "combat-conflict-ignore", small: true));
                section.Add(row);
            }

            return section;
        }

        private VisualElement BuildJournalSection()
        {
            VisualElement section = OdyUi.Section("Combat log", "combat-journal");
            var list = new VisualElement { name = "combat-journal-list" };
            list.AddToClassList(OdyClasses.List);
            int shown = 0;
            foreach (GameLogEntryRecord entry in _journal)
            {
                bool isAttack = entry.EntryType == AttackResolvedEntryType;
                bool isCorrection = entry.EntryType == AttackCompensatedEntryType;
                if (!isAttack && !isCorrection) continue;
                shown++;
                var row = new VisualElement { name = "combat-journal-entry-" + entry.LogEntryId };
                row.AddToClassList(OdyClasses.ListItem);
                var main = new VisualElement();
                main.AddToClassList(OdyClasses.ListItemMain);
                main.Add(OdyUi.Text(NameOfRef(entry.SummaryPayload), OdyClasses.ListItemTitle));
                main.Add(OdyUi.Text("#" + entry.AuthoritativeSequence + " · " + entry.CreatedAt.Value.ToString("u"), OdyClasses.ListItemMeta));
                row.Add(main);
                row.Add(OdyUi.Badge(isCorrection ? "Correction" : "Attack", isCorrection ? OdyStatusKind.Info : OdyStatusKind.Neutral));
                if (isAttack && ActorIsMainGm)
                {
                    CommandId root = entry.RootCommandId;
                    row.Add(OdyUi.Button("Correct log entry", () => RequestCorrectLogEntry(root), OdyButtonVariant.Ghost, "combat-journal-correct-" + entry.LogEntryId, small: true));
                }

                list.Add(row);
            }

            if (shown == 0) list.Add(OdyUi.EmptyState("No combat entries yet.", "combat-journal-empty"));
            section.Add(list);
            section.Add(OdyUi.Text("A correction is a new entry next to the original; it never undoes damage or restores resources.", OdyClasses.FieldHint));
            return section;
        }
    }
}
