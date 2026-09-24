using System;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using Odyssey.Application.CharacterAdvancement;
using Odyssey.Application.Checks;
using Odyssey.Application.Commands;
using Odyssey.Application.Dice;
using Odyssey.Application.Persistence;
using Odyssey.Application.Random;
using Odyssey.Application.Results;
using Odyssey.Application.Time;
using Odyssey.Domain.Character;
using Odyssey.Domain.Checks;
using Odyssey.Domain.Content;
using Odyssey.Domain.Identity;
using Odyssey.Domain.Time;
using Odyssey.Persistence.Sqlite;
using Odyssey.Rules.Versions;

namespace Odyssey.Tests.Persistence
{
    /// <summary>
    /// ODY-S07-102: real, end-to-end tests for the check/contest command `ADR-031` specifies. Every test
    /// goes through the real `CheckService.PerformCheck` production entry point, the real, unmodified
    /// `DiceRollService.SubmitRoll`, and a real temp-directory SQLite campaign -- never a direct unit call
    /// to a private method, and never a fake/bypassed dice roll.
    /// </summary>
    public sealed class CheckIntegrationTests
    {
        private static readonly CorrelationId Corr = CorrelationId.Parse("corr_0123456789abcdef0123456789abcdef");
        private static readonly RngKeyEpochId Epoch = RngKeyEpochId.Parse("epoch-001");
        private static readonly RulesetVersion Ruleset = RulesetVersion.Parse("1.0.0");

        private string _root = null!;
        private CampaignHandle _campaign = null!;
        private IWallClock _clock = null!;
        private SqliteCharacterRepository _characters = null!;
        private SqliteCheckStateReader _reader = null!;
        private SqliteCheckRepository _apply = null!;
        private DiceRollStore _diceRollStore = null!;

        [SetUp]
        public void SetUp()
        {
            _root = Path.Combine(Path.GetTempPath(), "ody-s07-102-" + Guid.NewGuid().ToString("N"));
            _clock = new SystemWallClock();
            Result<CampaignHandle> campaign = new SqliteCampaignRepository(_clock).Create(new CreateCampaignRequest(_root, "check-scenario", "ruleset.core", "1.0.0", "0.1.0"), Command(), Corr);
            Assert.That(campaign.IsSuccess, Is.True);
            _campaign = campaign.Value;
            _characters = new SqliteCharacterRepository(_clock);
            _reader = new SqliteCheckStateReader(_characters);
            _apply = new SqliteCheckRepository(_clock);
            _diceRollStore = new DiceRollStore();
        }

        [Test] // TC-CHECK-001
        public void PerformCheck_SplitsFormula_RealSubmitRollCalled_AutomaticModifiersExact()
        {
            CharacterId actor = Active("actor");
            GrantAttribute(actor, "Strength", 3);

            CheckRequest request = Request(actor, "1d20+Strength+2", difficultyClass: 10, out CommandId commandId);
            Result<CheckOutcomeRecord> result = CheckService.PerformCheck(_reader, _apply, _characters, _diceRollStore, new FixedRandomStreamFactory(10), _clock, _campaign, Ruleset, Epoch, request);

            Assert.That(result.IsSuccess, Is.True, result.IsFailure ? result.Error.Code.ToString() : string.Empty);
            Assert.That(_diceRollStore.TryGet(result.Value.DiceRollId, out DiceRoll roll), Is.True);
            Assert.That(roll.FormulaOriginal, Is.EqualTo("1d20"), "The dice-only sub-formula, not the original check formula, must reach SubmitRollRequest.Formula.");
            Assert.That(roll.ModifierEntries.Count, Is.EqualTo(2));
            Assert.That(roll.ModifierEntries[0].SourceKind, Is.EqualTo("FormulaConstant"));
            Assert.That(roll.ModifierEntries[0].AppliedValue, Is.EqualTo(2));
            Assert.That(roll.ModifierEntries[1].SourceKind, Is.EqualTo("AttributeModifier"));
            Assert.That(roll.ModifierEntries[1].Label, Is.EqualTo("Strength"));
            Assert.That(roll.ModifierEntries[1].AppliedValue, Is.EqualTo(3));
            // raw fixed die value 10, +2 constant, +3 Strength = 15.
            Assert.That(roll.FinalTotal, Is.EqualTo(15));
            Assert.That(result.Value.Result, Is.EqualTo(CheckResultKind.Pass), "15 >= DC 10.");
        }

        [Test] // TC-CHECK-002
        public void PerformCheck_FinalTotalBelowDifficultyClass_Fails()
        {
            CharacterId actor = Active("actor");
            GrantAttribute(actor, "Strength", 1);

            CheckRequest request = Request(actor, "1d20+Strength", difficultyClass: 15, out CommandId commandId);
            Result<CheckOutcomeRecord> result = CheckService.PerformCheck(_reader, _apply, _characters, _diceRollStore, new FixedRandomStreamFactory(5), _clock, _campaign, Ruleset, Epoch, request);

            Assert.That(result.IsSuccess, Is.True, result.IsFailure ? result.Error.Code.ToString() : string.Empty);
            // raw 5 + 1 Strength = 6, DC 15 -> Fail.
            Assert.That(result.Value.Result, Is.EqualTo(CheckResultKind.Fail));
        }

        [Test] // TC-CHECK-003
        public void PerformCheck_UnresolvedReference_FailsClosed_NoRngConsumed()
        {
            CharacterId actor = Active("actor");

            CheckRequest request = Request(actor, "1d20+Nonexistent", difficultyClass: 10, out CommandId commandId);
            Result<CheckOutcomeRecord> result = CheckService.PerformCheck(_reader, _apply, _characters, _diceRollStore, new ThrowingRandomFactory(), _clock, _campaign, Ruleset, Epoch, request);

            Assert.That(result.IsFailure, Is.True);
            Assert.That(result.Error.Code, Is.EqualTo(ErrorCodes.CheckFormulaUnresolvedReference));
        }

