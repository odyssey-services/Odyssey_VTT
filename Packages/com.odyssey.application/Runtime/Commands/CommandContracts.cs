using System;
using System.Collections.ObjectModel;
using System.Collections.Generic;
using Odyssey.Application.Identity;
using Odyssey.Application.Random;
using Odyssey.Application.Results;
using Odyssey.Application.Time;
using Odyssey.Domain.Events;

namespace Odyssey.Application.Commands
{
    public readonly struct CommandId : IEquatable<CommandId>
    {
        private const string Prefix = "cmd_";
        private const int HexLength = 32;
        private readonly string _value;

        private CommandId(string value) => _value = value;
        public bool IsValid => _value != null;
        public static bool TryParse(string? value, out CommandId id) => CommandText.TryParsePrefixedHex(value, Prefix, HexLength, out id, static v => new CommandId(v));
        public static CommandId Parse(string value) => TryParse(value, out CommandId id) ? id : throw new FormatException("CommandId is not canonical.");
        public CausationCommandId ToCausationCommandId() => CausationCommandId.Parse(ToString());
        public override string ToString() => _value ?? string.Empty;
        public bool Equals(CommandId other) => string.Equals(_value, other._value, StringComparison.Ordinal);
        public override bool Equals(object? obj) => obj is CommandId other && Equals(other);
        public override int GetHashCode() => _value == null ? 0 : StringComparer.Ordinal.GetHashCode(_value);
        public static bool operator ==(CommandId left, CommandId right) => left.Equals(right);
        public static bool operator !=(CommandId left, CommandId right) => !left.Equals(right);
    }

    public readonly struct CommandType : IEquatable<CommandType>
    {
        public const int MaxLength = 96;
        private readonly string _value;

        private CommandType(string value) => _value = value;
        public bool IsValid => _value != null;
        public static bool TryParse(string? value, out CommandType type)
        {
            if (CommandText.IsDottedLowerIdentifier(value, MaxLength, 3))
            {
                type = new CommandType(value!);
                return true;
            }

            type = default;
            return false;
        }

        public static CommandType Parse(string value) => TryParse(value, out CommandType type) ? type : throw new FormatException("CommandType is not canonical.");
        public override string ToString() => _value ?? string.Empty;
        public bool Equals(CommandType other) => string.Equals(_value, other._value, StringComparison.Ordinal);
        public override bool Equals(object? obj) => obj is CommandType other && Equals(other);
        public override int GetHashCode() => _value == null ? 0 : StringComparer.Ordinal.GetHashCode(_value);
    }

    public readonly struct CommandVersion : IEquatable<CommandVersion>, IComparable<CommandVersion>
    {
        private readonly int _value;

        private CommandVersion(int value) => _value = value;
        public bool IsValid => _value > 0;
        public int Value => IsValid ? _value : throw new InvalidOperationException("CommandVersion is invalid.");
        public static CommandVersion Create(int value) => value > 0 ? new CommandVersion(value) : throw new ArgumentOutOfRangeException(nameof(value));
        public int CompareTo(CommandVersion other) => _value.CompareTo(other._value);
        public bool Equals(CommandVersion other) => _value == other._value;
        public override bool Equals(object? obj) => obj is CommandVersion other && Equals(other);
        public override int GetHashCode() => _value;
        public override string ToString() => IsValid ? _value.ToString(System.Globalization.CultureInfo.InvariantCulture) : string.Empty;
    }

    public readonly struct CommandPayloadVersion
    {
        private readonly int _value;
        private CommandPayloadVersion(int value) => _value = value;
        public bool IsValid => _value > 0;
        public int Value => IsValid ? _value : throw new InvalidOperationException("CommandPayloadVersion is invalid.");
        public static CommandPayloadVersion Create(int value) => value > 0 ? new CommandPayloadVersion(value) : throw new ArgumentOutOfRangeException(nameof(value));
    }

