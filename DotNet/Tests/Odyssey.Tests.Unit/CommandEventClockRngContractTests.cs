using System;
using System.Collections.Generic;
using NUnit.Framework;
using Odyssey.Application.Commands;
using Odyssey.Application.Identity;
using Odyssey.Application.Random;
using Odyssey.Application.Results;
using Odyssey.Application.Time;
using Odyssey.Domain.Events;
using Odyssey.Rules.Versions;

namespace Odyssey.Tests.Unit
{
    public sealed class CommandEventClockRngContractTests
    {
        [Test]
        public void CommandEnvelopeAndResultRejectInvalidValues()
        {
            CommandId commandId = CommandId.Parse("cmd_0123456789abcdef0123456789abcdef");
            CommandType commandType = CommandType.Parse("application.synthetic.accept");
            CommandVersion commandVersion = CommandVersion.Create(1);
            CommandFingerprint fingerprint = CommandFingerprint.Parse("fp_0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef");
            CorrelationId correlationId = CorrelationId.Parse("corr_0123456789abcdef0123456789abcdef");

            ApplicationCommand command = ApplicationCommand.Create(commandId, commandType, commandVersion, fingerprint, correlationId);

            Assert.That(command.CommandId, Is.EqualTo(commandId));
            Assert.That(command.RootCommandId, Is.EqualTo(commandId));
            Assert.That(command.ParentCommandId.HasValue, Is.False);
            Assert.That(CommandId.TryParse("cmd_0123456789ABCDEF0123456789abcdef", out _), Is.False);
            Assert.That(CommandType.TryParse("Application.Synthetic.Accept", out _), Is.False);
            AssertThrows<ArgumentOutOfRangeException>(() => CommandVersion.Create(0));
            Assert.That(CommandFingerprint.TryParse("fp_0123", out _), Is.False);
        }

        [Test]
        public void CommandResultHasExactTerminalStates()
        {
            CommandId commandId = CommandId.Parse("cmd_0123456789abcdef0123456789abcdef");
            DomainEventBatch batch = CreateBatch(commandId);
            Error rejection = CreateValidationError();

            CommandResult accepted = CommandResult.Accepted(commandId, batch);
            CommandResult pending = CommandResult.Pending(commandId, batch);
            CommandResult rejected = CommandResult.Rejected(commandId, rejection);

            Assert.That(Enum.GetNames(typeof(CommandResultStatus)), Is.EqualTo(new[] { "Accepted", "Pending", "Rejected" }));
            Assert.That(accepted.Status, Is.EqualTo(CommandResultStatus.Accepted));
            Assert.That(accepted.Events, Has.Count.EqualTo(2));
            Assert.That(pending.Status, Is.EqualTo(CommandResultStatus.Pending));
            Assert.That(pending.Events, Has.Count.EqualTo(2));
            Assert.That(pending.TransactionId, Is.EqualTo(batch.TransactionId));
            Assert.That(rejected.Status, Is.EqualTo(CommandResultStatus.Rejected));
            Assert.That(rejected.Error, Is.SameAs(rejection));
            Assert.That(Result<CommandResult>.Success(rejected).IsSuccess, Is.True);
        }

        [Test]
        public void DuplicateCommandReplaysStoredResultWithoutNewEffects()
        {
            CountingHandler handler = new CountingHandler(CommandResult.Accepted(CommandId.Parse("cmd_0123456789abcdef0123456789abcdef"), CreateBatch(CommandId.Parse("cmd_0123456789abcdef0123456789abcdef"))));
            InMemoryReceiptStore receipts = new InMemoryReceiptStore();
            CommandExecutor executor = new CommandExecutor(receipts, handler);
            ApplicationCommand command = CreateCommand("fp_0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef");

            Result<CommandResult> first = executor.Submit(command);
            Result<CommandResult> duplicate = executor.Submit(command);

            Assert.That(first.IsSuccess, Is.True);
            Assert.That(duplicate.IsSuccess, Is.True);
            Assert.That(duplicate.Value, Is.SameAs(first.Value));
            Assert.That(handler.Calls, Is.EqualTo(1));
            Assert.That(receipts.Saves, Is.EqualTo(1));
        }

        [Test]
        public void CommandIdMismatchIsSafeAndDoesNotRevealStoredResult()
        {
            CommandResult stored = CommandResult.Accepted(CommandId.Parse("cmd_0123456789abcdef0123456789abcdef"), CreateBatch(CommandId.Parse("cmd_0123456789abcdef0123456789abcdef")));
            CountingHandler handler = new CountingHandler(stored);
            InMemoryReceiptStore receipts = new InMemoryReceiptStore();
            CommandExecutor executor = new CommandExecutor(receipts, handler);
            ApplicationCommand original = CreateCommand("fp_0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef");
            ApplicationCommand changed = CreateCommand("fp_fedcba9876543210fedcba9876543210fedcba9876543210fedcba9876543210");

            Result<CommandResult> accepted = executor.Submit(original);
            Result<CommandResult> mismatch = executor.Submit(changed);

            Assert.That(accepted.IsSuccess, Is.True);
            Assert.That(mismatch.IsFailure, Is.True);
            Assert.That(mismatch.Error.Code, Is.EqualTo(ErrorCodes.CommandIdentityMismatch));
            Assert.That(mismatch.Error.SafeReasonCode, Is.EqualTo(SafeReasonCode.ActionNotAllowed));
            Assert.That(mismatch.Error.Metadata, Is.Empty);
            Assert.That(handler.Calls, Is.EqualTo(1));
            Assert.That(receipts.Saves, Is.EqualTo(1));
        }

