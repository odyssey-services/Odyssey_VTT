using System;
using System.Collections.Generic;
using System.Reflection;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using Odyssey.Application.Commands;
using Odyssey.Application.Identity;
using Odyssey.Application.Random;
using Odyssey.Application.Results;
using Odyssey.Application.Time;
using Odyssey.Domain.Events;
using Odyssey.Domain.Identity;
using Odyssey.Domain.Time;
using Odyssey.Rules.Versions;

namespace Odyssey.Tests.Unit
{
    public sealed class CommandEventClockRngContractTests
    {
        private const string GoldenCanonicalMessageHex = "000000156f6479737365792d726e672d73747265616d2d76310000002563616d705f303132333435363738396162636465663031323334353637383961626364656600000024636d645f30313233343536373839616263646566303132333435363738396162636465660000000200000013746573742e73796e7468657469635f726f6c6c00000005312e322e3300000001000000010000000965706f63682d303031";
        private const string GoldenHmacHex = "60286c918a0aca2a5e8aaf3c247405fb3e5bd4790e29148ecb79badcf399e7f4";
        private const string GoldenStreamId = "389f9e6e5dda289403d35697de46d10aec4258af1dec0a01f96436e083a1f27e";
        private const string GoldenSeedCommitment = "a7dbf705ade53401bf502d86d2074569e55fd7d62a63769b8d42043dc2c07e00";
        private const ulong GoldenFirstRaw = 0xfab52f556da9470bUL;
        private const ulong GoldenSecondRaw = 0xc733a2a41b6283e7UL;

        [Test]
        public void CommandEnvelopeAndResultExposeAdr002SemanticFields()
        {
            ApplicationCommand command = CreateCommand("application.synthetic.accept");

            Assert.That(command.CommandId, Is.EqualTo(CommandId.Parse("cmd_0123456789abcdef0123456789abcdef")));
            Assert.That(command.RootCommandId, Is.EqualTo(command.CommandId));
            Assert.That(command.ParentCommandId.HasValue, Is.False);
            Assert.That(command.CampaignId!.Value.ToString(), Is.EqualTo("camp_0123456789abcdef0123456789abcdef"));
            Assert.That(command.SessionId!.Value.ToString(), Is.EqualTo("sess_0123456789abcdef0123456789abcdef"));
            Assert.That(command.OriginClientInstanceId!.Value.ToString(), Is.EqualTo("client_0123456789abcdef0123456789abcdef"));
            Assert.That(command.Issuer.IssuerKind, Is.EqualTo(CommandIssuerKind.User));
            Assert.That(command.Issuer.ActorUserId!.Value.ToString(), Is.EqualTo("user_0123456789abcdef0123456789abcdef"));
            Assert.That(command.ReceivedAtHost.ToString(), Is.EqualTo("2026-08-11T01:02:03.1234567Z"));
            Assert.That(command.PayloadVersion.Value, Is.EqualTo(1));
            Assert.That(command.Payload.PayloadType, Is.EqualTo("application.synthetic.payload"));
            Assert.That(CommandId.TryParse("cmd_0123456789ABCDEF0123456789abcdef", out _), Is.False);
            Assert.That(CommandType.TryParse("Application.Synthetic.Accept", out _), Is.False);
            AssertThrows<ArgumentOutOfRangeException>(() => CommandVersion.Create(0));
            Assert.That(CommandFingerprint.TryParse("fp_0123", out _), Is.False);
            AssertThrows<ArgumentOutOfRangeException>(() => new CommandIssuer((CommandIssuerKind)0, null, null));
            AssertThrows<ArgumentOutOfRangeException>(() => new CommandIssuer((CommandIssuerKind)999, null, null));
        }

        [Test]
        public void SharedSemanticPrimitivesAreDomainOwnedAndTyped()
        {
            Assert.That(typeof(CampaignId).Namespace, Is.EqualTo("Odyssey.Domain.Identity"));
            Assert.That(typeof(CorrelationId).Namespace, Is.EqualTo("Odyssey.Domain.Identity"));
            Assert.That(typeof(UtcInstant).Namespace, Is.EqualTo("Odyssey.Domain.Time"));
            Assert.That(typeof(SessionId).Namespace, Is.EqualTo("Odyssey.Domain.Identity"));
            Assert.That(typeof(UserId).Namespace, Is.EqualTo("Odyssey.Domain.Identity"));
            Assert.That(typeof(CharacterId).Namespace, Is.EqualTo("Odyssey.Domain.Identity"));
            Assert.That(typeof(ClientInstanceId).Namespace, Is.EqualTo("Odyssey.Application.Commands"));
            Assert.That(typeof(AggregateType).Namespace, Is.EqualTo("Odyssey.Domain.Identity"));
            Assert.That(typeof(AggregateId).Namespace, Is.EqualTo("Odyssey.Domain.Identity"));
            Assert.That(typeof(DomainEvent).Assembly.GetType("Odyssey.Domain.Events.DomainCampaignId"), Is.Null);
            Assert.That(typeof(DomainEvent).Assembly.GetType("Odyssey.Domain.Events.EventCorrelationId"), Is.Null);
            Assert.That(typeof(DomainEvent).Assembly.GetType("Odyssey.Domain.Events.DomainUtcInstant"), Is.Null);
            Assert.That(typeof(DeterministicRandomStreamFactory).Assembly.GetType("Odyssey.Application.Random.CampaignId"), Is.Null);
            Assert.That(typeof(DiagnosticId).Namespace, Is.EqualTo("Odyssey.Application.Identity"));
        }