    public readonly struct CommandFingerprint : IEquatable<CommandFingerprint>
    {
        private const string Prefix = "fp_";
        private const int HexLength = 64;
        private readonly string _value;

        private CommandFingerprint(string value) => _value = value;
        public bool IsValid => _value != null;
        public static bool TryParse(string? value, out CommandFingerprint fingerprint) => CommandText.TryParsePrefixedHex(value, Prefix, HexLength, out fingerprint, static v => new CommandFingerprint(v));
        public static CommandFingerprint Parse(string value) => TryParse(value, out CommandFingerprint fingerprint) ? fingerprint : throw new FormatException("CommandFingerprint is not canonical.");
        public override string ToString() => _value ?? string.Empty;
        public bool Equals(CommandFingerprint other) => string.Equals(_value, other._value, StringComparison.Ordinal);
        public override bool Equals(object? obj) => obj is CommandFingerprint other && Equals(other);
        public override int GetHashCode() => _value == null ? 0 : StringComparer.Ordinal.GetHashCode(_value);
        public static bool operator ==(CommandFingerprint left, CommandFingerprint right) => left.Equals(right);
        public static bool operator !=(CommandFingerprint left, CommandFingerprint right) => !left.Equals(right);
    }

    public enum CommandIssuerKind
    {
        User = 1,
        HostSystem = 2,
        Migration = 3,
        Recovery = 4
    }

    public readonly struct CommandIssuer
    {
        public CommandIssuer(CommandIssuerKind issuerKind, string? actorUserId, string? actorCharacterId)
        {
            if (issuerKind == default) throw new ArgumentException("Issuer kind is required.", nameof(issuerKind));
            if (issuerKind == CommandIssuerKind.User && string.IsNullOrWhiteSpace(actorUserId)) throw new ArgumentException("User issuer requires actor user id.", nameof(actorUserId));
            if (actorUserId != null && !CommandText.IsLowerToken(actorUserId, 64)) throw new ArgumentException("Actor user id is not canonical.", nameof(actorUserId));
            if (actorCharacterId != null && !CommandText.IsLowerToken(actorCharacterId, 64)) throw new ArgumentException("Actor character id is not canonical.", nameof(actorCharacterId));
            IssuerKind = issuerKind;
            ActorUserId = actorUserId;
            ActorCharacterId = actorCharacterId;
        }

        public CommandIssuerKind IssuerKind { get; }
        public string? ActorUserId { get; }
        public string? ActorCharacterId { get; }
        public DomainActor ToDomainActor() => new DomainActor(IssuerKind.ToString().ToLowerInvariant(), ActorUserId, ActorCharacterId);
    }

    public readonly struct ExpectedAggregateRevision
    {
        public ExpectedAggregateRevision(string aggregateType, string aggregateId, long expectedRevision)
        {
            if (!CommandText.IsDottedLowerIdentifier(aggregateType, 96, 2)) throw new ArgumentException("Aggregate type is not canonical.", nameof(aggregateType));
            if (!CommandText.IsLowerToken(aggregateId, 96)) throw new ArgumentException("Aggregate id is not canonical.", nameof(aggregateId));
            if (expectedRevision < 0) throw new ArgumentOutOfRangeException(nameof(expectedRevision));
            AggregateType = aggregateType;
            AggregateId = aggregateId;
            ExpectedRevision = expectedRevision;
        }

        public string AggregateType { get; }
        public string AggregateId { get; }
        public long ExpectedRevision { get; }
    }

    public readonly struct CommandPayload
    {
        public CommandPayload(string payloadType)
        {
            if (!CommandText.IsDottedLowerIdentifier(payloadType, 96, 3)) throw new ArgumentException("Payload type is not canonical.", nameof(payloadType));
            PayloadType = payloadType;
        }

        public string PayloadType { get; }
    }

    public sealed class ApplicationCommand
    {
        private readonly ReadOnlyCollection<ExpectedAggregateRevision> _expectedAggregateRevisions;