        [Test]
        public void RejectedSyntheticCommandCreatesNoEventsAndConsumesNoRng()
        {
            CountingHandler handler = new CountingHandler(CommandResult.Rejected(CommandId.Parse("cmd_0123456789abcdef0123456789abcdef"), CreateValidationError()));
            InMemoryReceiptStore receipts = new InMemoryReceiptStore();
            CommandExecutor executor = new CommandExecutor(receipts, handler);

            Result<CommandResult> result = executor.Submit(CreateCommand("fp_1111111111111111111111111111111111111111111111111111111111111111"));

            Assert.That(result.IsSuccess, Is.True);
            Assert.That(result.Value.Status, Is.EqualTo(CommandResultStatus.Rejected));
            Assert.That(result.Value.Events, Is.Empty);
            Assert.That(handler.RngCalls, Is.EqualTo(0));
        }

        [Test]
        public void DomainEventBatchIsImmutableOrderedAndCausallyLinked()
        {
            CommandId commandId = CommandId.Parse("cmd_0123456789abcdef0123456789abcdef");
            DomainEvent first = CreateEvent("evt_00000000000000000000000000000001", commandId, 10);
            DomainEvent second = CreateEvent("evt_00000000000000000000000000000002", commandId, 11);
            DomainEvent[] source = { first, second };

            DomainEventBatch batch = DomainEventBatch.Create(first.TransactionId, source);
            source[0] = second;

            Assert.That(batch.Events[0], Is.SameAs(first));
            Assert.That(batch.Events[1], Is.SameAs(second));
            Assert.That(batch.Events, Is.InstanceOf<System.Collections.ObjectModel.ReadOnlyCollection<DomainEvent>>());
            Assert.That(batch.Events[0].CausationCommandId.ToString(), Is.EqualTo(commandId.ToString()));
            AssertThrows<ArgumentException>(() => DomainEventBatch.Create(first.TransactionId, new[] { second, first }));
        }

        [Test]
        public void InjectedClockAndVirtualSchedulerAreDeterministic()
        {
            FixedWallClock wallClock = new FixedWallClock(UtcInstant.FromUnixMilliseconds(1234567890));
            VirtualMonotonicScheduler scheduler = new VirtualMonotonicScheduler(MonotonicInstant.FromTicks(10));
            List<string> fired = new List<string>();

            scheduler.Schedule(MonotonicInstant.FromTicks(30), () => fired.Add("second"));
            scheduler.Schedule(MonotonicInstant.FromTicks(20), () => fired.Add("first"));
            scheduler.AdvanceTo(MonotonicInstant.FromTicks(25));
            scheduler.AdvanceTo(MonotonicInstant.FromTicks(30));

            Assert.That(wallClock.GetUtcNow().UnixMilliseconds, Is.EqualTo(1234567890));
            Assert.That(scheduler.GetCurrentInstant().Ticks, Is.EqualTo(30));
            Assert.That(fired, Is.EqualTo(new[] { "first", "second" }));
        }

        [Test]
        public void RngVectorsAreStableAndProofDataContainsNoSecret()
        {
            byte[] key = new byte[32];
            for (int index = 0; index < key.Length; index++)
            {
                key[index] = (byte)index;
            }

            CommandId commandId = CommandId.Parse("cmd_0123456789abcdef0123456789abcdef");
            RngPurpose purpose = RngPurpose.Parse("test.synthetic_roll");
            RngStreamContext context = RngStreamContext.Create(commandId, 2, purpose, RulesetVersion.Parse("1.2.3"));
            Xoshiro256StarStar stream = DeterministicRng.CreateStream(CampaignRngKey.FromBytes(key), context);

            Assert.That(stream.State0, Is.EqualTo(0xe79c594a1121d0b0UL));
            Assert.That(stream.State1, Is.EqualTo(0xbd5c5aa379119510UL));
            Assert.That(stream.State2, Is.EqualTo(0x27d8932b92e458bcUL));
            Assert.That(stream.State3, Is.EqualTo(0xd6d1e0ec3400e446UL));
            Assert.That(stream.NextUInt64(), Is.EqualTo(0x9df75e240b99eb21UL));
            Assert.That(stream.NextUInt64(), Is.EqualTo(0xa8b9230ba48ef7f8UL));

            stream = DeterministicRng.CreateStream(CampaignRngKey.FromBytes(key), context);
            RngIntegerResult roll = DeterministicRng.NextIntInclusive(ref stream, 1, 20, RngKeyEpochId.Parse("epoch-001"), purpose, 2);

            Assert.That(roll.Value, Is.EqualTo(2));
            Assert.That(roll.ProofData.RejectionCount, Is.EqualTo(0));
            Assert.That(roll.ProofData.KeyEpochId.ToString(), Is.EqualTo("epoch-001"));
            Assert.That(roll.ProofData.GetType().GetProperties(), Has.None.Property("Name").EqualTo("Key"));
            Assert.That(roll.ProofData.GetType().GetProperties(), Has.None.Property("Name").EqualTo("Secret"));
        }