        [Test]
        public void CommandResultHasExactTerminalStatesAndAdr002Metadata()
        {
            ApplicationCommand command = CreateCommand("application.synthetic.accept");
            DomainEventBatch batch = CreateBatch(command, UtcInstant.Parse("2026-08-11T01:02:04.0000000Z"));
            Error rejection = CreateValidationError(command.CorrelationId);

            CommandResult accepted = CommandResult.Accepted(command, batch);
            CommandResult pending = CommandResult.Pending(command, batch);
            CommandResult rejected = CommandResult.Rejected(command, rejection);

            Assert.That(Enum.GetNames(typeof(CommandResultStatus)), Is.EqualTo(new[] { "Accepted", "Pending", "Rejected" }));
            Assert.That(accepted.Status, Is.EqualTo(CommandResultStatus.Accepted));
            Assert.That(accepted.TransactionId, Is.EqualTo(batch.TransactionId));
            Assert.That(accepted.EventSequenceFrom!.Value.Value, Is.EqualTo(100));
            Assert.That(accepted.EventSequenceTo!.Value.Value, Is.EqualTo(101));
            Assert.That(pending.Status, Is.EqualTo(CommandResultStatus.Pending));
            Assert.That(typeof(CommandResult).GetProperty("Events"), Is.Null);
            Assert.That(rejected.Status, Is.EqualTo(CommandResultStatus.Rejected));
            Assert.That(rejected.Error, Is.SameAs(rejection));
            Assert.That(rejected.CompletedAtHost.HasValue, Is.False);
            Assert.That(accepted.WithCompletedAtHost(UtcInstant.Parse("2026-08-11T01:02:05.0000000Z")).CompletedAtHost!.Value.ToString(), Is.EqualTo("2026-08-11T01:02:05.0000000Z"));
            Assert.That(Result<CommandResult>.Success(rejected).IsSuccess, Is.True);
        }

        [Test]
        public void SyntheticAcceptedCommandCommitsEventsReceiptClockAndRealRngOnce()
        {
            FixedWallClock wallClock = new FixedWallClock(UtcInstant.Parse("2026-08-11T01:02:04.0000000Z"), UtcInstant.Parse("2026-08-11T01:02:05.0000000Z"));
            CountingRandomFactory randomFactory = new CountingRandomFactory(CreateKey());
            InMemoryCommitPort commit = new InMemoryCommitPort();
            SyntheticOperationHandler handler = new SyntheticOperationHandler(wallClock, randomFactory);
            CommandExecutor executor = new CommandExecutor(commit, commit, handler, wallClock);
            ApplicationCommand command = CreateCommand("application.synthetic.accept");

            Result<CommandResult> result = executor.Submit(command);

            Assert.That(result.IsSuccess, Is.True);
            Assert.That(result.Value.Status, Is.EqualTo(CommandResultStatus.Accepted));
            Assert.That(result.Value.CompletedAtHost!.Value.ToString(), Is.EqualTo("2026-08-11T01:02:05.0000000Z"));
            Assert.That(commit.CommittedBatchCount, Is.EqualTo(1));
            Assert.That(commit.CommittedEventCount, Is.EqualTo(1));
            Assert.That(commit.LastBatch!.Events[0].OccurredAtHost.ToString(), Is.EqualTo("2026-08-11T01:02:04.0000000Z"));
            Assert.That(commit.LastBatch.Events[0].CausationCommandId.ToString(), Is.EqualTo(command.CommandId.ToString()));
            Assert.That(commit.LastBatch.Events[0].RootCommandId.ToString(), Is.EqualTo(command.RootCommandId.ToString()));
            Assert.That(commit.LastRandomEvidenceCount, Is.EqualTo(1));
            Assert.That(randomFactory.CreateCalls, Is.EqualTo(1));
            Assert.That(handler.EventBatchCreations, Is.EqualTo(1));
            Assert.That(wallClock.Calls, Is.EqualTo(2));
            Assert.That(commit.CommitCalls, Is.EqualTo(1));
            Assert.That(commit.ReceiptCount, Is.EqualTo(1));
        }

        [Test]
        public void DuplicateCommandReplaysStoredResultWithoutNewEffects()
        {
            FixedWallClock wallClock = new FixedWallClock(UtcInstant.Parse("2026-08-11T01:02:04.0000000Z"), UtcInstant.Parse("2026-08-11T01:02:05.0000000Z"));
            CountingRandomFactory randomFactory = new CountingRandomFactory(CreateKey());
            InMemoryCommitPort commit = new InMemoryCommitPort();
            SyntheticOperationHandler handler = new SyntheticOperationHandler(wallClock, randomFactory);
            CommandExecutor executor = new CommandExecutor(commit, commit, handler);
            ApplicationCommand command = CreateCommand("application.synthetic.accept");

            Result<CommandResult> first = executor.Submit(command);
            Result<CommandResult> duplicate = executor.Submit(command);

            Assert.That(first.IsSuccess, Is.True);
            Assert.That(duplicate.IsSuccess, Is.True);
            Assert.That(duplicate.Value.CommandId, Is.EqualTo(first.Value.CommandId));
            Assert.That(handler.Calls, Is.EqualTo(1));
            Assert.That(randomFactory.CreateCalls, Is.EqualTo(1));
            Assert.That(handler.EventBatchCreations, Is.EqualTo(1));
            Assert.That(wallClock.Calls, Is.EqualTo(1));
            Assert.That(commit.CommitCalls, Is.EqualTo(1));
            Assert.That(commit.ReceiptCount, Is.EqualTo(1));
        }