        [Test] // TC-CHECK-004
        public void PerformCheck_AmbiguousReference_BothAttributeAndSkillMatchSameName_FailsClosed_NoRngConsumed()
        {
            CharacterId actor = Active("actor");
            GrantAttribute(actor, "Grit", 2);
            GrantSkill(actor, "Grit", 1);

            CheckRequest request = Request(actor, "1d20+Grit", difficultyClass: 10, out CommandId commandId);
            Result<CheckOutcomeRecord> result = CheckService.PerformCheck(_reader, _apply, _characters, _diceRollStore, new ThrowingRandomFactory(), _clock, _campaign, Ruleset, Epoch, request);

            Assert.That(result.IsFailure, Is.True);
            Assert.That(result.Error.Code, Is.EqualTo(ErrorCodes.CheckFormulaAmbiguousReference));
        }

        [Test] // TC-CHECK-005
        public void PerformCheck_MoreThanOneDiceGroup_RejectedWhole_NoRngConsumed()
        {
            CharacterId actor = Active("actor");
            GrantAttribute(actor, "Strength", 1);

            CheckRequest request = Request(actor, "1d20+1d4+Strength", difficultyClass: 10, out CommandId commandId);
            Result<CheckOutcomeRecord> result = CheckService.PerformCheck(_reader, _apply, _characters, _diceRollStore, new ThrowingRandomFactory(), _clock, _campaign, Ruleset, Epoch, request);

            Assert.That(result.IsFailure, Is.True);
            Assert.That(result.Error.Code, Is.EqualTo(ErrorCodes.CheckFormulaRequiresExactlyOneDiceGroup));
        }

        [Test] // TC-CHECK-006
        public void PerformCheck_MoreThanOneAttributeReference_RejectedWhole_NoRngConsumed()
        {
            CharacterId actor = Active("actor");
            GrantAttribute(actor, "Strength", 1);
            GrantAttribute(actor, "Dexterity", 1);

            CheckRequest request = Request(actor, "1d20+Strength+Dexterity", difficultyClass: 10, out CommandId commandId);
            Result<CheckOutcomeRecord> result = CheckService.PerformCheck(_reader, _apply, _characters, _diceRollStore, new ThrowingRandomFactory(), _clock, _campaign, Ruleset, Epoch, request);

            Assert.That(result.IsFailure, Is.True);
            Assert.That(result.Error.Code, Is.EqualTo(ErrorCodes.CheckFormulaRequiresAtMostOneAttributeReference));
        }

        [Test] // TC-CHECK-007
        public void PerformCheck_NaturalMaximumOnSkillCheck_RecordsRealCriticalSuccessEvidence()
        {
            CharacterId actor = Active("actor");
            GrantSkill(actor, "Athletics", 2);

            CheckRequest request = Request(actor, "1d20+Athletics", difficultyClass: 5, out CommandId commandId);
            Result<CheckOutcomeRecord> result = CheckService.PerformCheck(_reader, _apply, _characters, _diceRollStore, new FixedRandomStreamFactory(20), _clock, _campaign, Ruleset, Epoch, request);

            Assert.That(result.IsSuccess, Is.True, result.IsFailure ? result.Error.Code.ToString() : string.Empty);
            Assert.That(result.Value.IsNaturalMaximum, Is.True, "A raw die value of 20 on a d20 is its own maximum face value.");
            Result<System.Collections.Generic.IReadOnlyList<CriticalSuccessEvidenceRecord>> evidence = _characters.GetCriticalSuccessEvidence(_campaign, actor, Corr);
            Assert.That(evidence.IsSuccess, Is.True);
            Assert.That(evidence.Value.Count, Is.EqualTo(1));
            Assert.That(evidence.Value[0].SkillDefinitionId, Is.EqualTo(SkillDefinitionId.Parse("Athletics")));
            Assert.That(evidence.Value[0].SourceDiceRollId, Is.EqualTo(result.Value.DiceRollId), "RecordCriticalSuccessEvidence must be called with the REAL DiceRoll.RollId, never a synthetic value.");
        }

        [Test] // TC-CHECK-008
        public void PerformCheck_NaturalMaximumOnAttributeOnlyCheck_DoesNotRecordCriticalSuccessEvidence()
        {
            CharacterId actor = Active("actor");
            GrantAttribute(actor, "Strength", 1);

            CheckRequest request = Request(actor, "1d20+Strength", difficultyClass: 5, out CommandId commandId);
            Result<CheckOutcomeRecord> result = CheckService.PerformCheck(_reader, _apply, _characters, _diceRollStore, new FixedRandomStreamFactory(20), _clock, _campaign, Ruleset, Epoch, request);

            Assert.That(result.IsSuccess, Is.True, result.IsFailure ? result.Error.Code.ToString() : string.Empty);
            Assert.That(result.Value.IsNaturalMaximum, Is.True);
            Assert.That(result.Value.ResolvedSkill, Is.Null, "An attribute-only check never resolves a SkillDefinitionId.");
            Result<System.Collections.Generic.IReadOnlyList<CriticalSuccessEvidenceRecord>> evidence = _characters.GetCriticalSuccessEvidence(_campaign, actor, Corr);
            Assert.That(evidence.IsSuccess, Is.True);
            Assert.That(evidence.Value.Count, Is.EqualTo(0), "ADR-031 section 10 limits the RecordCriticalSuccessEvidence hook to skill checks only.");
        }

