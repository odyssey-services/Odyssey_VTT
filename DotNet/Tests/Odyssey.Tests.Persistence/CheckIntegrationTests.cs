using System;
using System.IO;
using NUnit.Framework;
using Odyssey.Application.Checks;
using Odyssey.Application.Commands;
using Odyssey.Application.Dice;
using Odyssey.Application.Persistence;
using Odyssey.Application.Random;
using Odyssey.Application.Results;
using Odyssey.Application.Time;
using Odyssey.Domain.Character;
using Odyssey.Domain.Checks;
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
            Result<CharacterRecord> purchased = _characters.PurchaseAttributeIncrease(_campaign, characterId, AttributeDefinitionId.Parse(attributeName), value, User(), actorIsMainGm: true, granted.Value.Revisions.MechanicsRevision, expectedAttributeRevision: 0, Command(), Corr);
            Assert.That(purchased.IsSuccess, Is.True, purchased.IsFailure ? purchased.Error.Code.ToString() : string.Empty);
            return purchased.Value;
        }

        private CharacterRecord GrantSkill(CharacterId characterId, string skillName, long level)
        {
            CharacterRecord current = _characters.GetCharacter(_campaign, characterId, Corr).Value;
            Result<CharacterRecord> granted = _characters.GrantDevelopmentPoints(_campaign, characterId, 100, "test", User(), actorIsMainGm: true, current.Revisions.MechanicsRevision, Command(), Corr);
            Assert.That(granted.IsSuccess, Is.True);
            Result<CharacterRecord> purchased = _characters.PurchaseSkillLevel(_campaign, characterId, SkillDefinitionId.Parse(skillName), level, User(), actorIsMainGm: true, granted.Value.Revisions.MechanicsRevision, expectedSkillRevision: 0, Command(), Corr);
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
    }
}