        [Test]
        public void ConcurrentDuplicateCommandIsSingleFlightInCurrentProcess()
        {
            FixedWallClock wallClock = new FixedWallClock(UtcInstant.Parse("2026-08-11T01:02:04.0000000Z"), UtcInstant.Parse("2026-08-11T01:02:05.0000000Z"));
            CountingRandomFactory randomFactory = new CountingRandomFactory(CreateKey());
            InMemoryCommitPort commit = new InMemoryCommitPort();
            SyntheticOperationHandler handler = new SyntheticOperationHandler(wallClock, randomFactory);
            CommandExecutor executor = new CommandExecutor(commit, commit, handler);
            ApplicationCommand command = CreateCommand("application.synthetic.accept");
            Result<CommandResult>[] results = new Result<CommandResult>[2];

            Parallel.Invoke(
                () => results[0] = executor.Submit(command),
                () => results[1] = executor.Submit(command));

            Assert.That(results[0].IsSuccess, Is.True);
            Assert.That(results[1].IsSuccess, Is.True);
            Assert.That(results[0].Value, Is.SameAs(results[1].Value));
            Assert.That(handler.Calls, Is.EqualTo(1));
            Assert.That(randomFactory.CreateCalls, Is.EqualTo(1));
            Assert.That(handler.EventBatchCreations, Is.EqualTo(1));
            Assert.That(commit.CommitCalls, Is.EqualTo(1));
        }

        [Test]
        public void CommandIdMismatchAndHandlerResultMismatchAreSafe()
        {
            FixedWallClock wallClock = new FixedWallClock(UtcInstant.Parse("2026-08-11T01:02:04.0000000Z"), UtcInstant.Parse("2026-08-11T01:02:05.0000000Z"));
            InMemoryCommitPort commit = new InMemoryCommitPort();
            CommandExecutor executor = new CommandExecutor(commit, commit, new SyntheticOperationHandler(wallClock, new CountingRandomFactory(CreateKey())));
            ApplicationCommand original = CreateCommand("application.synthetic.accept", "fp_0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef");
            ApplicationCommand changed = CreateCommand("application.synthetic.accept", "fp_fedcba9876543210fedcba9876543210fedcba9876543210fedcba9876543210");

            Result<CommandResult> accepted = executor.Submit(original);
            Result<CommandResult> mismatch = executor.Submit(changed);

            Assert.That(accepted.IsSuccess, Is.True);
            Assert.That(mismatch.IsFailure, Is.True);
            Assert.That(mismatch.Error.Code, Is.EqualTo(ErrorCodes.CommandIdentityMismatch));
            Assert.That(mismatch.Error.Metadata, Is.Empty);

            CommandExecutor badExecutor = new CommandExecutor(new InMemoryCommitPort(), new InMemoryCommitPort(), new MismatchedResultHandler());
            Result<CommandResult> bad = badExecutor.Submit(original);
            Assert.That(bad.IsFailure, Is.True);
            Assert.That(bad.Error.Code, Is.EqualTo(ErrorCodes.CommandIdentityMismatch));
        }

        [Test]
        public void CommitFailureIsOuterFailureAndDoesNotStoreDurableReceipt()
        {
            FixedWallClock wallClock = new FixedWallClock(UtcInstant.Parse("2026-08-11T01:02:04.0000000Z"), UtcInstant.Parse("2026-08-11T01:02:05.0000000Z"));
            InMemoryCommitPort commit = new InMemoryCommitPort { FailCommit = true };
            CommandExecutor executor = new CommandExecutor(commit, commit, new SyntheticOperationHandler(wallClock, new CountingRandomFactory(CreateKey())));

            Result<CommandResult> result = executor.Submit(CreateCommand("application.synthetic.accept"));

            Assert.That(result.IsFailure, Is.True);
            Assert.That(commit.CommitCalls, Is.EqualTo(1));
            Assert.That(commit.ReceiptCount, Is.EqualTo(0));
            Assert.That(commit.CommittedBatchCount, Is.EqualTo(0));
            Assert.That(commit.CommittedEventCount, Is.EqualTo(0));
        }

        [Test]
        public void RejectedSyntheticCommandCreatesNoEventsAndConsumesNoRngOrTransactionClock()
        {
            FixedWallClock wallClock = new FixedWallClock(UtcInstant.Parse("2026-08-11T01:02:05.0000000Z"));
            CountingRandomFactory randomFactory = new CountingRandomFactory(CreateKey());
            InMemoryCommitPort commit = new InMemoryCommitPort();
            SyntheticOperationHandler handler = new SyntheticOperationHandler(wallClock, randomFactory);
            CommandExecutor executor = new CommandExecutor(commit, commit, handler);

            Result<CommandResult> result = executor.Submit(CreateCommand("application.synthetic.reject"));

            Assert.That(result.IsSuccess, Is.True);
            Assert.That(result.Value.Status, Is.EqualTo(CommandResultStatus.Rejected));
            Assert.That(randomFactory.CreateCalls, Is.EqualTo(0));
            Assert.That(handler.EventBatchCreations, Is.EqualTo(0));
            Assert.That(wallClock.Calls, Is.EqualTo(0));
            Assert.That(commit.CommitCalls, Is.EqualTo(1));
            Assert.That(commit.ReceiptCount, Is.EqualTo(1));
            Assert.That(commit.CommittedBatchCount, Is.EqualTo(0));
            Assert.That(commit.CommittedEventCount, Is.EqualTo(0));
        }