        [Test] // TC-CHECK-009
        public void PerformCheck_RetryWithSameCommandId_ReplaysWithoutSecondRngDerivation()
        {
            CharacterId actor = Active("actor");
            GrantAttribute(actor, "Strength", 2);

            CheckRequest request = Request(actor, "1d20+Strength", difficultyClass: 10, out CommandId commandId);
            var countingFactory = new CountingRandomFactory(new FixedRandomStreamFactory(10));

            Result<CheckOutcomeRecord> first = CheckService.PerformCheck(_reader, _apply, _characters, _diceRollStore, countingFactory, _clock, _campaign, Ruleset, Epoch, request);
            Assert.That(first.IsSuccess, Is.True, first.IsFailure ? first.Error.Code.ToString() : string.Empty);
            Assert.That(countingFactory.CreateCalls, Is.EqualTo(1));

            Result<CheckOutcomeRecord> second = CheckService.PerformCheck(_reader, _apply, _characters, _diceRollStore, countingFactory, _clock, _campaign, Ruleset, Epoch, request);
            Assert.That(second.IsSuccess, Is.True);
            Assert.That(countingFactory.CreateCalls, Is.EqualTo(1), "A retry of the same CommandId must never re-derive an authoritative random stream.");
            Assert.That(second.Value.DiceRollId, Is.EqualTo(first.Value.DiceRollId));
            Assert.That(second.Value.Result, Is.EqualTo(first.Value.Result));
        }

        [Test] // TC-CHECK-010
        public void PerformCheck_Succeeds_WithNoOpenCombatEncounterAnywhere()
        {
            // ADR-031 section 9 / DoD point 5: a check must work identically whether or not a
            // CombatEncounter is open. This test never creates a scene, token, or combat encounter of any
            // kind -- proving the command has no such dependency, not merely omitting one by coincidence.
            CharacterId actor = Active("actor");
            GrantAttribute(actor, "Strength", 4);

            CheckRequest request = Request(actor, "1d20+Strength", difficultyClass: 10, out CommandId commandId);
            Result<CheckOutcomeRecord> result = CheckService.PerformCheck(_reader, _apply, _characters, _diceRollStore, new FixedRandomStreamFactory(10), _clock, _campaign, Ruleset, Epoch, request);

            Assert.That(result.IsSuccess, Is.True, result.IsFailure ? result.Error.Code.ToString() : string.Empty);
        }

        /// <summary>
        /// ODY-S07-102 доработка (product-owner-ordered fix, after independent verification found
        /// `check.formula.invalid` in `docs/errors/ERROR_CODES.md` claimed test coverage it did not have):
        /// a genuine `AttackDamageFormulaParser.TryParse` syntax failure, distinct from the already-covered
        /// "more than one DiceGroup/AttributeReference term" semantic-validation failures (`TC-CHECK-005`/`006`,
        /// which the parser itself accepts syntactically) -- a trailing operator with nothing after it.
        /// </summary>
        [Test] // TC-CHECK-011
        public void PerformCheck_MalformedFormula_FailsWithCheckFormulaInvalid_NoRngConsumed()
        {
            CharacterId actor = Active("actor");

            // "1d20+" is syntactically invalid (a trailing operator with no following term) -- confirmed by
            // direct read of AttackDamageFormulaParser.TryParse, which returns false with InvalidSyntax for
            // this exact input, not merely assumed.
            CheckRequest request = Request(actor, "1d20+", difficultyClass: 10, out CommandId commandId);
            Result<CheckOutcomeRecord> result = CheckService.PerformCheck(_reader, _apply, _characters, _diceRollStore, new ThrowingRandomFactory(), _clock, _campaign, Ruleset, Epoch, request);

            Assert.That(result.IsFailure, Is.True);
            Assert.That(result.Error.Code, Is.EqualTo(ErrorCodes.CheckFormulaInvalid));
        }

        /// <summary>
        /// ODY-S07-102 доработка (product-owner-ordered fix, after independent verification found a real
        /// `DiceRoll.RollId` desynchronization risk): forces `RecordCriticalSuccessEvidence` to fail on its
        /// first call -- AFTER `RecordCheckOutcome` has already durably committed, per the new operation
        /// order -- then retries the same `CommandId`. Proves the exact invariant that was broken: the retry
        /// never re-derives RNG (the counting factory sees exactly one `Create()` call across both attempts),
        /// really does call `RecordCriticalSuccessEvidence` again (not silently skip it), and the resulting
        /// `CriticalSuccessEvidenceRecord.SourceDiceRollId` is IDENTICAL to the durable `CheckOutcomeRecord.DiceRollId`
        /// -- never a second, re-rolled `DiceRoll` from a hypothetical retry roll.
        /// </summary>
        [Test] // TC-CHECK-012
        public void PerformCheck_RetryAfterCriticalSuccessEvidenceFailure_ResumesWithSameDiceRollId_NoSecondRngDerivation()
        {
            CharacterId actor = Active("actor");
            GrantSkill(actor, "Athletics", 2);

            CheckRequest request = Request(actor, "1d20+Athletics", difficultyClass: 5, out CommandId commandId);
            var countingFactory = new CountingRandomFactory(new FixedRandomStreamFactory(20));
            var failingCharacters = new FailsFirstCriticalSuccessEvidenceCharacterRepository(_characters);

            Result<CheckOutcomeRecord> first = CheckService.PerformCheck(_reader, _apply, failingCharacters, _diceRollStore, countingFactory, _clock, _campaign, Ruleset, Epoch, request);
            Assert.That(first.IsFailure, Is.True, "The first attempt's own RecordCriticalSuccessEvidence call is deliberately made to fail -- RecordCheckOutcome must already be durable at this point, per the new operation order.");

            Result<CheckOutcomeRecord> second = CheckService.PerformCheck(_reader, _apply, failingCharacters, _diceRollStore, countingFactory, _clock, _campaign, Ruleset, Epoch, request);
            Assert.That(second.IsSuccess, Is.True, second.IsFailure ? second.Error.Code.ToString() : string.Empty);
            Assert.That(countingFactory.CreateCalls, Is.EqualTo(1), "The retry must never re-derive an authoritative random stream -- CheckOutcomeRecord was already durable after the first attempt, so GetCheckOutcome finds it before SubmitRoll is ever reached.");

            Result<System.Collections.Generic.IReadOnlyList<CriticalSuccessEvidenceRecord>> evidence = _characters.GetCriticalSuccessEvidence(_campaign, actor, Corr);
            Assert.That(evidence.IsSuccess, Is.True);
            Assert.That(evidence.Value.Count, Is.EqualTo(1), "Exactly one evidence row, written on the resumed retry -- never lost, never duplicated.");
            Assert.That(evidence.Value[0].SourceDiceRollId, Is.EqualTo(second.Value.DiceRollId), "RecordCriticalSuccessEvidence must always reference the SAME DiceRollId as the durable CheckOutcomeRecord -- the exact invariant the doработка closes.");
        }

