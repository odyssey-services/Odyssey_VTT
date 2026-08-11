using System;
using System.Collections.Generic;
using Odyssey.Application.Commands;
using Odyssey.Application.Results;
using Odyssey.Application.Time;
using Odyssey.Domain.Events;
using Odyssey.Domain.Identity;
using Odyssey.Domain.Time;

namespace Odyssey.Unity.Client
{
    public interface ITechnicalIdGenerator
    {
        CommandId CreateCommandId();
        CorrelationId CreateCorrelationId();
        DomainEventId CreateDomainEventId();
        TransactionId CreateTransactionId();
    }

    public sealed class GuidTechnicalIdGenerator : ITechnicalIdGenerator
    {
        public CommandId CreateCommandId() => CommandId.Parse("cmd_" + Guid.NewGuid().ToString("N"));
        public CorrelationId CreateCorrelationId() => CorrelationId.Parse("corr_" + Guid.NewGuid().ToString("N"));
        public DomainEventId CreateDomainEventId() => DomainEventId.Parse("evt_" + Guid.NewGuid().ToString("N"));
        public TransactionId CreateTransactionId() => TransactionId.Parse("tx_" + Guid.NewGuid().ToString("N"));
    }

    public sealed class DeveloperShellProbe
    {
        private static readonly CommandFingerprint AcceptedFingerprint = CommandFingerprint.Parse("fp_1000000000000000000000000000000000000000000000000000000000000001");
        private static readonly CommandFingerprint RejectedFingerprint = CommandFingerprint.Parse("fp_2000000000000000000000000000000000000000000000000000000000000002");
        private readonly CommandExecutor _executor;
        private readonly IWallClock _clock;
        private readonly ITechnicalIdGenerator _ids;
        private readonly DeveloperProbeReceiptStore _store;

        public DeveloperShellProbe(IWallClock clock, ITechnicalIdGenerator ids)
        {
            _clock = clock ?? throw new ArgumentNullException(nameof(clock));
            _ids = ids ?? throw new ArgumentNullException(nameof(ids));
            _store = new DeveloperProbeReceiptStore();
            _executor = new CommandExecutor(_store, _store, new DeveloperProbeHandler(clock, ids), clock);
        }

        public int AcceptedCommitCount => _store.AcceptedCommitCount;
        public int RejectedCommitCount => _store.RejectedCommitCount;
        public int EventBatchCommitCount => _store.EventBatchCommitCount;

        public Result<CommandResult> ExecuteAccepted()
        {
            return _executor.Submit(CreateCommand("developer.shell.probe.accepted", AcceptedFingerprint));
        }

        public Result<CommandResult> ExecuteRejected()
        {
            return _executor.Submit(CreateCommand("developer.shell.probe.rejected", RejectedFingerprint));
        }

        private ApplicationCommand CreateCommand(string type, CommandFingerprint fingerprint)
        {
            UtcInstant now = _clock.GetUtcNow();
            return ApplicationCommand.Create(
                _ids.CreateCommandId(),
                CommandType.Parse(type),
                CommandVersion.Create(1),
                fingerprint,
                _ids.CreateCorrelationId(),
                now,
                new CommandIssuer(CommandIssuerKind.HostSystem, null, null),
                CommandPayloadVersion.Create(1),
                new CommandPayload(type));
        }

        private sealed class DeveloperProbeReceiptStore : ICommandReceiptStore, ICommandCommitter
        {
            private readonly Dictionary<CommandId, DeveloperProbeCommitRecord> _records = new Dictionary<CommandId, DeveloperProbeCommitRecord>();

            public int AcceptedCommitCount { get; private set; }
            public int RejectedCommitCount { get; private set; }
            public int EventBatchCommitCount { get; private set; }

            public bool TryGet(CommandId commandId, out CommandReceipt receipt)
            {
                if (_records.TryGetValue(commandId, out DeveloperProbeCommitRecord record))
                {
                    receipt = record.Receipt;
                    return true;
                }

                receipt = null!;
                return false;
            }