        private static ApplicationCommand CreateCommand(string fingerprint)
        {
            return ApplicationCommand.Create(
                CommandId.Parse("cmd_0123456789abcdef0123456789abcdef"),
                CommandType.Parse("application.synthetic.accept"),
                CommandVersion.Create(1),
                CommandFingerprint.Parse(fingerprint),
                CorrelationId.Parse("corr_0123456789abcdef0123456789abcdef"));
        }

        private static DomainEventBatch CreateBatch(CommandId commandId)
        {
            DomainEvent first = CreateEvent("evt_00000000000000000000000000000001", commandId, 10);
            DomainEvent second = CreateEvent("evt_00000000000000000000000000000002", commandId, 11);
            return DomainEventBatch.Create(first.TransactionId, new[] { first, second });
        }

        private static DomainEvent CreateEvent(string id, CommandId commandId, long sequence)
        {
            return DomainEvent.Create(
                DomainEventId.Parse(id),
                DomainEventType.Parse("application.synthetic.accepted"),
                DomainEventVersion.Create(1),
                TransactionId.Parse("tx_0123456789abcdef0123456789abcdef"),
                commandId.ToCausationCommandId(),
                sequence);
        }

        private static Error CreateValidationError()
        {
            return Error.Create(
                ErrorCodes.ApplicationValidationInvalid,
                ErrorCategory.Validation,
                SafeReasonCode.InvalidRequest,
                UserMessageKey.Parse("errors.application.validation_invalid"),
                RetryDirective.DoNotRetry,
                CorrelationId.Parse("corr_0123456789abcdef0123456789abcdef"));
        }

        private static void AssertThrows<TException>(Action action)
            where TException : Exception
        {
            Assert.That(action, Throws.TypeOf<TException>());
        }

        private sealed class CountingHandler : ICommandHandler
        {
            private readonly CommandResult _result;

            public CountingHandler(CommandResult result) => _result = result;
            public int Calls { get; private set; }
            public int RngCalls { get; private set; }

            public Result<CommandResult> Execute(ApplicationCommand command)
            {
                Calls++;
                if (_result.Status == CommandResultStatus.Accepted)
                {
                    RngCalls++;
                }

                return Result<CommandResult>.Success(_result);
            }
        }

        private sealed class InMemoryReceiptStore : ICommandReceiptStore
        {
            private readonly Dictionary<CommandId, CommandReceipt> _receipts = new Dictionary<CommandId, CommandReceipt>();

            public int Saves { get; private set; }

            public bool TryGet(CommandId commandId, out CommandReceipt receipt)
            {
                return _receipts.TryGetValue(commandId, out receipt!);
            }

            public void Save(CommandReceipt receipt)
            {
                Saves++;
                _receipts.Add(receipt.CommandId, receipt);
            }
        }

        private sealed class FixedWallClock : IWallClock
        {
            private readonly UtcInstant _instant;

            public FixedWallClock(UtcInstant instant) => _instant = instant;
            public UtcInstant GetUtcNow() => _instant;
        }

        private sealed class VirtualMonotonicScheduler : IMonotonicClock, IDelayScheduler
        {
            private readonly SortedList<long, List<Action>> _callbacks = new SortedList<long, List<Action>>();
            private MonotonicInstant _current;

            public VirtualMonotonicScheduler(MonotonicInstant current) => _current = current;
            public MonotonicInstant GetCurrentInstant() => _current;

            public void Schedule(MonotonicInstant dueAt, Action callback)
            {
                if (callback == null) throw new ArgumentNullException(nameof(callback));
                if (!_callbacks.TryGetValue(dueAt.Ticks, out List<Action>? callbacks))
                {
                    callbacks = new List<Action>();
                    _callbacks.Add(dueAt.Ticks, callbacks);
                }

                callbacks.Add(callback);
            }

            public void AdvanceTo(MonotonicInstant instant)
            {
                if (instant < _current) throw new ArgumentOutOfRangeException(nameof(instant));
                _current = instant;
                List<long> ready = new List<long>();
                foreach (long dueAt in _callbacks.Keys)
                {
                    if (dueAt <= instant.Ticks)
                    {
                        ready.Add(dueAt);
                    }
                }

                foreach (long dueAt in ready)
                {
                    foreach (Action callback in _callbacks[dueAt])
                    {
                        callback();
                    }

                    _callbacks.Remove(dueAt);
                }
            }
        }
    }
}
