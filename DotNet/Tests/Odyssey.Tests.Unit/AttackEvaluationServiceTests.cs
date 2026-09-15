using System;
using System.Collections.Generic;
using NUnit.Framework;
using Odyssey.Application.Commands;
using Odyssey.Application.Combat;
using Odyssey.Application.Persistence;
using Odyssey.Application.Random;
using Odyssey.Application.Results;
using Odyssey.Domain.Combat;
using Odyssey.Domain.Character;
using Odyssey.Domain.Content;
using Odyssey.Domain.Identity;
using Odyssey.Domain.Inventory;
using Odyssey.Domain.Time;
using Odyssey.Rules.Combat;

namespace Odyssey.Tests.Unit
{
    public sealed class AttackEvaluationServiceTests
    {
        [Test]
        public void Intent_RejectsInvalidOrDuplicateTargets_AndCopiesTargetList()
        {
            CharacterId actor = CharacterId.Parse("char_0123456789abcdef0123456789abcdef");
            Action invalidEncounter = () => new AttackIntent(default, actor, new[] { actor }, Item(), 1);
            Action duplicateTargets = () => new AttackIntent(Encounter(), actor, new[] { actor, actor }, Item(), 1);
            Assert.Throws<ArgumentException>(invalidEncounter);
            Assert.Throws<ArgumentException>(duplicateTargets);
            CharacterId[] targets = { Target() };
            AttackIntent intent = new AttackIntent(Encounter(), actor, targets, Item(), 1);
            targets[0] = actor;
            Assert.That(intent.TargetIds[0], Is.EqualTo(Target()));
        }

        [Test]
        public void Preview_UsesReadOnlyState_AndNeverCreatesRandomStream()
        {
            var reader = new Reader(State());
            var rules = new Rules();
            var random = new ThrowingRandomFactory();
            Result<ProposedAttackResolution> result = AttackEvaluationService.PreviewAttack(reader, rules, Campaign(), Request());
            Assert.That(result.IsSuccess, Is.True);
            Assert.That(rules.PreviewCalls, Is.EqualTo(1));
            Assert.That(rules.EvaluateCalls, Is.Zero);
            Assert.That(reader.ReadCalls, Is.EqualTo(1));
            Assert.That(random.CreateCalls, Is.Zero);
        }

        [Test]
        public void Evaluate_UsesAuthoritativeRngWithCommandIdentity_AndReturnsProposal()
        {
            var reader = new Reader(State());
            var rules = new Rules();
            var random = new RecordingRandomFactory();
            AttackRequest request = Request();
            Result<ProposedAttackResolution> result = AttackEvaluationService.EvaluateAttack(reader, rules, random, Campaign(), RngKeyEpochId.Parse("epoch-001"), request);
            Assert.That(result.IsSuccess, Is.True);
            Assert.That(rules.EvaluateCalls, Is.EqualTo(1));
            Assert.That(result.Value.RandomSample!.Value.Value, Is.InRange(1, 100));
            Assert.That(random.Context.RootCommandId, Is.EqualTo(request.CommandId));
            Assert.That(random.Context.CorrelationId, Is.EqualTo(request.CorrelationId));
            Assert.That(random.Context.DecisionOrdinal, Is.EqualTo(0));
            Assert.That(random.Context.Purpose.ToString(), Is.EqualTo("combat.attack.roll"));
        }

        [Test]
        public void Evaluation_RejectsUnauthorizedOrInactiveActor_BeforeRulesAndRng()
        {
            var unauthorized = new Reader(State()) { CanControl = false };
            Result<ProposedAttackResolution> denied = AttackEvaluationService.PreviewAttack(unauthorized, new Rules(), Campaign(), Request(mainGm: false));
            Assert.That(denied.IsFailure, Is.True);
            Assert.That(denied.Error.SafeReasonCode, Is.EqualTo(SafeReasonCode.PermissionDenied));
            var inactive = new Reader(State(current: Target()));
            Result<ProposedAttackResolution> notTurn = AttackEvaluationService.PreviewAttack(inactive, new Rules(), Campaign(), Request());
            Assert.That(notTurn.IsFailure, Is.True);
            Assert.That(notTurn.Error.SafeReasonCode, Is.EqualTo(SafeReasonCode.ActionNotAllowed));
        }

