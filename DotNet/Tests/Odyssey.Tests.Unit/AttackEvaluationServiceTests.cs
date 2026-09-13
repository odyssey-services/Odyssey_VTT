using System;
using System.Collections.Generic;
using NUnit.Framework;
using Odyssey.Application.Commands;
using Odyssey.Application.Combat;
using Odyssey.Application.Persistence;
using Odyssey.Application.Random;
using Odyssey.Application.Results;
using Odyssey.Domain.Combat;
using Odyssey.Domain.Content;
using Odyssey.Domain.Identity;
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
            Action invalidEncounter = () => new AttackIntent(default, actor, new[] { actor }, Source(), 1);
            Action duplicateTargets = () => new AttackIntent(Encounter(), actor, new[] { actor, actor }, Source(), 1);
            Assert.Throws<ArgumentException>(invalidEncounter);
            Assert.Throws<ArgumentException>(duplicateTargets);
            CharacterId[] targets = { Target() };
            AttackIntent intent = new AttackIntent(Encounter(), actor, targets, Source(), 1);
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
            Assert.That(result.Value.RandomSample.Value, Is.InRange(1, 100));
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
            Assert.That(typeof(ProposedAttackResolution).GetProperty("RandomSample")!.PropertyType, Is.EqualTo(typeof(AttackRandomSample)));
            Assert.That(typeof(AttackRandomSample).GetProperty("ProofData"), Is.Null);
            Assert.That(result.Value.EffectDecision, Is.EqualTo(EffectApplicationDecision.Apply));
        }

        private static CampaignHandle Campaign()
        {
            CampaignId id = CampaignId.Parse("camp_0123456789abcdef0123456789abcdef");
            UtcInstant now = UtcInstant.Parse("2026-09-13T00:00:00.0000000Z");
            return new CampaignHandle(id, CampaignPublicId.Parse("cpub_0123456789abcdef0123456789abcdef"), "test", new CampaignManifest(id, "test", "1", "1", "core", "1.0.0", now, now, "1", 1, false));
        }

        private static AttackRequest Request(bool mainGm = true) => new AttackRequest(new AttackIntent(Encounter(), Actor(), new[] { Target() }, Source(), 1), UserId.Parse("user_0123456789abcdef0123456789abcdef"), mainGm, CommandId.Parse("cmd_0123456789abcdef0123456789abcdef"), CorrelationId.Parse("corr_0123456789abcdef0123456789abcdef"));
        private static AttackEvaluationState State(CharacterId? current = null) => new AttackEvaluationState(new CombatEncounterRecord(Encounter(), Campaign().CampaignId, "core", "1.0.0", new[] { new CombatParticipant(Actor(), 0), new CombatParticipant(Target(), 1) }, 1, 1, 1, CombatEncounterStatus.Open, CombatPhase.TurnOpen, current ?? Actor(), default, default), new AttackEvaluationSnapshot("fingerprint", "core", "1.0.0", 1));
        private static CombatEncounterId Encounter() => CombatEncounterId.Parse("enc_0123456789abcdef0123456789abcdef");
        private static CharacterId Actor() => CharacterId.Parse("char_0123456789abcdef0123456789abcdef");
        private static CharacterId Target() => CharacterId.Parse("char_fedcba9876543210fedcba9876543210");
        private static ContentDefinitionRef Source() => ContentDefinitionRef.Parse("cdef_0123456789abcdef0123456789abcdef/1");

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
            private static ProposedAttackResolution Resolution(AttackIntent intent, AttackEvaluationSnapshot snapshot, int sample) => new ProposedAttackResolution(intent, snapshot, new AttackRandomSample(sample), Array.AsReadOnly(new[] { "range", "modifiers", "hit", "body-part", "armor", "damage", "costs" }), true, true, EffectApplicationDecision.Apply);
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
