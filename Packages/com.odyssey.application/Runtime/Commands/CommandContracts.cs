using System;
using System.Collections.ObjectModel;
using System.Collections.Generic;
using Odyssey.Application.Identity;
using Odyssey.Application.Random;
using Odyssey.Application.Results;
using Odyssey.Application.Time;
using Odyssey.Domain.Events;
using Odyssey.Domain.Identity;
using Odyssey.Domain.Time;

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

    public readonly struct ClientInstanceId : IEquatable<ClientInstanceId>
    {
        private const string Prefix = "client_";
        private const int HexLength = 32;
        private readonly string _value;

        private ClientInstanceId(string value) => _value = value;
        public bool IsValid => _value != null;
        public static bool TryParse(string? value, out ClientInstanceId id) => CommandText.TryParsePrefixedHex(value, Prefix, HexLength, out id, static v => new ClientInstanceId(v));
        public static ClientInstanceId Parse(string value) => TryParse(value, out ClientInstanceId id) ? id : throw new FormatException("ClientInstanceId is not canonical.");
        public override string ToString() => _value ?? string.Empty;
        public bool Equals(ClientInstanceId other) => string.Equals(_value, other._value, StringComparison.Ordinal);
        public override bool Equals(object? obj) => obj is ClientInstanceId other && Equals(other);
        public override int GetHashCode() => _value == null ? 0 : StringComparer.Ordinal.GetHashCode(_value);
    }

    public readonly struct CommandIssuer
    {
        public CommandIssuer(CommandIssuerKind issuerKind, UserId? actorUserId, CharacterId? actorCharacterId)
        {
            if (!Enum.IsDefined(typeof(CommandIssuerKind), issuerKind)) throw new ArgumentOutOfRangeException(nameof(issuerKind));
            if (issuerKind == CommandIssuerKind.User && (!actorUserId.HasValue || !actorUserId.Value.IsValid)) throw new ArgumentException("User issuer requires actor user id.", nameof(actorUserId));
            if (actorUserId.HasValue && !actorUserId.Value.IsValid) throw new ArgumentException("Actor user id must be valid.", nameof(actorUserId));
            if (actorCharacterId.HasValue && !actorCharacterId.Value.IsValid) throw new ArgumentException("Actor character id must be valid.", nameof(actorCharacterId));
            IssuerKind = issuerKind;
            ActorUserId = actorUserId;
            ActorCharacterId = actorCharacterId;
        }

        public CommandIssuerKind IssuerKind { get; }
        public UserId? ActorUserId { get; }
        public CharacterId? ActorCharacterId { get; }
        public DomainActor ToDomainActor() => new DomainActor(ToDomainActorKind(IssuerKind), ActorUserId, ActorCharacterId);

        private static DomainActorKind ToDomainActorKind(CommandIssuerKind kind)
        {
            switch (kind)
            {
                case CommandIssuerKind.User: return DomainActorKind.User;
                case CommandIssuerKind.HostSystem: return DomainActorKind.HostSystem;
                case CommandIssuerKind.Migration: return DomainActorKind.Migration;
                case CommandIssuerKind.Recovery: return DomainActorKind.Recovery;
                default: throw new ArgumentOutOfRangeException(nameof(kind));
            }
        }
    }

    public readonly struct ExpectedAggregateRevision
    {
        public ExpectedAggregateRevision(AggregateType aggregateType, AggregateId aggregateId, long expectedRevision)
        {
            if (!aggregateType.IsValid) throw new ArgumentException("Aggregate type is required.", nameof(aggregateType));
            if (!aggregateId.IsValid) throw new ArgumentException("Aggregate id is required.", nameof(aggregateId));
            if (expectedRevision < 0) throw new ArgumentOutOfRangeException(nameof(expectedRevision));
            AggregateType = aggregateType;
            AggregateId = aggregateId;
            ExpectedRevision = expectedRevision;
        }

        public AggregateType AggregateType { get; }
        public AggregateId AggregateId { get; }
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
            SessionId? sessionId,
            CommandIssuer issuer,
            ClientInstanceId? originClientInstanceId,
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
        public SessionId? SessionId { get; }
        public CommandIssuer Issuer { get; }
        public ClientInstanceId? OriginClientInstanceId { get; }
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

        public static ApplicationCommand Create(CommandId commandId, CommandType commandType, CommandVersion commandVersion, CommandFingerprint fingerprint, CorrelationId correlationId, UtcInstant receivedAtHost, CommandIssuer issuer, CommandPayloadVersion payloadVersion, CommandPayload payload, CampaignId? campaignId = null, SessionId? sessionId = null, ClientInstanceId? originClientInstanceId = null, CommandId? rootCommandId = null, CommandId? parentCommandId = null, long? expectedCampaignRevision = null, long? expectedSessionSequence = null, IReadOnlyList<ExpectedAggregateRevision>? expectedAggregateRevisions = null, UtcInstant? issuedAtClient = null)
        {
            if (!commandId.IsValid) throw new ArgumentException("Command id is required.", nameof(commandId));
            if (!commandType.IsValid) throw new ArgumentException("Command type is required.", nameof(commandType));
            if (!commandVersion.IsValid) throw new ArgumentException("Command version is required.", nameof(commandVersion));
            if (!fingerprint.IsValid) throw new ArgumentException("Command fingerprint is required.", nameof(fingerprint));
            if (!correlationId.IsValid) throw new ArgumentException("Correlation id is required.", nameof(correlationId));
            if (!receivedAtHost.IsValid) throw new ArgumentException("ReceivedAtHost is required.", nameof(receivedAtHost));
            if (!payloadVersion.IsValid) throw new ArgumentException("Payload version is required.", nameof(payloadVersion));
            if (campaignId.HasValue && !campaignId.Value.IsValid) throw new ArgumentException("Campaign id must be valid.", nameof(campaignId));
            if (sessionId.HasValue && !sessionId.Value.IsValid) throw new ArgumentException("Session id must be valid.", nameof(sessionId));
            if (originClientInstanceId.HasValue && !originClientInstanceId.Value.IsValid) throw new ArgumentException("Origin client instance id must be valid.", nameof(originClientInstanceId));
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
        private CommandResult(CommandId commandId, CommandResultStatus status, CommandId rootCommandId, CorrelationId correlationId, TransactionId? transactionId, CampaignRevision? campaignRevision, EventSequence? eventSequenceFrom, EventSequence? eventSequenceTo, UtcInstant? completedAtHost, Error? error)
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
        public UtcInstant? CompletedAtHost { get; }
        public Error? Error { get; }

        public static CommandResult Accepted(ApplicationCommand command, DomainEventBatch batch)
        {
            if (command == null) throw new ArgumentNullException(nameof(command));
            if (batch == null) throw new ArgumentNullException(nameof(batch));
            ValidateBatchCausation(command, batch);
            return new CommandResult(command.CommandId, CommandResultStatus.Accepted, command.RootCommandId, command.CorrelationId, batch.TransactionId, batch.CampaignRevision, batch.EventSequenceFrom, batch.EventSequenceTo, null, null);
        }

        public static CommandResult Pending(ApplicationCommand command, DomainEventBatch batch)
        {
            if (command == null) throw new ArgumentNullException(nameof(command));
            if (batch == null) throw new ArgumentNullException(nameof(batch));
            ValidateBatchCausation(command, batch);
            return new CommandResult(command.CommandId, CommandResultStatus.Pending, command.RootCommandId, command.CorrelationId, batch.TransactionId, batch.CampaignRevision, batch.EventSequenceFrom, batch.EventSequenceTo, null, null);
        }

        public static CommandResult Rejected(ApplicationCommand command, Error error)
        {
            if (command == null) throw new ArgumentNullException(nameof(command));
            if (error == null) throw new ArgumentNullException(nameof(error));
            if (error.CorrelationId != command.CorrelationId) throw new ArgumentException("Rejected error correlation must match command.", nameof(error));
            return new CommandResult(command.CommandId, CommandResultStatus.Rejected, command.RootCommandId, command.CorrelationId, null, null, null, null, null, error);
        }

        public CommandResult WithCompletedAtHost(UtcInstant completedAtHost)
        {
            if (!completedAtHost.IsValid) throw new ArgumentException("CompletedAtHost is required.", nameof(completedAtHost));
            return new CommandResult(CommandId, Status, RootCommandId, CorrelationId, TransactionId, CampaignRevision, EventSequenceFrom, EventSequenceTo, completedAtHost, Error);
        }

        private static void ValidateBatchCausation(ApplicationCommand command, DomainEventBatch batch)
        {
            foreach (DomainEvent domainEvent in batch.Events)
            {
                if (domainEvent.RootCommandId != command.RootCommandId.ToCausationCommandId()) throw new ArgumentException("Event root command does not match command.", nameof(batch));
                if (domainEvent.CausationCommandId != command.CommandId.ToCausationCommandId()) throw new ArgumentException("Event causation command does not match command.", nameof(batch));
                if (domainEvent.CorrelationId != command.CorrelationId) throw new ArgumentException("Event correlation does not match command.", nameof(batch));
            }
        }
    }

    public sealed class CommandExecutionProposal
    {
        private readonly ReadOnlyCollection<RandomEvidence> _randomEvidence;

        private CommandExecutionProposal(CommandResult result, DomainEventBatch? eventBatch, ReadOnlyCollection<RandomEvidence> randomEvidence)
        {
            Result = result;
            EventBatch = eventBatch;
            _randomEvidence = randomEvidence;
        }

        public CommandResult Result { get; }
        public DomainEventBatch? EventBatch { get; }
        public IReadOnlyList<RandomEvidence> RandomEvidence => _randomEvidence;
        public static CommandExecutionProposal FromResult(CommandResult result, DomainEventBatch? eventBatch, IReadOnlyList<RandomEvidence>? randomEvidence = null)
        {
            if (result == null) throw new ArgumentNullException(nameof(result));
            if ((result.Status == CommandResultStatus.Accepted || result.Status == CommandResultStatus.Pending) && eventBatch == null) throw new ArgumentException("Accepted/Pending proposals require an event batch.", nameof(eventBatch));
            if (result.Status == CommandResultStatus.Rejected && eventBatch != null) throw new ArgumentException("Rejected proposals must not include events.", nameof(eventBatch));
            if (eventBatch != null)
            {
                if (result.TransactionId != eventBatch.TransactionId) throw new ArgumentException("Result transaction id must match event batch.", nameof(eventBatch));
                if (!result.CampaignRevision.HasValue || !result.CampaignRevision.Value.Equals(eventBatch.CampaignRevision)) throw new ArgumentException("Result campaign revision must match event batch.", nameof(eventBatch));
                if (!result.EventSequenceFrom.HasValue || !result.EventSequenceFrom.Value.Equals(eventBatch.EventSequenceFrom)) throw new ArgumentException("Result sequence start must match event batch.", nameof(eventBatch));
                if (!result.EventSequenceTo.HasValue || !result.EventSequenceTo.Value.Equals(eventBatch.EventSequenceTo)) throw new ArgumentException("Result sequence end must match event batch.", nameof(eventBatch));
            }

            return new CommandExecutionProposal(result, eventBatch, CopyRandomEvidence(randomEvidence));
        }

        private static ReadOnlyCollection<RandomEvidence> CopyRandomEvidence(IReadOnlyList<RandomEvidence>? source)
        {
            if (source == null || source.Count == 0) return Array.AsReadOnly(Array.Empty<RandomEvidence>());
            RandomEvidence[] copy = new RandomEvidence[source.Count];
            for (int index = 0; index < source.Count; index++)
            {
                if (!source[index].IsValid) throw new ArgumentException("Random evidence is required.", nameof(source));
                copy[index] = source[index];
            }

            return Array.AsReadOnly(copy);
        }
    }

    public readonly struct RandomEvidence
    {
        public RandomEvidence(string purpose, int value, RngProofData proofData)
        {
            if (!CommandText.IsDottedLowerIdentifier(purpose, 96, 2)) throw new ArgumentException("Random evidence purpose is not canonical.", nameof(purpose));
            Purpose = purpose;
            Value = value;
            ProofData = proofData;
        }

        public string Purpose { get; }
        public int Value { get; }
        public RngProofData ProofData { get; }
        public bool IsValid => Purpose != null;
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
            if (execution.Result.RootCommandId != command.RootCommandId) throw new ArgumentException("Handler result root command id must match submitted command.", nameof(execution));
            if (execution.Result.CorrelationId != command.CorrelationId) throw new ArgumentException("Handler result correlation id must match submitted command.", nameof(execution));
            if (execution.Result.Status == CommandResultStatus.Rejected && execution.Result.Error != null && execution.Result.Error.CorrelationId != command.CorrelationId) throw new ArgumentException("Rejected result correlation id must match submitted command.", nameof(execution));
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
        private readonly IWallClock? _completionClock;

        public CommandExecutor(ICommandReceiptStore receipts, ICommandCommitter committer, ICommandHandler handler, IWallClock? completionClock = null)
        {
            _receipts = receipts ?? throw new ArgumentNullException(nameof(receipts));
            _committer = committer ?? throw new ArgumentNullException(nameof(committer));
            _handler = handler ?? throw new ArgumentNullException(nameof(handler));
            _completionClock = completionClock;
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

                CommandResult response = _completionClock == null ? committed.Value.Result : committed.Value.Result.WithCompletedAtHost(_completionClock.GetUtcNow());
                return Result<CommandResult>.Success(response);
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
