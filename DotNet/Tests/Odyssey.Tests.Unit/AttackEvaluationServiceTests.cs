using System;
using System.Collections.Generic;
using NUnit.Framework;
using Odyssey.Application.Commands;
using Odyssey.Application.Combat;
using Odyssey.Application.Identity;
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
        private static readonly UserId Host = global::Odyssey.Application.Identity.DevIdentityProvider.AssignHost();

        private static FakeCampaignRepository NewCampaignRepository()
        {
            var repository = new FakeCampaignRepository();
            Assert.That(repository.AddMember(Campaign(), Host, CampaignMembershipRole.MainGm, CommandId.Parse("cmd_" + Guid.NewGuid().ToString("N")), CorrelationId.Parse("corr_" + Guid.NewGuid().ToString("N"))).IsSuccess, Is.True);
            return repository;
        }
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
            Result<ProposedAttackResolution> result = AttackEvaluationService.PreviewAttack(reader, NewCampaignRepository(), rules, Campaign(), Request());
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
            Result<ProposedAttackResolution> result = AttackEvaluationService.EvaluateAttack(reader, NewCampaignRepository(), rules, random, Campaign(), RngKeyEpochId.Parse("epoch-001"), request);
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
            var campaignRepository = NewCampaignRepository();
            var unauthorized = new Reader(State()) { CanControl = false };
            Result<ProposedAttackResolution> denied = AttackEvaluationService.PreviewAttack(unauthorized, campaignRepository, new Rules(), Campaign(), Request(mainGm: false));
            Assert.That(denied.IsFailure, Is.True);
            Assert.That(denied.Error.SafeReasonCode, Is.EqualTo(SafeReasonCode.PermissionDenied));
            var inactive = new Reader(State(current: Target()));
            Result<ProposedAttackResolution> notTurn = AttackEvaluationService.PreviewAttack(inactive, campaignRepository, new Rules(), Campaign(), Request());
            Assert.That(notTurn.IsFailure, Is.True);
            Assert.That(notTurn.Error.SafeReasonCode, Is.EqualTo(SafeReasonCode.ActionNotAllowed));
        }

        [Test]
        public void Proposal_IsImmutableHostOnlyData_AndDoesNotExposeRngProof()
        {
            Result<ProposedAttackResolution> result = AttackEvaluationService.EvaluateAttack(new Reader(State()), NewCampaignRepository(), new Rules(), new RecordingRandomFactory(), Campaign(), RngKeyEpochId.Parse("epoch-001"), Request());
            Assert.That(result.IsSuccess, Is.True);
            Assert.That(typeof(ProposedAttackResolution).GetProperty("RandomSample")!.PropertyType, Is.EqualTo(typeof(AttackRandomSample?)));
            Assert.That(typeof(AttackRandomSample).GetProperty("ProofData"), Is.Null);
            Assert.That(result.Value.EffectCandidates[0].Decision, Is.EqualTo(EffectApplicationDecision.Apply));
        }

        [Test]
        public void Evaluation_RejectsStaleRevisionOrTargetOutsideEncounter_BeforeRules()
        {
            var campaignRepository = NewCampaignRepository();
            var stale = new Reader(State(revision: 2));
            Assert.That(AttackEvaluationService.PreviewAttack(stale, campaignRepository, new Rules(), Campaign(), Request()).IsFailure, Is.True);
            AttackRequest wrongTarget = new AttackRequest(new AttackIntent(Encounter(), Actor(), new[] { CharacterId.Parse("char_11111111111111111111111111111111") }, Item(), 1), Host, CommandId.Parse("cmd_0123456789abcdef0123456789abcdef"), CorrelationId.Parse("corr_0123456789abcdef0123456789abcdef"));
            Assert.That(AttackEvaluationService.PreviewAttack(new Reader(State()), campaignRepository, new Rules(), Campaign(), wrongTarget).IsFailure, Is.True);
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
            ProposedAttackResolution preview = AttackEvaluationService.PreviewAttack(new Reader(State()), NewCampaignRepository(), new Rules(), Campaign(), Request()).Value;
            Assert.That(preview.RandomSample.HasValue, Is.False);
            Assert.That(preview.Range.IsInRange, Is.True);
            Assert.That(preview.Modifiers.Count, Is.EqualTo(1));
            Assert.That(preview.BodyPart!.Value.BodyPartRef, Is.EqualTo("body"));
            Assert.That(preview.Armor!.Value.ArmorRef, Is.EqualTo("armor"));
            Assert.That(preview.DamageDeltas.Count, Is.EqualTo(1));
            Assert.That(preview.CostDeltas.Count, Is.EqualTo(1));
            Assert.That(preview.EffectCandidates.Count, Is.EqualTo(1));
        }

        [Test] // MG-3b: TC-ATTACK-127 -- owner without MainGm still succeeds via CanControlActor (regression: this path must not be broken by the new MainGm check).
        public void Evaluation_OwnerWithoutMainGm_StillSucceedsThroughCanControlActor()
        {
            var owner = new Reader(State()) { CanControl = true };
            Result<ProposedAttackResolution> result = AttackEvaluationService.PreviewAttack(owner, NewCampaignRepository(), new Rules(), Campaign(), Request(mainGm: false));
            Assert.That(result.IsSuccess, Is.True, "an owner (CanControlActor) must still be authorized without being MainGm");
        }

        [Test] // MG-3b: TC-ATTACK-128 -- a registered-but-not-MainGm, non-owning user is denied; a real MainGm succeeds even when CanControlActor would say no.
        public void Evaluation_AreMainGmOrOwnerOnly_ByTheStoredMembership()
        {
            var campaignRepository = NewCampaignRepository();
            UserId player = UserId.Parse("user_" + Guid.NewGuid().ToString("N"));
            Assert.That(campaignRepository.AddMember(Campaign(), player, CampaignMembershipRole.Player, CommandId.Parse("cmd_" + Guid.NewGuid().ToString("N")), CorrelationId.Parse("corr_" + Guid.NewGuid().ToString("N"))).IsSuccess, Is.True);

            var notOwner = new Reader(State()) { CanControl = false };
            AttackRequest playerRequest = new AttackRequest(new AttackIntent(Encounter(), Actor(), new[] { Target() }, Item(), 1), player, CommandId.Parse("cmd_0123456789abcdef0123456789abcdef"), CorrelationId.Parse("corr_0123456789abcdef0123456789abcdef"));
            Result<ProposedAttackResolution> deniedForPlayer = AttackEvaluationService.PreviewAttack(notOwner, campaignRepository, new Rules(), Campaign(), playerRequest);
            Assert.That(deniedForPlayer.IsFailure, Is.True, "a registered Player who does not control the actor must still be denied");

            Result<ProposedAttackResolution> succeedsForHost = AttackEvaluationService.PreviewAttack(notOwner, campaignRepository, new Rules(), Campaign(), Request(mainGm: true));
            Assert.That(succeedsForHost.IsSuccess, Is.True, "a genuinely registered MainGm must be authorized even without CanControlActor");
        }

        [Test] // MG-3b: TC-ATTACK-129 -- fail-closed when the membership lookup itself cannot be read.
        public void Evaluation_FailsClosed_WhenTheMembershipLookupFails()
        {
            var poisoned = PoisonedCampaignRepository.FailsOnLookup();
            Result<ProposedAttackResolution> result = AttackEvaluationService.PreviewAttack(new Reader(State()) { CanControl = false }, poisoned, new Rules(), Campaign(), Request());
            Assert.That(result.IsFailure, Is.True);
            Assert.That(result.Error.Code, Is.EqualTo(ErrorCodes.PersistenceCampaignIoFailed), "an unreadable membership is the lookup's own failure, not a pass and not a fake denial -- even for the host");
            Assert.That(poisoned.LookupCalls, Is.EqualTo(1));
        }

        private static CampaignHandle Campaign()
        {
            CampaignId id = CampaignId.Parse("camp_0123456789abcdef0123456789abcdef");
            UtcInstant now = UtcInstant.Parse("2026-09-13T00:00:00.0000000Z");
            return new CampaignHandle(id, CampaignPublicId.Parse("cpub_0123456789abcdef0123456789abcdef"), "test", new CampaignManifest(id, "test", "1", "1", "core", "1.0.0", now, now, "1", 1, false));
        }

        private static AttackRequest Request(bool mainGm = true) => new AttackRequest(new AttackIntent(Encounter(), Actor(), new[] { Target() }, Item(), 1), mainGm ? Host : UserId.Parse("user_0123456789abcdef0123456789abcdef"), CommandId.Parse("cmd_0123456789abcdef0123456789abcdef"), CorrelationId.Parse("corr_0123456789abcdef0123456789abcdef"));
        private static AttackEvaluationState State(CharacterId? current = null, long revision = 1) => new AttackEvaluationState(new CombatEncounterRecord(Encounter(), Campaign().CampaignId, "core", "1.0.0", new[] { new CombatParticipant(Actor(), 0), new CombatParticipant(Target(), 1) }, revision, 1, 1, CombatEncounterStatus.Open, CombatPhase.TurnOpen, current ?? Actor(), default, default), new AttackEvaluationSnapshot("fingerprint", "core", "1.0.0", revision, Source(), Mechanics(), Participant(Actor()), Array.AsReadOnly(new[] { Participant(Target()) }), AttackTopologyInput.Unavailable("no topology"), AttackArmorInput.Unavailable("no armor/effects")));
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

        /// <summary>A minimal in-memory <see cref="ICampaignRepository"/> -- this project has no reference to the real SQLite one.</summary>
        private sealed class FakeCampaignRepository : ICampaignRepository
        {
            private readonly Dictionary<string, CampaignMembershipRole> _members = new Dictionary<string, CampaignMembershipRole>();

            public Result<CampaignMembership> AddMember(CampaignHandle campaign, UserId userId, CampaignMembershipRole role, CommandId commandId, CorrelationId correlationId)
            {
                string key = userId.ToString();
                if (_members.ContainsKey(key))
                {
                    return Result<CampaignMembership>.Failure(PersistenceFailures.CampaignIoFailed(correlationId));
                }

                _members[key] = role;
                return Result<CampaignMembership>.Success(new CampaignMembership(userId, campaign.CampaignId, role, 1,
                    UtcInstant.FromDateTimeOffset(DateTimeOffset.UtcNow), UtcInstant.FromDateTimeOffset(DateTimeOffset.UtcNow)));
            }

            public Result<CampaignMemberLookup> GetMemberRole(CampaignHandle campaign, UserId userId, CorrelationId correlationId)
            {
                return _members.TryGetValue(userId.ToString(), out CampaignMembershipRole role)
                    ? Result<CampaignMemberLookup>.Success(CampaignMemberLookup.Member(role))
                    : Result<CampaignMemberLookup>.Success(CampaignMemberLookup.NotAMember);
            }

            public Result<CampaignHandle> Create(CreateCampaignRequest request, CommandId commandId, CorrelationId correlationId) => throw new NotSupportedException();
            public Result<CampaignHandle> Open(string campaignFolderPath, CorrelationId correlationId) => throw new NotSupportedException();
            public Result Close(CampaignHandle handle, CorrelationId correlationId) => throw new NotSupportedException();
            public Result<IReadOnlyList<CampaignMembership>> ListMembers(CampaignHandle campaign, CorrelationId correlationId) => throw new NotSupportedException();
        }

        /// <summary>A campaign repository whose membership lookup always fails -- everything else is unused by these checks.</summary>
        private sealed class PoisonedCampaignRepository : ICampaignRepository
        {
            public static PoisonedCampaignRepository FailsOnLookup() => new PoisonedCampaignRepository();

            public int LookupCalls { get; private set; }

            public Result<CampaignMemberLookup> GetMemberRole(CampaignHandle campaign, UserId userId, CorrelationId correlationId)
            {
                LookupCalls++;
                return Result<CampaignMemberLookup>.Failure(PersistenceFailures.CampaignIoFailed(correlationId));
            }

            public Result<CampaignHandle> Create(CreateCampaignRequest request, CommandId commandId, CorrelationId correlationId) => throw new NotSupportedException();
            public Result<CampaignHandle> Open(string campaignFolderPath, CorrelationId correlationId) => throw new NotSupportedException();
            public Result Close(CampaignHandle handle, CorrelationId correlationId) => throw new NotSupportedException();
            public Result<CampaignMembership> AddMember(CampaignHandle campaign, UserId userId, CampaignMembershipRole role, CommandId commandId, CorrelationId correlationId) => throw new NotSupportedException();
            public Result<IReadOnlyList<CampaignMembership>> ListMembers(CampaignHandle campaign, CorrelationId correlationId) => throw new NotSupportedException();
        }
    }
}