        // ---- helpers ----

        private CheckRequest Request(CharacterId actor, string formula, long difficultyClass, out CommandId commandId)
        {
            commandId = Command();
            var intent = new CheckIntent(actor, formula, difficultyClass, DiceRollAudience.Public());
            return new CheckRequest(intent, User(), commandId, Corr);
        }

        private CharacterRecord GrantAttribute(CharacterId characterId, string attributeName, long value)
        {
            CharacterRecord current = _characters.GetCharacter(_campaign, characterId, Corr).Value;
            Result<CharacterRecord> granted = _characters.GrantDevelopmentPoints(_campaign, characterId, 100, "test", User(), actorIsMainGm: true, current.Revisions.MechanicsRevision, Command(), Corr);
            Assert.That(granted.IsSuccess, Is.True);
            Result<CharacterRecord> purchased = CharacterAdvancementService.PurchaseAttributeIncrease(_characters, _campaign, characterId, AttributeDefinitionId.Parse(attributeName), value, User(), actorIsMainGm: true, granted.Value.Revisions.MechanicsRevision, expectedAttributeRevision: 0, Command(), Corr);
            Assert.That(purchased.IsSuccess, Is.True, purchased.IsFailure ? purchased.Error.Code.ToString() : string.Empty);
            return purchased.Value;
        }

        private CharacterRecord GrantSkill(CharacterId characterId, string skillName, long level)
        {
            CharacterRecord current = _characters.GetCharacter(_campaign, characterId, Corr).Value;
            Result<CharacterRecord> granted = _characters.GrantDevelopmentPoints(_campaign, characterId, 100, "test", User(), actorIsMainGm: true, current.Revisions.MechanicsRevision, Command(), Corr);
            Assert.That(granted.IsSuccess, Is.True);
            Result<CharacterRecord> purchased = CharacterAdvancementService.PurchaseSkillLevel(_characters, _campaign, characterId, SkillDefinitionId.Parse(skillName), level, User(), actorIsMainGm: true, granted.Value.Revisions.MechanicsRevision, expectedSkillRevision: 0, Command(), Corr);
            Assert.That(purchased.IsSuccess, Is.True, purchased.IsFailure ? purchased.Error.Code.ToString() : string.Empty);
            return purchased.Value;
        }

        private CharacterId Active(string name)
        {
            CharacterId id = _characters.CreateCharacter(new CreateCharacterRequest(_campaign, CharacterKind.PlayerCharacter, name), Command(), Corr).Value.CharacterId;
            CharacterRecord current = _characters.GetCharacter(_campaign, id, Corr).Value;
            return _characters.ApproveCharacterDraft(_campaign, id, true, current.Revisions.LifecycleRevision, Command(), Corr).Value.CharacterId;
        }

        private static CommandId Command() => CommandId.Parse("cmd_" + Guid.NewGuid().ToString("N"));
        private static UserId User() => UserId.Parse("user_" + Guid.NewGuid().ToString("N"));

        private sealed class ThrowingRandomFactory : IAuthoritativeRandomStreamFactory
        {
            public Result<IAuthoritativeRandomStream> Create(RandomDecisionContext context) => throw new AssertionException("This scenario must not derive an RNG stream at all.");
        }

        private sealed class FixedRandomStream : IAuthoritativeRandomStream
        {
            private readonly int[] _values;
            public FixedRandomStream(int[] values) { _values = values; }
            public RandomStreamIdentity Identity => default;
            public Result<RandomSample> NextInclusive(int minInclusive, int maxInclusive, int drawIndex) => Result<RandomSample>.Success(new RandomSample(_values[drawIndex], default!));
        }

        private sealed class FixedRandomStreamFactory : IAuthoritativeRandomStreamFactory
        {
            private readonly int[] _values;
            public FixedRandomStreamFactory(params int[] values) { _values = values; }
            public Result<IAuthoritativeRandomStream> Create(RandomDecisionContext context) => Result<IAuthoritativeRandomStream>.Success(new FixedRandomStream(_values));
        }

        private sealed class CountingRandomFactory : IAuthoritativeRandomStreamFactory
        {
            private readonly IAuthoritativeRandomStreamFactory _inner;
            public int CreateCalls;
            public CountingRandomFactory(IAuthoritativeRandomStreamFactory inner) { _inner = inner; }
            public Result<IAuthoritativeRandomStream> Create(RandomDecisionContext context) { CreateCalls++; return _inner.Create(context); }
        }

        /// <summary>
        /// ODY-S07-102 доработка (TC-CHECK-012): a real, unmodified `ICharacterRepository` (`_inner`), forwarding
        /// every call verbatim except that the FIRST call to `RecordCriticalSuccessEvidence` returns a real,
        /// distinct `Result.Failure` instead of touching the database at all -- models a genuine transient
        /// write failure, mirroring `ActivateAbilityIntegrationTests.cs`'s own established
        /// `FailsNthRemovalActiveEffectRepository` precedent (a small, real interface implementation
        /// controlling one specific, deterministic behavior, not a mocking framework, not bypassing any real
        /// logic for calls it does not intercept). Every other member is a plain one-line forward to `_inner`.
        /// </summary>
        private sealed class FailsFirstCriticalSuccessEvidenceCharacterRepository : ICharacterRepository
        {
            private readonly ICharacterRepository _inner;
            private int _recordCriticalSuccessEvidenceCallCount;