        [Test]
        public void DomainEventBatchIsImmutableOrderedAndSemanticallyComplete()
        {
            ApplicationCommand command = CreateCommand("application.synthetic.accept");
            DomainEvent first = CreateEvent("evt_00000000000000000000000000000001", command, EventSequence.Create(100), AggregateRevision.Create(7), UtcInstant.Parse("2026-08-11T01:02:04.0000000Z"));
            DomainEvent second = CreateEvent("evt_00000000000000000000000000000002", command, EventSequence.Create(101), AggregateRevision.Create(8), UtcInstant.Parse("2026-08-11T01:02:04.0000000Z"));
            DomainEvent[] source = { first, second };

            DomainEventBatch batch = DomainEventBatch.Create(first.TransactionId, source);
            source[0] = second;

            Assert.That(batch.Events[0], Is.SameAs(first));
            Assert.That(batch.Events[1], Is.SameAs(second));
            Assert.That(batch.Events, Is.InstanceOf<System.Collections.ObjectModel.ReadOnlyCollection<DomainEvent>>());
            Assert.That(batch.EventSequenceFrom.Value, Is.EqualTo(100));
            Assert.That(batch.EventSequenceTo.Value, Is.EqualTo(101));
            Assert.That(batch.Events[0].CorrelationId.ToString(), Is.EqualTo(command.CorrelationId.ToString()));
            AssertThrows<ArgumentException>(() => DomainEventBatch.Create(first.TransactionId, new[] { second, first }));
        }

        [Test]
        public void ResultProposalAndCommitProposalRejectCoherenceMismatches()
        {
            ApplicationCommand command = CreateCommand("application.synthetic.accept");
            DomainEventBatch batch = CreateBatch(command, UtcInstant.Parse("2026-08-11T01:02:04.0000000Z"));
            CommandResult result = CommandResult.Accepted(command, batch);
            DomainEventBatch sequenceMismatch = DomainEventBatch.Create(batch.TransactionId, new[] { CreateEvent("evt_00000000000000000000000000000003", command, EventSequence.Create(200), AggregateRevision.Create(9), batch.OccurredAtHost) });
            DomainEventBatch transactionMismatch = DomainEventBatch.Create(
                TransactionId.Parse("tx_fedcba9876543210fedcba9876543210"),
                new[] { CreateEvent("evt_00000000000000000000000000000004", command, EventSequence.Create(100), AggregateRevision.Create(9), batch.OccurredAtHost, transactionId: TransactionId.Parse("tx_fedcba9876543210fedcba9876543210")) });

            AssertThrows<ArgumentException>(() => CommandExecutionProposal.FromResult(result, sequenceMismatch));
            AssertThrows<ArgumentException>(() => CommandExecutionProposal.FromResult(result, transactionMismatch));
            AssertThrows<ArgumentException>(() => CommandExecutionProposal.FromResult(CommandResult.Rejected(command, CreateValidationError(command.CorrelationId)), batch));
            AssertThrows<ArgumentException>(() => CommandResult.Rejected(command, CreateValidationError(CorrelationId.Parse("corr_fedcba9876543210fedcba9876543210"))));

            ApplicationCommand otherCorrelation = CreateCommand("application.synthetic.accept", correlationId: CorrelationId.Parse("corr_fedcba9876543210fedcba9876543210"));
            CommandExecutionProposal execution = CommandExecutionProposal.FromResult(result, batch);
            AssertThrows<ArgumentException>(() => new CommandCommitProposal(otherCorrelation, otherCorrelation.Fingerprint, execution));
        }

        [Test]
        public void DomainEventBatchRejectsSharedMetadataMismatches()
        {
            ApplicationCommand command = CreateCommand("application.synthetic.accept");
            UtcInstant occurredAt = UtcInstant.Parse("2026-08-11T01:02:04.0000000Z");
            DomainEvent first = CreateEvent("evt_00000000000000000000000000000001", command, EventSequence.Create(100), AggregateRevision.Create(7), occurredAt);
            AssertThrows<ArgumentException>(() => DomainEventBatch.Create(first.TransactionId, new[] { first, CreateEvent("evt_00000000000000000000000000000002", command, EventSequence.Create(101), AggregateRevision.Create(8), occurredAt, campaignId: CampaignId.Parse("camp_fedcba9876543210fedcba9876543210")) }));
            AssertThrows<ArgumentException>(() => DomainEventBatch.Create(first.TransactionId, new[] { first, CreateEvent("evt_00000000000000000000000000000002", command, EventSequence.Create(101), AggregateRevision.Create(8), occurredAt, rootCommandId: CausationCommandId.Parse("cmd_fedcba9876543210fedcba9876543210")) }));
            AssertThrows<ArgumentException>(() => DomainEventBatch.Create(first.TransactionId, new[] { first, CreateEvent("evt_00000000000000000000000000000002", command, EventSequence.Create(101), AggregateRevision.Create(8), occurredAt, correlationId: CorrelationId.Parse("corr_fedcba9876543210fedcba9876543210")) }));
            AssertThrows<ArgumentException>(() => DomainEventBatch.Create(first.TransactionId, new[] { first, CreateEvent("evt_00000000000000000000000000000002", command, EventSequence.Create(101), AggregateRevision.Create(8), UtcInstant.Parse("2026-08-11T01:02:05.0000000Z")) }));
        }