        private ApplicationCommand(
            CommandId commandId,
            CommandType commandType,
            CommandVersion commandVersion,
            CampaignId? campaignId,
            string? sessionId,
            CommandIssuer issuer,
            string? originClientInstanceId,
            CommandId rootCommandId,
            CommandId? parentCommandId,
            CorrelationId correlationId,
            long? expectedCampaignRevision,
            long? expectedSessionSequence,
            ReadOnlyCollection<ExpectedAggregateRevision> expectedAggregateRevisions,
            UtcInstant? issuedAtClient,
            UtcInstant receivedAtHost,
            CommandPayloadVersion payloadVersion,
            CommandPayload payload,
            CommandFingerprint fingerprint)
        {
            CommandId = commandId;
            CommandType = commandType;
            CommandVersion = commandVersion;
            CampaignId = campaignId;
            SessionId = sessionId;
            Issuer = issuer;
            OriginClientInstanceId = originClientInstanceId;
            RootCommandId = rootCommandId;
            ParentCommandId = parentCommandId;
            CorrelationId = correlationId;
            ExpectedCampaignRevision = expectedCampaignRevision;
            ExpectedSessionSequence = expectedSessionSequence;
            _expectedAggregateRevisions = expectedAggregateRevisions;
            IssuedAtClient = issuedAtClient;
            ReceivedAtHost = receivedAtHost;
            PayloadVersion = payloadVersion;
            Payload = payload;
            Fingerprint = fingerprint;
        }

        public CommandId CommandId { get; }
        public CommandType CommandType { get; }
        public CommandVersion CommandVersion { get; }
        public CampaignId? CampaignId { get; }
        public string? SessionId { get; }
        public CommandIssuer Issuer { get; }
        public string? OriginClientInstanceId { get; }
        public CommandId RootCommandId { get; }
        public CommandId? ParentCommandId { get; }
        public CorrelationId CorrelationId { get; }
        public long? ExpectedCampaignRevision { get; }
        public long? ExpectedSessionSequence { get; }
        public IReadOnlyList<ExpectedAggregateRevision> ExpectedAggregateRevisions => _expectedAggregateRevisions;
        public UtcInstant? IssuedAtClient { get; }
        public UtcInstant ReceivedAtHost { get; }
        public CommandPayloadVersion PayloadVersion { get; }
        public CommandPayload Payload { get; }
        public CommandFingerprint Fingerprint { get; }

        public static ApplicationCommand Create(CommandId commandId, CommandType commandType, CommandVersion commandVersion, CommandFingerprint fingerprint, CorrelationId correlationId, UtcInstant receivedAtHost, CommandIssuer issuer, CommandPayloadVersion payloadVersion, CommandPayload payload, CampaignId? campaignId = null, string? sessionId = null, string? originClientInstanceId = null, CommandId? rootCommandId = null, CommandId? parentCommandId = null, long? expectedCampaignRevision = null, long? expectedSessionSequence = null, IReadOnlyList<ExpectedAggregateRevision>? expectedAggregateRevisions = null, UtcInstant? issuedAtClient = null)
        {
            if (!commandId.IsValid) throw new ArgumentException("Command id is required.", nameof(commandId));
            if (!commandType.IsValid) throw new ArgumentException("Command type is required.", nameof(commandType));
            if (!commandVersion.IsValid) throw new ArgumentException("Command version is required.", nameof(commandVersion));
            if (!fingerprint.IsValid) throw new ArgumentException("Command fingerprint is required.", nameof(fingerprint));
            if (!correlationId.IsValid) throw new ArgumentException("Correlation id is required.", nameof(correlationId));
            if (!receivedAtHost.IsValid) throw new ArgumentException("ReceivedAtHost is required.", nameof(receivedAtHost));
            if (!payloadVersion.IsValid) throw new ArgumentException("Payload version is required.", nameof(payloadVersion));
            if (campaignId.HasValue && !campaignId.Value.IsValid) throw new ArgumentException("Campaign id must be valid.", nameof(campaignId));
            if (sessionId != null && !CommandText.IsLowerToken(sessionId, 64)) throw new ArgumentException("Session id is not canonical.", nameof(sessionId));
            if (originClientInstanceId != null && !CommandText.IsLowerToken(originClientInstanceId, 64)) throw new ArgumentException("Origin client instance id is not canonical.", nameof(originClientInstanceId));
            if (rootCommandId.HasValue && !rootCommandId.Value.IsValid) throw new ArgumentException("Root command id must be valid.", nameof(rootCommandId));
            if (parentCommandId.HasValue && !parentCommandId.Value.IsValid) throw new ArgumentException("Parent command id must be valid.", nameof(parentCommandId));
            if (expectedCampaignRevision.HasValue && expectedCampaignRevision.Value < 0) throw new ArgumentOutOfRangeException(nameof(expectedCampaignRevision));
            if (expectedSessionSequence.HasValue && expectedSessionSequence.Value < 0) throw new ArgumentOutOfRangeException(nameof(expectedSessionSequence));
            return new ApplicationCommand(commandId, commandType, commandVersion, campaignId, sessionId, issuer, originClientInstanceId, rootCommandId ?? commandId, parentCommandId, correlationId, expectedCampaignRevision, expectedSessionSequence, Array.AsReadOnly(CopyExpectedRevisions(expectedAggregateRevisions ?? Array.Empty<ExpectedAggregateRevision>())), issuedAtClient, receivedAtHost, payloadVersion, payload, fingerprint);
        }