            public FailsFirstCriticalSuccessEvidenceCharacterRepository(ICharacterRepository inner)
            {
                _inner = inner;
            }

            public Result<CriticalSuccessEvidenceRecord> RecordCriticalSuccessEvidence(CampaignHandle campaign, CharacterId characterId, SkillDefinitionId skillDefinitionId, string? sourceDiceRollId, string? sourceActionId, CommandId commandId, CorrelationId correlationId)
            {
                _recordCriticalSuccessEvidenceCallCount++;
                if (_recordCriticalSuccessEvidenceCallCount == 1)
                {
                    return Result<CriticalSuccessEvidenceRecord>.Failure(Error.Create(
                        ErrorCodes.PersistenceCharacterIoFailed, ErrorCategory.PermanentInfrastructure, SafeReasonCode.UnexpectedError,
                        UserMessageKey.Parse("errors.persistence.character_io_failed"), RetryDirective.ManualRecoveryRequired, correlationId));
                }

                return _inner.RecordCriticalSuccessEvidence(campaign, characterId, skillDefinitionId, sourceDiceRollId, sourceActionId, commandId, correlationId);
            }

            public Result<CharacterRecord> CreateCharacter(CreateCharacterRequest request, CommandId commandId, CorrelationId correlationId) => _inner.CreateCharacter(request, commandId, correlationId);
            public Result<CharacterRecord> GetCharacter(CampaignHandle campaign, CharacterId characterId, CorrelationId correlationId) => _inner.GetCharacter(campaign, characterId, correlationId);
            public Result<CharacterRecord> SetCharacterPortrait(CampaignHandle campaign, CharacterId characterId, AssetId? portraitAssetId, long expectedPresentationRevision, CommandId commandId, CorrelationId correlationId) => _inner.SetCharacterPortrait(campaign, characterId, portraitAssetId, expectedPresentationRevision, commandId, correlationId);
            public Result<CharacterRecord> UpdateIdentity(CampaignHandle campaign, CharacterId characterId, string newDisplayName, long expectedIdentityRevision, CommandId commandId, CorrelationId correlationId) => _inner.UpdateIdentity(campaign, characterId, newDisplayName, expectedIdentityRevision, commandId, correlationId);
            public Result<CharacterRecord> UpdatePresentation(CampaignHandle campaign, CharacterId characterId, string? portraitReference, long expectedPresentationRevision, CommandId commandId, CorrelationId correlationId) => _inner.UpdatePresentation(campaign, characterId, portraitReference, expectedPresentationRevision, commandId, correlationId);
            public Result<IReadOnlyList<CharacterHistoryEntry>> GetCharacterHistory(CampaignHandle campaign, CharacterId characterId, CorrelationId correlationId) => _inner.GetCharacterHistory(campaign, characterId, correlationId);
            public Result<CharacterRecord> AssignPrimaryOwner(CampaignHandle campaign, CharacterId characterId, UserId newPrimaryOwnerUserId, string reasonCode, bool actorIsMainGm, long expectedOwnershipRevision, CommandId commandId, CorrelationId correlationId) => _inner.AssignPrimaryOwner(campaign, characterId, newPrimaryOwnerUserId, reasonCode, actorIsMainGm, expectedOwnershipRevision, commandId, correlationId);
            public Result<CharacterRecord> AddCharacterCoOwner(CampaignHandle campaign, CharacterId characterId, UserId coOwnerUserId, bool actorIsMainGm, long expectedOwnershipRevision, CommandId commandId, CorrelationId correlationId) => _inner.AddCharacterCoOwner(campaign, characterId, coOwnerUserId, actorIsMainGm, expectedOwnershipRevision, commandId, correlationId);
            public Result<CharacterRecord> RemoveCharacterCoOwner(CampaignHandle campaign, CharacterId characterId, UserId coOwnerUserId, bool actorIsMainGm, long expectedOwnershipRevision, CommandId commandId, CorrelationId correlationId) => _inner.RemoveCharacterCoOwner(campaign, characterId, coOwnerUserId, actorIsMainGm, expectedOwnershipRevision, commandId, correlationId);
            public Result<CharacterRecord> GrantPermanentCharacterControl(CampaignHandle campaign, CharacterId characterId, UserId controlUserId, bool actorIsMainGm, long expectedOwnershipRevision, CommandId commandId, CorrelationId correlationId) => _inner.GrantPermanentCharacterControl(campaign, characterId, controlUserId, actorIsMainGm, expectedOwnershipRevision, commandId, correlationId);
            public Result<CharacterRecord> GrantTemporaryCharacterControl(CampaignHandle campaign, CharacterId characterId, UserId controlUserId, UtcInstant? expiresAt, bool actorIsMainGm, long expectedOwnershipRevision, CommandId commandId, CorrelationId correlationId) => _inner.GrantTemporaryCharacterControl(campaign, characterId, controlUserId, expiresAt, actorIsMainGm, expectedOwnershipRevision, commandId, correlationId);
            public Result<CharacterRecord> RevokeCharacterControl(CampaignHandle campaign, CharacterId characterId, UserId controlUserId, bool actorIsMainGm, long expectedOwnershipRevision, CommandId commandId, CorrelationId correlationId) => _inner.RevokeCharacterControl(campaign, characterId, controlUserId, actorIsMainGm, expectedOwnershipRevision, commandId, correlationId);
            public Result<CharacterRecord> BindDraftToCampaign(BindDraftToCampaignRequest request, CommandId commandId, CorrelationId correlationId) => _inner.BindDraftToCampaign(request, commandId, correlationId);
            public Result<CharacterRecord> SubmitCharacterDraft(CampaignHandle campaign, CharacterId characterId, long expectedLifecycleRevision, CommandId commandId, CorrelationId correlationId) => _inner.SubmitCharacterDraft(campaign, characterId, expectedLifecycleRevision, commandId, correlationId);
            public Result<CharacterReviewCommentRecord> AddCharacterReviewComment(CampaignHandle campaign, CharacterId characterId, UserId authorUserId, string text, CommandId commandId, CorrelationId correlationId) => _inner.AddCharacterReviewComment(campaign, characterId, authorUserId, text, commandId, correlationId);
            public Result<CharacterRecord> ApproveCharacterDraft(CampaignHandle campaign, CharacterId characterId, bool actorIsMainGm, long expectedLifecycleRevision, CommandId commandId, CorrelationId correlationId) => _inner.ApproveCharacterDraft(campaign, characterId, actorIsMainGm, expectedLifecycleRevision, commandId, correlationId);
            public Result<IReadOnlyList<CharacterReviewCommentRecord>> GetCharacterReviewComments(CampaignHandle campaign, CharacterId characterId, CorrelationId correlationId) => _inner.GetCharacterReviewComments(campaign, characterId, correlationId);
            public Result<CharacterRecord> GrantDevelopmentPoints(CampaignHandle campaign, CharacterId characterId, long amount, string reason, UserId actorUserId, bool actorIsMainGm, long expectedMechanicsRevision, CommandId commandId, CorrelationId correlationId) => _inner.GrantDevelopmentPoints(campaign, characterId, amount, reason, actorUserId, actorIsMainGm, expectedMechanicsRevision, commandId, correlationId);
            public Result<CharacterRecord> PurchaseAttributeIncrease(CampaignHandle campaign, CharacterId characterId, AttributeDefinitionId attributeDefinitionId, long toValue, long decidedFromValue, bool exceedsNormalCap, long decidedCost, UserId actorUserId, bool actorIsMainGm, long expectedMechanicsRevision, long expectedAttributeRevision, CommandId commandId, CorrelationId correlationId) => _inner.PurchaseAttributeIncrease(campaign, characterId, attributeDefinitionId, toValue, decidedFromValue, exceedsNormalCap, decidedCost, actorUserId, actorIsMainGm, expectedMechanicsRevision, expectedAttributeRevision, commandId, correlationId);
            public Result<IReadOnlyList<DevelopmentTransactionRecord>> GetDevelopmentLedger(CampaignHandle campaign, CharacterId characterId, CorrelationId correlationId) => _inner.GetDevelopmentLedger(campaign, characterId, correlationId);
            public Result<CharacterRecord> PurchaseSkillLevel(CampaignHandle campaign, CharacterId characterId, SkillDefinitionId skillDefinitionId, long toLevel, long decidedFromLevel, bool requiresRecommendation, long decidedCost, UserId actorUserId, bool actorIsMainGm, long expectedMechanicsRevision, long expectedSkillRevision, CommandId commandId, CorrelationId correlationId) => _inner.PurchaseSkillLevel(campaign, characterId, skillDefinitionId, toLevel, decidedFromLevel, requiresRecommendation, decidedCost, actorUserId, actorIsMainGm, expectedMechanicsRevision, expectedSkillRevision, commandId, correlationId);
            public Result<IReadOnlyList<CriticalSuccessEvidenceRecord>> GetCriticalSuccessEvidence(CampaignHandle campaign, CharacterId characterId, CorrelationId correlationId) => _inner.GetCriticalSuccessEvidence(campaign, characterId, correlationId);
            public Result<AdvancementRecommendationRecord> RequestSkillAdvancedRecommendation(CampaignHandle campaign, CharacterId characterId, SkillDefinitionId skillDefinitionId, long targetLevel, long decidedFromLevel, long decidedReservedAmount, IReadOnlyList<CriticalSuccessEvidenceId> evidenceIds, UserId actorUserId, bool actorIsMainGm, long expectedMechanicsRevision, CommandId commandId, CorrelationId correlationId) => _inner.RequestSkillAdvancedRecommendation(campaign, characterId, skillDefinitionId, targetLevel, decidedFromLevel, decidedReservedAmount, evidenceIds, actorUserId, actorIsMainGm, expectedMechanicsRevision, commandId, correlationId);
            public Result<CharacterRecord> ResolveAdvancementRecommendation(CampaignHandle campaign, CharacterId characterId, AdvancementRecommendationId recommendationId, bool approve, bool spendReservedPoints, UserId actorUserId, bool actorIsMainGm, long expectedMechanicsRevision, long expectedRecommendationRevision, CommandId commandId, CorrelationId correlationId) => _inner.ResolveAdvancementRecommendation(campaign, characterId, recommendationId, approve, spendReservedPoints, actorUserId, actorIsMainGm, expectedMechanicsRevision, expectedRecommendationRevision, commandId, correlationId);
            public Result<AdvancementRecommendationRecord> GetAdvancementRecommendation(CampaignHandle campaign, CharacterId characterId, AdvancementRecommendationId recommendationId, CorrelationId correlationId) => _inner.GetAdvancementRecommendation(campaign, characterId, recommendationId, correlationId);
            public Result<IReadOnlyList<AdvancementPurchase>> GetAdvancementPurchases(CampaignHandle campaign, CharacterId characterId, CorrelationId correlationId) => _inner.GetAdvancementPurchases(campaign, characterId, correlationId);
            public Result<CharacterRecord> RevertAdvancementPurchase(CampaignHandle campaign, CharacterId characterId, AdvancementPurchaseId purchaseId, string reasonCode, UserId actorUserId, bool actorIsMainGm, long expectedMechanicsRevision, CommandId commandId, CorrelationId correlationId) => _inner.RevertAdvancementPurchase(campaign, characterId, purchaseId, reasonCode, actorUserId, actorIsMainGm, expectedMechanicsRevision, commandId, correlationId);
            public Result<CharacterRespecPreview> PreviewCharacterRespec(CampaignHandle campaign, CharacterId characterId, IReadOnlyList<CharacterRespecTarget> targets, CorrelationId correlationId) => _inner.PreviewCharacterRespec(campaign, characterId, targets, correlationId);
            public Result<CharacterRecord> ApplyCharacterRespec(CampaignHandle campaign, CharacterId characterId, IReadOnlyList<CharacterRespecTarget> targets, string reasonCode, UserId actorUserId, bool actorIsMainGm, long expectedMechanicsRevision, CommandId commandId, CorrelationId correlationId) => _inner.ApplyCharacterRespec(campaign, characterId, targets, reasonCode, actorUserId, actorIsMainGm, expectedMechanicsRevision, commandId, correlationId);
            public Result<CharacterRecord> AcquireAbility(CampaignHandle campaign, CharacterId characterId, AbilityDefinitionId abilityDefinitionId, SourceKind sourceKind, string? sourceRef, RankMode rankMode, long? numericRank, string? namedRankKey, string configuration, long progressionPurchaseCost, UserId actorUserId, bool actorIsMainGm, long? expectedMechanicsRevision, long expectedCharacterAbilitiesRevision, CommandId commandId, CorrelationId correlationId) => _inner.AcquireAbility(campaign, characterId, abilityDefinitionId, sourceKind, sourceRef, rankMode, numericRank, namedRankKey, configuration, progressionPurchaseCost, actorUserId, actorIsMainGm, expectedMechanicsRevision, expectedCharacterAbilitiesRevision, commandId, correlationId);
            public Result<CharacterRecord> RemoveAbility(CampaignHandle campaign, CharacterId characterId, CharacterAbilityId characterAbilityId, UserId actorUserId, bool actorIsMainGm, long expectedCharacterAbilitiesRevision, CommandId commandId, CorrelationId correlationId) => _inner.RemoveAbility(campaign, characterId, characterAbilityId, actorUserId, actorIsMainGm, expectedCharacterAbilitiesRevision, commandId, correlationId);
            public Result<CharacterRecord> LinkAbilityActivationSource(CampaignHandle campaign, CharacterId characterId, CharacterAbilityId characterAbilityId, ContentDefinitionRef activationDefinitionRef, UserId actorUserId, bool actorIsMainGm, long expectedCharacterAbilitiesRevision, CommandId commandId, CorrelationId correlationId) => _inner.LinkAbilityActivationSource(campaign, characterId, characterAbilityId, activationDefinitionRef, actorUserId, actorIsMainGm, expectedCharacterAbilitiesRevision, commandId, correlationId);
            public Result<CharacterRecord> InitializeCharacterResource(CampaignHandle campaign, CharacterId characterId, ResourceDefinitionId resourceDefinitionId, long baseMaximum, long minimumValue, RecoveryRule recoveryRule, UserId actorUserId, bool actorIsMainGm, long expectedCharacterResourcesRevision, CommandId commandId, CorrelationId correlationId) => _inner.InitializeCharacterResource(campaign, characterId, resourceDefinitionId, baseMaximum, minimumValue, recoveryRule, actorUserId, actorIsMainGm, expectedCharacterResourcesRevision, commandId, correlationId);
            public Result<CharacterRecord> SetResourceCurrentValue(CampaignHandle campaign, CharacterId characterId, CharacterResourceId characterResourceId, long newCurrentValue, UserId actorUserId, bool actorIsMainGm, long expectedCharacterResourcesRevision, CommandId commandId, CorrelationId correlationId) => _inner.SetResourceCurrentValue(campaign, characterId, characterResourceId, newCurrentValue, actorUserId, actorIsMainGm, expectedCharacterResourcesRevision, commandId, correlationId);
            public Result<CharacterRecord> SetResourceMaximum(CampaignHandle campaign, CharacterId characterId, CharacterResourceId characterResourceId, long newBaseMaximum, long newPermanentMaximumAdjustment, UserId actorUserId, bool actorIsMainGm, long expectedCharacterResourcesRevision, CommandId commandId, CorrelationId correlationId) => _inner.SetResourceMaximum(campaign, characterId, characterResourceId, newBaseMaximum, newPermanentMaximumAdjustment, actorUserId, actorIsMainGm, expectedCharacterResourcesRevision, commandId, correlationId);
            public Result<CharacterRecord> InitializeCharacterAnatomy(CampaignHandle campaign, CharacterId characterId, AnatomyProfileDefinitionId anatomyProfileDefinitionId, string anatomyProfileVersion, IReadOnlyList<BodyPart> bodyParts, UserId actorUserId, bool actorIsMainGm, long expectedCharacterAnatomyRevision, CommandId commandId, CorrelationId correlationId) => _inner.InitializeCharacterAnatomy(campaign, characterId, anatomyProfileDefinitionId, anatomyProfileVersion, bodyParts, actorUserId, actorIsMainGm, expectedCharacterAnatomyRevision, commandId, correlationId);
            public Result<CharacterRecord> AddBodyPart(CampaignHandle campaign, CharacterId characterId, BodyPartId bodyPartId, string name, long damageLimit, BodyPartId? attachedToBodyPartId, string properties, UserId actorUserId, bool actorIsMainGm, long expectedCharacterAnatomyRevision, CommandId commandId, CorrelationId correlationId) => _inner.AddBodyPart(campaign, characterId, bodyPartId, name, damageLimit, attachedToBodyPartId, properties, actorUserId, actorIsMainGm, expectedCharacterAnatomyRevision, commandId, correlationId);
            public Result<CharacterRecord> RemoveBodyPart(CampaignHandle campaign, CharacterId characterId, BodyPartId bodyPartId, UserId actorUserId, bool actorIsMainGm, long expectedCharacterAnatomyRevision, CommandId commandId, CorrelationId correlationId) => _inner.RemoveBodyPart(campaign, characterId, bodyPartId, actorUserId, actorIsMainGm, expectedCharacterAnatomyRevision, commandId, correlationId);
            public Result<CharacterRecord> UpdateBodyPart(CampaignHandle campaign, CharacterId characterId, BodyPartId bodyPartId, long? newDamageLimit, string? newProperties, UserId actorUserId, bool actorIsMainGm, long expectedCharacterAnatomyRevision, CommandId commandId, CorrelationId correlationId) => _inner.UpdateBodyPart(campaign, characterId, bodyPartId, newDamageLimit, newProperties, actorUserId, actorIsMainGm, expectedCharacterAnatomyRevision, commandId, correlationId);
            public Result<CharacterRecord> ReplaceAnatomyProfile(CampaignHandle campaign, CharacterId characterId, AnatomyProfileDefinitionId newAnatomyProfileDefinitionId, string newAnatomyProfileVersion, IReadOnlyList<BodyPart> newBodyParts, UserId actorUserId, bool actorIsMainGm, long expectedCharacterAnatomyRevision, CommandId commandId, CorrelationId correlationId) => _inner.ReplaceAnatomyProfile(campaign, characterId, newAnatomyProfileDefinitionId, newAnatomyProfileVersion, newBodyParts, actorUserId, actorIsMainGm, expectedCharacterAnatomyRevision, commandId, correlationId);
            public Result<CharacterRecord> ApplyPermanentModification(CampaignHandle campaign, CharacterId characterId, BodyPartId attachedToBodyPartId, string kind, string description, UserId actorUserId, bool actorIsMainGm, long expectedCharacterAnatomyRevision, CommandId commandId, CorrelationId correlationId) => _inner.ApplyPermanentModification(campaign, characterId, attachedToBodyPartId, kind, description, actorUserId, actorIsMainGm, expectedCharacterAnatomyRevision, commandId, correlationId);
            public Result<CharacterRecord> ArchiveCharacter(CampaignHandle campaign, CharacterId characterId, UserId actorUserId, bool actorIsMainGm, long expectedLifecycleRevision, CommandId commandId, CorrelationId correlationId) => _inner.ArchiveCharacter(campaign, characterId, actorUserId, actorIsMainGm, expectedLifecycleRevision, commandId, correlationId);
            public Result DeleteCharacterPermanently(CampaignHandle campaign, CharacterId characterId, string reasonCode, UserId actorUserId, bool actorIsMainGm, long expectedLifecycleRevision, CommandId commandId, CorrelationId correlationId) => _inner.DeleteCharacterPermanently(campaign, characterId, reasonCode, actorUserId, actorIsMainGm, expectedLifecycleRevision, commandId, correlationId);
            public Result<CharacterRecord> TransitionCharacterToDead(CampaignHandle campaign, CharacterId characterId, LifecycleDeathIssuerKind issuerKind, UserId actorUserId, bool actorIsMainGm, long expectedLifecycleRevision, CommandId commandId, CorrelationId correlationId) => _inner.TransitionCharacterToDead(campaign, characterId, issuerKind, actorUserId, actorIsMainGm, expectedLifecycleRevision, commandId, correlationId);
            public Result<CharacterRecord> RestoreDeadCharacter(RestoreDeadCharacterRequest request, CommandId commandId, CorrelationId correlationId) => _inner.RestoreDeadCharacter(request, commandId, correlationId);
            public Result<CharacterExportBundle> ExportCharacter(CampaignHandle campaign, CharacterId characterId, string bundleDirectoryPath, ExportActorContext actorContext, CorrelationId correlationId) => _inner.ExportCharacter(campaign, characterId, bundleDirectoryPath, actorContext, correlationId);
            public Result<CharacterRecord> ImportCharacter(ImportCharacterRequest request, CommandId bindCommandId, CommandId applyStateCommandId, CorrelationId correlationId) => _inner.ImportCharacter(request, bindCommandId, applyStateCommandId, correlationId);
            public Result<Odyssey.Rules.Character.CharacterRulesetMigrationPlan> PreviewCharacterRulesetMigration(CampaignHandle campaign, CharacterId characterId, string targetRulesetId, string targetRulesetVersion, Odyssey.Rules.Character.RulesetDefinitionCatalog targetCatalog, CorrelationId correlationId) => _inner.PreviewCharacterRulesetMigration(campaign, characterId, targetRulesetId, targetRulesetVersion, targetCatalog, correlationId);
            public Result<CharacterRecord> ApplyCharacterRulesetMigration(CampaignHandle campaign, CharacterId characterId, Odyssey.Rules.Character.CharacterRulesetMigrationPlan plan, Odyssey.Rules.Character.RulesetDefinitionCatalog targetCatalog, UserId actorUserId, bool actorIsMainGm, CommandId commandId, CorrelationId correlationId) => _inner.ApplyCharacterRulesetMigration(campaign, characterId, plan, targetCatalog, actorUserId, actorIsMainGm, commandId, correlationId);
            public Result<CharacterRecord> RevertCharacterRulesetMigration(CampaignHandle campaign, CharacterId characterId, CommandId migrationCommandId, string reasonCode, UserId actorUserId, bool actorIsMainGm, long expectedCharacterRevision, CommandId commandId, CorrelationId correlationId) => _inner.RevertCharacterRulesetMigration(campaign, characterId, migrationCommandId, reasonCode, actorUserId, actorIsMainGm, expectedCharacterRevision, commandId, correlationId);
        }
    }
}