        [Test]
        public async Task InjectedClockAndVirtualSchedulerMatchAdr008Shape()
        {
            UtcInstant instant = UtcInstant.FromDateTimeOffset(new DateTimeOffset(2026, 8, 11, 3, 2, 3, 123, TimeSpan.FromHours(2)).AddTicks(4567));
            Assert.That(instant.ToString(), Is.EqualTo("2026-08-11T01:02:03.1234567Z"));
            Assert.That(UtcInstant.Parse(instant.ToString()), Is.EqualTo(instant));

            VirtualScheduler scheduler = new VirtualScheduler();
            MonotonicTimestamp start = scheduler.GetTimestamp();
            await scheduler.DelayAsync(TimeSpan.Zero, CancellationToken.None);
            Assert.That(scheduler.CompletedDelays, Is.EqualTo(1));
            AssertThrows<ArgumentOutOfRangeException>(() => scheduler.DelayAsync(TimeSpan.FromTicks(-1), CancellationToken.None).GetAwaiter().GetResult());

            CancellationTokenSource cts = new CancellationTokenSource();
            ValueTask delayed = scheduler.DelayAsync(TimeSpan.FromSeconds(5), cts.Token);
            cts.Cancel();
            AssertThrows<TaskCanceledException>(() => delayed.AsTask().GetAwaiter().GetResult());

            scheduler.Advance(TimeSpan.FromSeconds(2));
            MonotonicTimestamp end = scheduler.GetTimestamp();
            Assert.That(scheduler.GetElapsedTime(start, end), Is.EqualTo(TimeSpan.FromSeconds(2)));
            Assert.That(default(UtcInstant).IsValid, Is.False);
            Assert.That(default(UtcInstant).Equals(default(UtcInstant)), Is.True);
            Assert.That(default(UtcInstant).GetHashCode(), Is.EqualTo(0));
            AssertThrows<InvalidOperationException>(() => default(UtcInstant).CompareTo(instant));
        }

        [Test]
        public void RngGoldenVectorsUseExactAdr008CanonicalMessageAndProofData()
        {
            RandomDecisionContext context = CreateRandomContext();
            byte[] message = CreateCanonicalMessage(context);
            byte[] key = CreateKeyBytes();
            byte[] digest = ComputeHmacReference(key, message);
            IAuthoritativeRandomStream stream = new DeterministicRandomStreamFactory(CampaignRngKey.FromBytes(key)).Create(context).Value;

            Assert.That(ToHex(message), Is.EqualTo(GoldenCanonicalMessageHex));
            Assert.That(ToHex(digest), Is.EqualTo(GoldenHmacHex));
            Assert.That(stream.Identity.StreamId.ToString(), Is.EqualTo(GoldenStreamId));
            Assert.That(stream.Identity.SeedCommitment.ToString(), Is.EqualTo(GoldenSeedCommitment));
            Assert.That(InvokeInternalNextRaw(stream), Is.EqualTo(GoldenFirstRaw));
            Assert.That(InvokeInternalNextRaw(new DeterministicRandomStreamFactory(CampaignRngKey.FromBytes(key)).Create(context).Value, 2), Is.EqualTo(GoldenSecondRaw));

            stream = new DeterministicRandomStreamFactory(CampaignRngKey.FromBytes(key)).Create(context).Value;
            RandomSample roll = stream.NextInclusive(1, 20, 0).Value;
            Assert.That(roll.Value, Is.EqualTo(4));
            Assert.That(roll.ProofData.DrawIndex, Is.EqualTo(0));
            Assert.That(roll.ProofData.DecisionOrdinal, Is.EqualTo(2));
            Assert.That(roll.ProofData.RawStepCount, Is.EqualTo(1));
            Assert.That(roll.ProofData.StreamId.ToString(), Is.EqualTo(GoldenStreamId));
            Assert.That(roll.ProofData.SeedCommitment.ToString(), Is.EqualTo(GoldenSeedCommitment));
            Assert.That(roll.ProofData.GetType().GetProperties(), Has.None.Property("Name").EqualTo("Key"));
            Assert.That(roll.ProofData.GetType().GetProperties(), Has.None.Property("Name").EqualTo("State0"));
        }

        [Test]
        public void RngRangeBoundariesAndDrawAccountingDoNotAdvanceOnInvalidCalls()
        {
            IAuthoritativeRandomStream stream = new DeterministicRandomStreamFactory(CreateKey()).Create(CreateRandomContext()).Value;

            Assert.That(stream.NextInclusive(7, 7, 0).Value.Value, Is.EqualTo(7));
            Assert.That(stream.NextInclusive(-10, -5, 1).Value.Value, Is.InRange(-10, -5));
            Assert.That(stream.NextInclusive(int.MinValue, int.MaxValue, 2).Value.Value, Is.InRange(int.MinValue, int.MaxValue));

            Result<RandomSample> invalidRange = stream.NextInclusive(2, 1, 3);
            Assert.That(invalidRange.IsFailure, Is.True);
            Assert.That(invalidRange.Error.Code, Is.EqualTo(ErrorCodes.RandomInvalidRange));

            Result<RandomSample> wrongIndex = stream.NextInclusive(1, 10, 4);
            Assert.That(wrongIndex.IsFailure, Is.True);
            Assert.That(wrongIndex.Error.Code, Is.EqualTo(ErrorCodes.RandomDrawIndexMismatch));

            Assert.That(stream.NextInclusive(1, 10, 3).IsSuccess, Is.True);
        }

        [Test]
        public void ForcedRejectionProducesRawStepCountGreaterThanOne()
        {
            IAuthoritativeRandomStream stream = CreateReflectionStreamForForcedRejection();

            RandomSample sample = stream.NextInclusive(1, 10, 0).Value;

            Assert.That(sample.ProofData.RawStepCount, Is.GreaterThan(1));
            Assert.That(sample.ProofData.RejectionCount, Is.EqualTo(sample.ProofData.RawStepCount - 1));
        }