        private static ExpectedAggregateRevision[] CopyExpectedRevisions(IReadOnlyList<ExpectedAggregateRevision> source)
        {
            ExpectedAggregateRevision[] copy = new ExpectedAggregateRevision[source.Count];
            for (int index = 0; index < source.Count; index++)
            {
                copy[index] = source[index];
            }

            return copy;
        }
    }

    public enum CommandResultStatus
    {
        Accepted = 1,
        Pending = 2,
        Rejected = 3
    }

    public sealed class CommandResult
    {
        private readonly ReadOnlyCollection<DomainEvent> _events;

        private CommandResult(CommandId commandId, CommandResultStatus status, CommandId rootCommandId, CorrelationId correlationId, TransactionId? transactionId, CampaignRevision? campaignRevision, EventSequence? eventSequenceFrom, EventSequence? eventSequenceTo, UtcInstant completedAtHost, ReadOnlyCollection<DomainEvent> events, Error? error)
        {
            CommandId = commandId;
            Status = status;
            RootCommandId = rootCommandId;
            CorrelationId = correlationId;
            TransactionId = transactionId;
            CampaignRevision = campaignRevision;
            EventSequenceFrom = eventSequenceFrom;
            EventSequenceTo = eventSequenceTo;
            CompletedAtHost = completedAtHost;
            _events = events;
            Error = error;
        }

        public CommandId CommandId { get; }
        public CommandResultStatus Status { get; }
        public CommandId RootCommandId { get; }
        public CorrelationId CorrelationId { get; }
        public TransactionId? TransactionId { get; }
        public CampaignRevision? CampaignRevision { get; }
        public EventSequence? EventSequenceFrom { get; }
        public EventSequence? EventSequenceTo { get; }
        public UtcInstant CompletedAtHost { get; }
        public IReadOnlyList<DomainEvent> Events => _events;
        public Error? Error { get; }

        public static CommandResult Accepted(ApplicationCommand command, DomainEventBatch batch, UtcInstant completedAtHost)
        {
            if (command == null) throw new ArgumentNullException(nameof(command));
            if (batch == null) throw new ArgumentNullException(nameof(batch));
            if (!completedAtHost.IsValid) throw new ArgumentException("CompletedAtHost is required.", nameof(completedAtHost));
            ValidateBatchCausation(command, batch);
            return new CommandResult(command.CommandId, CommandResultStatus.Accepted, command.RootCommandId, command.CorrelationId, batch.TransactionId, batch.CampaignRevision, batch.EventSequenceFrom, batch.EventSequenceTo, completedAtHost, CopyEvents(batch.Events), null);
        }

        public static CommandResult Pending(ApplicationCommand command, DomainEventBatch batch, UtcInstant completedAtHost)
        {
            if (command == null) throw new ArgumentNullException(nameof(command));
            if (batch == null) throw new ArgumentNullException(nameof(batch));
            if (!completedAtHost.IsValid) throw new ArgumentException("CompletedAtHost is required.", nameof(completedAtHost));
            ValidateBatchCausation(command, batch);
            return new CommandResult(command.CommandId, CommandResultStatus.Pending, command.RootCommandId, command.CorrelationId, batch.TransactionId, batch.CampaignRevision, batch.EventSequenceFrom, batch.EventSequenceTo, completedAtHost, CopyEvents(batch.Events), null);
        }