            public Result<CommandReceipt> Commit(CommandCommitProposal proposal)
            {
                if (proposal == null) throw new ArgumentNullException(nameof(proposal));
                CommandReceipt receipt = new CommandReceipt(proposal.Command.CommandId, proposal.Fingerprint, proposal.Execution.Result);
                _records.Add(receipt.CommandId, new DeveloperProbeCommitRecord(receipt, proposal.Execution.EventBatch));
                if (receipt.Result.Status == CommandResultStatus.Accepted) AcceptedCommitCount++;
                if (receipt.Result.Status == CommandResultStatus.Rejected) RejectedCommitCount++;
                if (proposal.Execution.EventBatch != null) EventBatchCommitCount++;
                return Result<CommandReceipt>.Success(receipt);
            }
        }

        private sealed class DeveloperProbeCommitRecord
        {
            public DeveloperProbeCommitRecord(CommandReceipt receipt, DomainEventBatch? eventBatch)
            {
                Receipt = receipt ?? throw new ArgumentNullException(nameof(receipt));
                EventBatch = eventBatch;
            }

            public CommandReceipt Receipt { get; }
            public DomainEventBatch? EventBatch { get; }
        }

        private sealed class DeveloperProbeHandler : ICommandHandler
        {
            private static readonly CampaignId ProbeCampaignId = CampaignId.Parse("camp_10000000000000000000000000000001");
            private readonly IWallClock _clock;
            private readonly ITechnicalIdGenerator _ids;

            public DeveloperProbeHandler(IWallClock clock, ITechnicalIdGenerator ids)
            {
                _clock = clock ?? throw new ArgumentNullException(nameof(clock));
                _ids = ids ?? throw new ArgumentNullException(nameof(ids));
            }

            public Result<CommandExecutionProposal> Execute(ApplicationCommand command)
            {
                if (command == null) throw new ArgumentNullException(nameof(command));
                if (command.CommandType.Equals(CommandType.Parse("developer.shell.probe.rejected")))
                {
                    Error error = Error.Create(
                        ErrorCodes.ApplicationDeveloperProbeRejected,
                        ErrorCategory.RuleViolation,
                        SafeReasonCode.ActionNotAllowed,
                        UserMessageKey.Parse("errors.developer_shell.probe_rejected"),
                        RetryDirective.DoNotRetry,
                        command.CorrelationId);
                    CommandResult rejected = CommandResult.Rejected(command, error);
                    return Result<CommandExecutionProposal>.Success(CommandExecutionProposal.FromResult(rejected, null));
                }

                UtcInstant now = _clock.GetUtcNow();
                TransactionId transactionId = _ids.CreateTransactionId();
                DomainEvent domainEvent = DomainEvent.Create(
                    _ids.CreateDomainEventId(),
                    DomainEventType.Parse("developer.shell.probe_emitted"),
                    DomainEventVersion.Create(1),
                    ProbeCampaignId,
                    new AggregateIdentity(AggregateType.Parse("developer.shell"), AggregateId.Parse("probe")),
                    AggregateRevision.Create(1),
                    CampaignRevision.Create(1),
                    EventSequence.Create(1),
                    transactionId,
                    command.RootCommandId.ToCausationCommandId(),
                    command.CommandId.ToCausationCommandId(),
                    command.CorrelationId,
                    command.Issuer.ToDomainActor(),
                    now,
                    "technical",
                    "developer",
                    false,
                    Array.Empty<DomainEventId>(),
                    null,
                    DomainEventPayloadVersion.Create(1),
                    new DomainEventPayload("developer.shell.probe_emitted"));
                DomainEventBatch batch = DomainEventBatch.Create(transactionId, new[] { domainEvent });
                CommandResult result = CommandResult.Accepted(command, batch);
                return Result<CommandExecutionProposal>.Success(CommandExecutionProposal.FromResult(result, batch));
            }
        }
    }
}