        [Test]
        public void ZeroStateFallbackUsesSecondHmacMessageWithTrailingByte()
        {
            byte[] message = HexToBytes(GoldenCanonicalMessageHex);
            byte[] firstDigest = new byte[32];
            byte[] fallbackMessage = new byte[message.Length + 1];
            Array.Copy(message, fallbackMessage, message.Length);
            fallbackMessage[fallbackMessage.Length - 1] = 0x01;
            byte[] fallbackDigest = ComputeHmacReference(CreateKeyBytes(), fallbackMessage);

            Assert.That(IsAllZeroState(firstDigest), Is.True);
            Assert.That(IsAllZeroState(fallbackDigest), Is.False);
            Assert.That(ToHex(fallbackMessage), Does.EndWith("01"));

            int hmacCalls = 0;
            IAuthoritativeRandomStream stream = CreateStreamWithInjectedHmac((key, actualMessage) =>
            {
                hmacCalls++;
                if (hmacCalls == 1)
                {
                    Assert.That(actualMessage, Is.EqualTo(message));
                    return firstDigest;
                }

                Assert.That(actualMessage, Is.EqualTo(fallbackMessage));
                return fallbackDigest;
            });

            Assert.That(stream.NextInclusive(1, 10, 0).IsSuccess, Is.True);
            Assert.That(hmacCalls, Is.EqualTo(2));
        }

        private static ApplicationCommand CreateCommand(string commandType, string fingerprint = "fp_0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef", CorrelationId? correlationId = null)
        {
            return ApplicationCommand.Create(
                CommandId.Parse("cmd_0123456789abcdef0123456789abcdef"),
                CommandType.Parse(commandType),
                CommandVersion.Create(1),
                CommandFingerprint.Parse(fingerprint),
                correlationId ?? CorrelationId.Parse("corr_0123456789abcdef0123456789abcdef"),
                UtcInstant.Parse("2026-08-11T01:02:03.1234567Z"),
                new CommandIssuer(CommandIssuerKind.User, UserId.Parse("user_0123456789abcdef0123456789abcdef"), CharacterId.Parse("char_0123456789abcdef0123456789abcdef")),
                CommandPayloadVersion.Create(1),
                new CommandPayload("application.synthetic.payload"),
                CampaignId.Parse("camp_0123456789abcdef0123456789abcdef"),
                SessionId.Parse("sess_0123456789abcdef0123456789abcdef"),
                ClientInstanceId.Parse("client_0123456789abcdef0123456789abcdef"));
        }

        private static RandomDecisionContext CreateRandomContext()
        {
            return RandomDecisionContext.Create(
                CampaignId.Parse("camp_0123456789abcdef0123456789abcdef"),
                CommandId.Parse("cmd_0123456789abcdef0123456789abcdef"),
                2,
                RngPurpose.Parse("test.synthetic_roll"),
                RulesetVersion.Parse("1.2.3"),
                RngKeyEpochId.Parse("epoch-001"),
                CorrelationId.Parse("corr_0123456789abcdef0123456789abcdef"));
        }

        private static CampaignRngKey CreateKey() => CampaignRngKey.FromBytes(CreateKeyBytes());

        private static byte[] CreateKeyBytes()
        {
            byte[] key = new byte[32];
            for (int index = 0; index < key.Length; index++)
            {
                key[index] = (byte)index;
            }

            return key;
        }

        private static DomainEventBatch CreateBatch(ApplicationCommand command, UtcInstant occurredAt)
        {
            DomainEvent first = CreateEvent("evt_00000000000000000000000000000001", command, EventSequence.Create(100), AggregateRevision.Create(7), occurredAt);
            DomainEvent second = CreateEvent("evt_00000000000000000000000000000002", command, EventSequence.Create(101), AggregateRevision.Create(8), occurredAt);
            return DomainEventBatch.Create(first.TransactionId, new[] { first, second });
        }

        private static DomainEvent CreateEvent(string id, ApplicationCommand command, EventSequence sequence, AggregateRevision aggregateRevision, UtcInstant occurredAt, CampaignId? campaignId = null, TransactionId? transactionId = null, CausationCommandId? rootCommandId = null, CausationCommandId? causationCommandId = null, CorrelationId? correlationId = null)
        {
            return DomainEvent.Create(
                DomainEventId.Parse(id),
                DomainEventType.Parse("application.synthetic.accepted"),
                DomainEventVersion.Create(1),
                campaignId ?? command.CampaignId!.Value,
                new AggregateIdentity(AggregateType.Parse("application.synthetic"), AggregateId.Parse("synthetic_001")),
                aggregateRevision,
                CampaignRevision.Create(42),
                sequence,
                transactionId ?? TransactionId.Parse("tx_0123456789abcdef0123456789abcdef"),
                rootCommandId ?? command.RootCommandId.ToCausationCommandId(),
                causationCommandId ?? command.CommandId.ToCausationCommandId(),
                correlationId ?? command.CorrelationId,
                command.Issuer.ToDomainActor(),
                occurredAt,
                "gm_visible",
                "public",
                false,
                Array.Empty<DomainEventId>(),
                null,
                DomainEventPayloadVersion.Create(1),
                new DomainEventPayload("application.synthetic.payload"));
        }

        private static Error CreateValidationError(CorrelationId correlationId)
        {
            return Error.Create(
                ErrorCodes.ApplicationValidationInvalid,
                ErrorCategory.Validation,
                SafeReasonCode.InvalidRequest,
                UserMessageKey.Parse("errors.application.validation_invalid"),
                RetryDirective.DoNotRetry,
                correlationId);
        }