        public static CommandResult Rejected(ApplicationCommand command, Error error, UtcInstant completedAtHost)
        {
            if (command == null) throw new ArgumentNullException(nameof(command));
            if (error == null) throw new ArgumentNullException(nameof(error));
            if (!completedAtHost.IsValid) throw new ArgumentException("CompletedAtHost is required.", nameof(completedAtHost));
            return new CommandResult(command.CommandId, CommandResultStatus.Rejected, command.RootCommandId, command.CorrelationId, null, null, null, null, completedAtHost, EmptyEvents(), error);
        }

        private static void ValidateBatchCausation(ApplicationCommand command, DomainEventBatch batch)
        {
            foreach (DomainEvent domainEvent in batch.Events)
            {
                if (domainEvent.RootCommandId != command.RootCommandId.ToCausationCommandId()) throw new ArgumentException("Event root command does not match command.", nameof(batch));
                if (domainEvent.CausationCommandId != command.CommandId.ToCausationCommandId()) throw new ArgumentException("Event causation command does not match command.", nameof(batch));
                if (domainEvent.CorrelationId.ToString() != command.CorrelationId.ToString()) throw new ArgumentException("Event correlation does not match command.", nameof(batch));
            }
        }

        private static ReadOnlyCollection<DomainEvent> CopyEvents(IReadOnlyList<DomainEvent> events)
        {
            DomainEvent[] copy = new DomainEvent[events.Count];
            for (int index = 0; index < events.Count; index++)
            {
                copy[index] = events[index] ?? throw new ArgumentException("Domain event is required.", nameof(events));
            }

            return Array.AsReadOnly(copy);
        }

        private static ReadOnlyCollection<DomainEvent> EmptyEvents() => Array.AsReadOnly(Array.Empty<DomainEvent>());
    }

    public sealed class CommandExecutionProposal
    {
        private CommandExecutionProposal(CommandResult result, DomainEventBatch? eventBatch)
        {
            Result = result;
            EventBatch = eventBatch;
        }

        public CommandResult Result { get; }
        public DomainEventBatch? EventBatch { get; }
        public static CommandExecutionProposal FromResult(CommandResult result, DomainEventBatch? eventBatch)
        {
            if (result == null) throw new ArgumentNullException(nameof(result));
            if ((result.Status == CommandResultStatus.Accepted || result.Status == CommandResultStatus.Pending) && eventBatch == null) throw new ArgumentException("Accepted/Pending proposals require an event batch.", nameof(eventBatch));
            if (result.Status == CommandResultStatus.Rejected && eventBatch != null) throw new ArgumentException("Rejected proposals must not include events.", nameof(eventBatch));
            return new CommandExecutionProposal(result, eventBatch);
        }
    }

    public interface ICommandHandler
    {
        Result<CommandExecutionProposal> Execute(ApplicationCommand command);
    }

    public interface ICommandCommitter
    {
        Result<CommandReceipt> Commit(CommandCommitProposal proposal);
    }

    public sealed class CommandCommitProposal
    {
        public CommandCommitProposal(ApplicationCommand command, CommandFingerprint fingerprint, CommandExecutionProposal execution)
        {
            Command = command ?? throw new ArgumentNullException(nameof(command));
            if (!fingerprint.IsValid) throw new ArgumentException("Command fingerprint is required.", nameof(fingerprint));
            Execution = execution ?? throw new ArgumentNullException(nameof(execution));
            Fingerprint = fingerprint;
            if (execution.Result.CommandId != command.CommandId) throw new ArgumentException("Handler result command id must match submitted command.", nameof(execution));
        }

        public ApplicationCommand Command { get; }
        public CommandFingerprint Fingerprint { get; }
        public CommandExecutionProposal Execution { get; }
    }

    public interface ICommandReceiptStore
    {
        bool TryGet(CommandId commandId, out CommandReceipt receipt);
    }

    public sealed class CommandReceipt
    {
        public CommandReceipt(CommandId commandId, CommandFingerprint fingerprint, CommandResult result)
        {
            if (!commandId.IsValid) throw new ArgumentException("Command id is required.", nameof(commandId));
            if (!fingerprint.IsValid) throw new ArgumentException("Command fingerprint is required.", nameof(fingerprint));
            if (result == null) throw new ArgumentNullException(nameof(result));
            if (result.CommandId != commandId) throw new ArgumentException("Receipt command id must match result command id.", nameof(result));
            CommandId = commandId;
            Fingerprint = fingerprint;
            Result = result;
        }