        [Test]
        public void Proposal_IsImmutableHostOnlyData_AndDoesNotExposeRngProof()
        {
            Result<ProposedAttackResolution> result = AttackEvaluationService.EvaluateAttack(new Reader(State()), new Rules(), new RecordingRandomFactory(), Campaign(), RngKeyEpochId.Parse("epoch-001"), Request());
            Assert.That(result.IsSuccess, Is.True);
            Assert.That(typeof(ProposedAttackResolution).GetProperty("RandomSample")!.PropertyType, Is.EqualTo(typeof(AttackRandomSample?)));
            Assert.That(typeof(AttackRandomSample).GetProperty("ProofData"), Is.Null);
            Assert.That(result.Value.EffectCandidates[0].Decision, Is.EqualTo(EffectApplicationDecision.Apply));
        }

        [Test]
        public void Evaluation_RejectsStaleRevisionOrTargetOutsideEncounter_BeforeRules()
        {
            var stale = new Reader(State(revision: 2));
            Assert.That(AttackEvaluationService.PreviewAttack(stale, new Rules(), Campaign(), Request()).IsFailure, Is.True);
            AttackRequest wrongTarget = new AttackRequest(new AttackIntent(Encounter(), Actor(), new[] { CharacterId.Parse("char_11111111111111111111111111111111") }, Item(), 1), UserId.Parse("user_0123456789abcdef0123456789abcdef"), true, CommandId.Parse("cmd_0123456789abcdef0123456789abcdef"), CorrelationId.Parse("corr_0123456789abcdef0123456789abcdef"));
            Assert.That(AttackEvaluationService.PreviewAttack(new Reader(State()), new Rules(), Campaign(), wrongTarget).IsFailure, Is.True);
        }

        [Test] // TC-ATTACK-022
        public void Intent_PublicSurfaceCarriesNoClientSidePreviewOrExecutionResult()
        {
            string[] allowed = { "EncounterId", "ActorId", "TargetIds", "ActionItemInstanceId", "ExpectedEncounterRevision" };
            string[] actual = Array.ConvertAll(typeof(AttackIntent).GetProperties(), p => p.Name);
            Assert.That(actual, Is.EquivalentTo(allowed));
            string[] forbiddenNameFragments = { "Preview", "Resolution", "Random", "Sample", "Hit", "Damage", "Cost", "Effect", "Modifier", "Armor", "BodyPart", "Range" };
            foreach (string name in actual)
            {
                foreach (string forbidden in forbiddenNameFragments)
                {
                    Assert.That(name, Does.Not.Contain(forbidden));
                }
            }
        }

        [Test]
        public void Preview_HasNoSample_AndAllProposalCollectionsAreCopied()
        {
            ProposedAttackResolution preview = AttackEvaluationService.PreviewAttack(new Reader(State()), new Rules(), Campaign(), Request()).Value;
            Assert.That(preview.RandomSample.HasValue, Is.False);
            Assert.That(preview.Range.IsInRange, Is.True);
            Assert.That(preview.Modifiers.Count, Is.EqualTo(1));
            Assert.That(preview.BodyPart!.Value.BodyPartRef, Is.EqualTo("body"));
            Assert.That(preview.Armor!.Value.ArmorRef, Is.EqualTo("armor"));
            Assert.That(preview.DamageDeltas.Count, Is.EqualTo(1));
            Assert.That(preview.CostDeltas.Count, Is.EqualTo(1));
            Assert.That(preview.EffectCandidates.Count, Is.EqualTo(1));
        }

        private static CampaignHandle Campaign()
        {
            CampaignId id = CampaignId.Parse("camp_0123456789abcdef0123456789abcdef");
            UtcInstant now = UtcInstant.Parse("2026-09-13T00:00:00.0000000Z");
            return new CampaignHandle(id, CampaignPublicId.Parse("cpub_0123456789abcdef0123456789abcdef"), "test", new CampaignManifest(id, "test", "1", "1", "core", "1.0.0", now, now, "1", 1, false));
        }