        private static Error CreateInternalError(CorrelationId correlationId)
        {
            return Error.Create(
                ErrorCodes.ApplicationInternalUnexpected,
                ErrorCategory.Internal,
                SafeReasonCode.UnexpectedError,
                UserMessageKey.Parse("errors.application.unexpected"),
                RetryDirective.ManualRecoveryRequired,
                correlationId);
        }

        private static void AssertThrows<TException>(Action action)
            where TException : Exception
        {
            Assert.That(action, Throws.TypeOf<TException>());
        }

        private static string ToHex(byte[] bytes)
        {
            char[] chars = new char[bytes.Length * 2];
            const string hex = "0123456789abcdef";
            for (int index = 0; index < bytes.Length; index++)
            {
                chars[index * 2] = hex[bytes[index] >> 4];
                chars[index * 2 + 1] = hex[bytes[index] & 0x0F];
            }

            return new string(chars);
        }

        private static byte[] HexToBytes(string hex)
        {
            byte[] bytes = new byte[hex.Length / 2];
            for (int index = 0; index < bytes.Length; index++)
            {
                bytes[index] = Convert.ToByte(hex.Substring(index * 2, 2), 16);
            }

            return bytes;
        }

        private static byte[] ComputeHmacReference(byte[] key, byte[] message)
        {
            using (HMACSHA256 hmac = new HMACSHA256(key))
            {
                return hmac.ComputeHash(message);
            }
        }

        private static byte[] CreateCanonicalMessage(RandomDecisionContext context)
        {
            Type type = typeof(DeterministicRandomStreamFactory).Assembly.GetType("Odyssey.Application.Random.HmacSha256StreamDeriverV1")!;
            MethodInfo method = type.GetMethod("CreateCanonicalMessage", BindingFlags.Static | BindingFlags.NonPublic)!;
            return (byte[])method.Invoke(null, new object[] { context })!;
        }

        private static IAuthoritativeRandomStream CreateStreamWithInjectedHmac(Func<byte[], byte[], byte[]> hmac)
        {
            Type type = typeof(DeterministicRandomStreamFactory).Assembly.GetType("Odyssey.Application.Random.HmacSha256StreamDeriverV1")!;
            MethodInfo method = type.GetMethod("CreateForTest", BindingFlags.Static | BindingFlags.NonPublic)!;
            return (IAuthoritativeRandomStream)method.Invoke(null, new object[] { CreateKey(), CreateRandomContext(), hmac })!;
        }

        private static bool IsAllZeroState(byte[] digest)
        {
            if (digest.Length != 32) throw new ArgumentException("Digest must be 32 bytes.", nameof(digest));
            ulong combined = BitConverter.ToUInt64(digest, 0) | BitConverter.ToUInt64(digest, 8) | BitConverter.ToUInt64(digest, 16) | BitConverter.ToUInt64(digest, 24);
            return combined == 0UL;
        }

        private static ulong InvokeInternalNextRaw(IAuthoritativeRandomStream stream, int count = 1)
        {
            FieldInfo? field = stream.GetType().GetField("_stream", BindingFlags.NonPublic | BindingFlags.Instance);
            object value = field!.GetValue(stream)!;
            MethodInfo method = value.GetType().GetMethod("NextUInt64", BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance)!;
            ulong result = 0;
            for (int index = 0; index < count; index++)
            {
                result = (ulong)method.Invoke(value, Array.Empty<object>())!;
            }

            return result;
        }

        private static IAuthoritativeRandomStream CreateReflectionStreamForForcedRejection()
        {
            RandomDecisionContext context = CreateRandomContext();
            IAuthoritativeRandomStream normal = new DeterministicRandomStreamFactory(CreateKey()).Create(context).Value;
            Type streamType = normal.GetType();
            Type xoshiroType = streamType.Assembly.GetType("Odyssey.Application.Random.Xoshiro256StarStarV1")!;
            object xoshiro = Activator.CreateInstance(xoshiroType, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public, null, new object[] { 1UL, 0UL, 0UL, 0UL }, null)!;
            return (IAuthoritativeRandomStream)Activator.CreateInstance(streamType, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public, null, new object[] { xoshiro, normal.Identity }, null)!;
        }

        private sealed class FixedWallClock : IWallClock
        {
            private readonly Queue<UtcInstant> _instants;

            public FixedWallClock(params UtcInstant[] instants) => _instants = new Queue<UtcInstant>(instants);
            public int Calls { get; private set; }

            public UtcInstant GetUtcNow()
            {
                Calls++;
                return _instants.Count > 1 ? _instants.Dequeue() : _instants.Peek();
            }
        }

        private sealed class CountingRandomFactory : IAuthoritativeRandomStreamFactory
        {
            private readonly DeterministicRandomStreamFactory _inner;

            public CountingRandomFactory(CampaignRngKey key) => _inner = new DeterministicRandomStreamFactory(key);
            public int CreateCalls { get; private set; }

            public Result<IAuthoritativeRandomStream> Create(RandomDecisionContext context)
            {
                CreateCalls++;
                return _inner.Create(context);
            }
        }

        private sealed class SyntheticOperationHandler : ICommandHandler
        {
            private readonly IWallClock _wallClock;
            private readonly IAuthoritativeRandomStreamFactory _randomFactory;

            public SyntheticOperationHandler(IWallClock wallClock, IAuthoritativeRandomStreamFactory randomFactory)
            {
                _wallClock = wallClock;
                _randomFactory = randomFactory;
            }

            public int Calls { get; private set; }
            public int EventBatchCreations { get; private set; }