        public CommandId CommandId { get; }
        public CommandFingerprint Fingerprint { get; }
        public CommandResult Result { get; }
    }

    public sealed class CommandExecutor
    {
        private readonly object _gate = new object();
        private readonly ICommandReceiptStore _receipts;
        private readonly ICommandCommitter _committer;
        private readonly ICommandHandler _handler;

        public CommandExecutor(ICommandReceiptStore receipts, ICommandCommitter committer, ICommandHandler handler)
        {
            _receipts = receipts ?? throw new ArgumentNullException(nameof(receipts));
            _committer = committer ?? throw new ArgumentNullException(nameof(committer));
            _handler = handler ?? throw new ArgumentNullException(nameof(handler));
        }

        public Result<CommandResult> Submit(ApplicationCommand command)
        {
            if (command == null) throw new ArgumentNullException(nameof(command));
            lock (_gate)
            {
                if (_receipts.TryGet(command.CommandId, out CommandReceipt receipt))
                {
                    if (receipt.Fingerprint == command.Fingerprint)
                    {
                        return Result<CommandResult>.Success(receipt.Result);
                    }

                    return Result<CommandResult>.Failure(CreateIdentityMismatch(command));
                }

                Result<CommandExecutionProposal> execution = _handler.Execute(command);
                if (execution.IsFailure)
                {
                    return Result<CommandResult>.Failure(execution.Error);
                }

                if (execution.Value.Result.CommandId != command.CommandId)
                {
                    return Result<CommandResult>.Failure(CreateIdentityMismatch(command));
                }

                Result<CommandReceipt> committed = _committer.Commit(new CommandCommitProposal(command, command.Fingerprint, execution.Value));
                if (committed.IsFailure)
                {
                    return Result<CommandResult>.Failure(committed.Error);
                }

                return Result<CommandResult>.Success(committed.Value.Result);
            }
        }

        private static Error CreateIdentityMismatch(ApplicationCommand command)
        {
            return Error.Create(
                ErrorCodes.CommandIdentityMismatch,
                ErrorCategory.Security,
                SafeReasonCode.ActionNotAllowed,
                UserMessageKey.Parse("errors.application.command_identity_mismatch"),
                RetryDirective.DoNotRetry,
                command.CorrelationId);
        }
    }

    internal static class CommandText
    {
        internal static bool TryParsePrefixedHex<T>(string? value, string prefix, int hexLength, out T result, Func<string, T> factory)
        {
            if (value == null || value.Length != prefix.Length + hexLength || !value.StartsWith(prefix, StringComparison.Ordinal))
            {
                result = default!;
                return false;
            }

            for (int index = prefix.Length; index < value.Length; index++)
            {
                char c = value[index];
                if (!((c >= '0' && c <= '9') || (c >= 'a' && c <= 'f')))
                {
                    result = default!;
                    return false;
                }
            }

            result = factory(value);
            return true;
        }

        internal static bool IsLowerToken(string? value, int maxLength)
        {
            if (string.IsNullOrWhiteSpace(value) || value!.Length > maxLength || value.Trim() != value)
            {
                return false;
            }

            for (int index = 0; index < value.Length; index++)
            {
                char c = value[index];
                if (!((c >= 'a' && c <= 'z') || (c >= '0' && c <= '9') || c == '_' || c == '-'))
                {
                    return false;
                }
            }

            return true;
        }

        internal static bool IsDottedLowerIdentifier(string? value, int maxLength, int minSegments)
        {
            if (string.IsNullOrWhiteSpace(value) || value!.Length > maxLength || value.Trim() != value)
            {
                return false;
            }

            int segments = 1;
            bool segmentHasCharacter = false;
            for (int index = 0; index < value.Length; index++)
            {
                char c = value[index];
                if (c == '.')
                {
                    if (!segmentHasCharacter) return false;
                    segments++;
                    segmentHasCharacter = false;
                    continue;
                }

                if (!((c >= 'a' && c <= 'z') || (c >= '0' && c <= '9') || c == '_'))
                {
                    return false;
                }

                segmentHasCharacter = true;
            }

            return segmentHasCharacter && segments >= minSegments;
        }
    }
}
