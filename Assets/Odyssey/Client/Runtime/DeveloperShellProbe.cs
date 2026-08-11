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
    public sealed class DeveloperShellProbe
    {
        private static readonly CommandId ProbeCommandId = CommandId.Parse("cmd_10000000000000000000000000000001");
        private static readonly CommandFingerprint ProbeFingerprint = CommandFingerprint.Parse("fp_1000000000000000000000000000000000000000000000000000000000000001");
        private static readonly CommandFingerprint MismatchFingerprint = CommandFingerprint.Parse("fp_2000000000000000000000000000000000000000000000000000000000000002");
        private static readonly CorrelationId ProbeCorrelationId = CorrelationId.Parse("corr_10000000000000000000000000000001");
        private readonly CommandExecutor _executor;
        private readonly IWallClock _clock;

        public DeveloperShellProbe(IWallClock clock)
        {
            _clock = clock ?? throw new ArgumentNullException(nameof(clock));
            DeveloperProbeReceiptStore receipts = new DeveloperProbeReceiptStore();
            _executor = new CommandExecutor(receipts, receipts, new DeveloperProbeHandler(clock), clock);
        }

        public Result<CommandResult> Execute(bool mismatchFingerprint = false)
        {
            UtcInstant now = _clock.GetUtcNow();
            ApplicationCommand command = ApplicationCommand.Create(
                ProbeCommandId,
                CommandType.Parse("developer.shell.probe"),
                CommandVersion.Create(1),
                mismatchFingerprint ? MismatchFingerprint : ProbeFingerprint,
                ProbeCorrelationId,
                now,
                new CommandIssuer(CommandIssuerKind.HostSystem, null, null),
                CommandPayloadVersion.Create(1),
                new CommandPayload("developer.shell.probe"));

            return _executor.Submit(command);
        }

        private sealed class DeveloperProbeReceiptStore : ICommandReceiptStore, ICommandCommitter
        {
            private readonly Dictionary<CommandId, CommandReceipt> _receipts = new Dictionary<CommandId, CommandReceipt>();

            public bool TryGet(CommandId commandId, out CommandReceipt receipt)
            {
                return _receipts.TryGetValue(commandId, out receipt!);
            }

            public Result<CommandReceipt> Commit(CommandCommitProposal proposal)
            {
                if (proposal == null) throw new ArgumentNullException(nameof(proposal));
                CommandReceipt receipt = new CommandReceipt(proposal.Command.CommandId, proposal.Fingerprint, proposal.Execution.Result);
                _receipts.Add(receipt.CommandId, receipt);
                return Result<CommandReceipt>.Success(receipt);
            }
        }

        private sealed class DeveloperProbeHandler : ICommandHandler
        {
            private static readonly CampaignId ProbeCampaignId = CampaignId.Parse("camp_10000000000000000000000000000001");
            private static readonly TransactionId ProbeTransactionId = TransactionId.Parse("tx_10000000000000000000000000000001");
            private static readonly DomainEventId ProbeEventId = DomainEventId.Parse("evt_10000000000000000000000000000001");
            private readonly IWallClock _clock;

            public DeveloperProbeHandler(IWallClock clock)
            {
                _clock = clock ?? throw new ArgumentNullException(nameof(clock));
            }

            public Result<CommandExecutionProposal> Execute(ApplicationCommand command)
            {
                if (command == null) throw new ArgumentNullException(nameof(command));
                UtcInstant now = _clock.GetUtcNow();
                DomainEvent domainEvent = DomainEvent.Create(
                    ProbeEventId,
                    DomainEventType.Parse("developer.shell.probed"),
                    DomainEventVersion.Create(1),
                    ProbeCampaignId,
                    new AggregateIdentity(AggregateType.Parse("developer.shell"), AggregateId.Parse("probe")),
                    AggregateRevision.Create(1),
                    CampaignRevision.Create(1),
                    EventSequence.Create(1),
                    ProbeTransactionId,
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
                    new DomainEventPayload("developer.shell.probe"));
                DomainEventBatch batch = DomainEventBatch.Create(ProbeTransactionId, new[] { domainEvent });
                CommandResult result = CommandResult.Accepted(command, batch);
                return Result<CommandExecutionProposal>.Success(CommandExecutionProposal.FromResult(result, batch));
            }
        }
    }
}