            public Result<CommandExecutionProposal> Execute(ApplicationCommand command)
            {
                Calls++;
                if (command.CommandType.ToString() == "application.synthetic.reject")
                {
                    CommandResult rejected = CommandResult.Rejected(command, CreateValidationError(command.CorrelationId));
                    return Result<CommandExecutionProposal>.Success(CommandExecutionProposal.FromResult(rejected, null));
                }

                UtcInstant occurredAt = _wallClock.GetUtcNow();
                Result<IAuthoritativeRandomStream> stream = _randomFactory.Create(CreateRandomContext());
                if (stream.IsFailure)
                {
                    return Result<CommandExecutionProposal>.Failure(stream.Error);
                }

                Result<RandomSample> roll = stream.Value.NextInclusive(1, 20, 0);
                if (roll.IsFailure)
                {
                    return Result<CommandExecutionProposal>.Failure(roll.Error);
                }

                EventBatchCreations++;
                DomainEventBatch batch = DomainEventBatch.Create(
                    TransactionId.Parse("tx_0123456789abcdef0123456789abcdef"),
                    new[]
                    {
                        CreateEvent("evt_00000000000000000000000000000001", command, EventSequence.Create(100), AggregateRevision.Create(7), occurredAt)
                    });
                CommandResult accepted = CommandResult.Accepted(command, batch);
                return Result<CommandExecutionProposal>.Success(CommandExecutionProposal.FromResult(accepted, batch, new[] { new RandomEvidence("test.synthetic_roll", roll.Value.Value, roll.Value.ProofData) }));
            }
        }

        private sealed class MismatchedResultHandler : ICommandHandler
        {
            public Result<CommandExecutionProposal> Execute(ApplicationCommand command)
            {
                ApplicationCommand other = ApplicationCommand.Create(
                    CommandId.Parse("cmd_fedcba9876543210fedcba9876543210"),
                    CommandType.Parse("application.synthetic.accept"),
                    CommandVersion.Create(1),
                    CommandFingerprint.Parse("fp_aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa"),
                    command.CorrelationId,
                    command.ReceivedAtHost,
                    command.Issuer,
                    command.PayloadVersion,
                    command.Payload,
                    command.CampaignId);
                DomainEventBatch batch = CreateBatch(other, UtcInstant.Parse("2026-08-11T01:02:04.0000000Z"));
                CommandResult result = CommandResult.Accepted(other, batch);
                return Result<CommandExecutionProposal>.Success(CommandExecutionProposal.FromResult(result, batch));
            }
        }

        private sealed class InMemoryCommitPort : ICommandReceiptStore, ICommandCommitter
        {
            private readonly Dictionary<CommandId, CommandReceipt> _receipts = new Dictionary<CommandId, CommandReceipt>();
            private readonly List<DomainEventBatch> _batches = new List<DomainEventBatch>();

            public int CommitCalls { get; private set; }
            public int ReceiptCount => _receipts.Count;
            public int CommittedBatchCount => _batches.Count;
            public int CommittedEventCount { get; private set; }
            public int LastRandomEvidenceCount { get; private set; }
            public DomainEventBatch? LastBatch => _batches.Count == 0 ? null : _batches[_batches.Count - 1];
            public bool FailCommit { get; set; }

            public bool TryGet(CommandId commandId, out CommandReceipt receipt)
            {
                return _receipts.TryGetValue(commandId, out receipt!);
            }

            public Result<CommandReceipt> Commit(CommandCommitProposal proposal)
            {
                CommitCalls++;
                if (FailCommit)
                {
                    return Result<CommandReceipt>.Failure(CreateInternalError(proposal.Command.CorrelationId));
                }

                CommandReceipt receipt = new CommandReceipt(proposal.Command.CommandId, proposal.Fingerprint, proposal.Execution.Result);
                if (proposal.Execution.EventBatch != null)
                {
                    _batches.Add(proposal.Execution.EventBatch);
                    CommittedEventCount += proposal.Execution.EventBatch.Events.Count;
                }

                LastRandomEvidenceCount = proposal.Execution.RandomEvidence.Count;
                _receipts.Add(receipt.CommandId, receipt);
                return Result<CommandReceipt>.Success(receipt);
            }
        }

        private sealed class VirtualScheduler : IMonotonicClock, IDelayScheduler
        {
            private readonly Dictionary<MonotonicTimestamp, long> _timestamps = new Dictionary<MonotonicTimestamp, long>();
            private long _ticks;

            public int CompletedDelays { get; private set; }
            public MonotonicTimestamp GetTimestamp()
            {
                MonotonicTimestamp timestamp = MonotonicTimestamp.FromTestTicks(_ticks);
                _timestamps[timestamp] = _ticks;
                return timestamp;
            }

            public TimeSpan GetElapsedTime(MonotonicTimestamp start, MonotonicTimestamp end) => TimeSpan.FromTicks(_timestamps[end] - _timestamps[start]);

            public ValueTask DelayAsync(TimeSpan delay, CancellationToken cancellationToken)
            {
                if (delay < TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(delay));
                if (cancellationToken.IsCancellationRequested) return new ValueTask(Task.FromCanceled(cancellationToken));
                if (delay == TimeSpan.Zero)
                {
                    CompletedDelays++;
                    return default;
                }

                TaskCompletionSource<bool> tcs = new TaskCompletionSource<bool>();
                cancellationToken.Register(() => tcs.TrySetCanceled(cancellationToken));
                return new ValueTask(tcs.Task);
            }

            public void Advance(TimeSpan elapsed)
            {
                _ticks += elapsed.Ticks;
            }
        }
    }
}