        private static AttackRequest Request(bool mainGm = true) => new AttackRequest(new AttackIntent(Encounter(), Actor(), new[] { Target() }, Item(), 1), UserId.Parse("user_0123456789abcdef0123456789abcdef"), mainGm, CommandId.Parse("cmd_0123456789abcdef0123456789abcdef"), CorrelationId.Parse("corr_0123456789abcdef0123456789abcdef"));
        private static AttackEvaluationState State(CharacterId? current = null, long revision = 1) => new AttackEvaluationState(new CombatEncounterRecord(Encounter(), Campaign().CampaignId, "core", "1.0.0", new[] { new CombatParticipant(Actor(), 0), new CombatParticipant(Target(), 1) }, revision, 1, 1, CombatEncounterStatus.Open, CombatPhase.TurnOpen, current ?? Actor(), default, default), new AttackEvaluationSnapshot("fingerprint", "core", "1.0.0", revision, Source(), Mechanics(), Participant(Actor()), Array.AsReadOnly(new[] { Participant(Target()) }), new AttackUnavailableInput(AttackInputAvailability.UnavailableNotBound, "no topology"), AttackArmorInput.Unavailable("no armor/effects")));
        private static CombatEncounterId Encounter() => CombatEncounterId.Parse("enc_0123456789abcdef0123456789abcdef");
        private static CharacterId Actor() => CharacterId.Parse("char_0123456789abcdef0123456789abcdef");
        private static CharacterId Target() => CharacterId.Parse("char_fedcba9876543210fedcba9876543210");
        private static ContentDefinitionRef Source() => ContentDefinitionRef.Parse("cdef_0123456789abcdef0123456789abcdef/1");
        private static ItemInstanceId Item() => ItemInstanceId.Parse("iinst_0123456789abcdef0123456789abcdef");
        private static ItemMechanicsSnapshot Mechanics() => new ItemMechanicsSnapshot(Source(), 1, ContentDefinitionType.Item, "{}");
        private static AttackParticipantState Participant(CharacterId id) => new AttackParticipantState(id, CharacterLifecycleStatus.Active, CharacterApprovalState.Approved, new Dictionary<AttributeDefinitionId, long>());

        private sealed class Reader : IAttackStateReader
        {
            private readonly AttackEvaluationState _state;
            public Reader(AttackEvaluationState state) { _state = state; }
            public bool CanControl = true;
            public int ReadCalls;
            public Result<AttackEvaluationState> Read(CampaignHandle campaign, AttackIntent intent, CorrelationId correlationId) { ReadCalls++; return Result<AttackEvaluationState>.Success(_state); }
            public Result<bool> CanControlActor(CampaignHandle campaign, CharacterId actorId, UserId userId, CorrelationId correlationId) => Result<bool>.Success(CanControl);
        }

        private sealed class Rules : IAttackRulesEvaluator
        {
            public int PreviewCalls;
            public int EvaluateCalls;
            public ProposedAttackResolution Preview(AttackIntent intent, AttackEvaluationSnapshot snapshot) { PreviewCalls++; return Resolution(intent, snapshot, 0); }
            public ProposedAttackResolution Evaluate(AttackIntent intent, AttackEvaluationSnapshot snapshot, AttackRandomSample randomSample) { EvaluateCalls++; return Resolution(intent, snapshot, randomSample.Value); }
            private static ProposedAttackResolution Resolution(AttackIntent intent, AttackEvaluationSnapshot snapshot, int sample) => new ProposedAttackResolution(intent, snapshot, sample == 0 ? null : new AttackRandomSample(sample), new AttackRangeResult(true, "in-range"), Array.AsReadOnly(new[] { new AttackModifierEntry("fixture", 1) }), new AttackHitResult(true, "hit"), new AttackBodyPartProposal("body"), new AttackArmorProposal("armor", 0), Array.AsReadOnly(new[] { new AttackDelta(Target().ToString(), 1) }), Array.AsReadOnly(new[] { new AttackDelta(Actor().ToString(), 1) }), Array.AsReadOnly(new[] { new AttackEffectCandidate(Target(), Source(), Source(), EffectApplicationDecision.Apply) }));
        }

        private sealed class ThrowingRandomFactory : IAuthoritativeRandomStreamFactory
        {
            public int CreateCalls;
            public Result<IAuthoritativeRandomStream> Create(RandomDecisionContext context) { CreateCalls++; throw new AssertionException("Preview must not create an RNG stream."); }
        }

        private sealed class RecordingRandomFactory : IAuthoritativeRandomStreamFactory
        {
            private readonly IAuthoritativeRandomStreamFactory _inner = new DeterministicRandomStreamFactory(CampaignRngKey.FromBytes(new byte[32] { 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1 }));
            public RandomDecisionContext Context = null!;
            public Result<IAuthoritativeRandomStream> Create(RandomDecisionContext context) { Context = context; return _inner.Create(context); }
        }
    }
}
