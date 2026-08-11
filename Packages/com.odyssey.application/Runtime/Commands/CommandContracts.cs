using System;
using System.Collections.ObjectModel;
using System.Collections.Generic;
using Odyssey.Application.Identity;
using Odyssey.Application.Results;
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

    public sealed class ApplicationCommand
    {
        private ApplicationCommand(CommandId commandId, CommandType commandType, CommandVersion commandVersion, CommandFingerprint fingerprint, CorrelationId correlationId, CommandId rootCommandId, CommandId? parentCommandId)
        {
            CommandId = commandId;
            CommandType = commandType;
            CommandVersion = commandVersion;
            Fingerprint = fingerprint;
            CorrelationId = correlationId;
            RootCommandId = rootCommandId;
            ParentCommandId = parentCommandId;
        }

        public CommandId CommandId { get; }
        public CommandType CommandType { get; }
        public CommandVersion CommandVersion { get; }
        public CommandFingerprint Fingerprint { get; }
        public CorrelationId CorrelationId { get; }
        public CommandId RootCommandId { get; }
        public CommandId? ParentCommandId { get; }

        public static ApplicationCommand Create(CommandId commandId, CommandType commandType, CommandVersion commandVersion, CommandFingerprint fingerprint, CorrelationId correlationId, CommandId? rootCommandId = null, CommandId? parentCommandId = null)
        {
            if (!commandId.IsValid) throw new ArgumentException("Command id is required.", nameof(commandId));
            if (!commandType.IsValid) throw new ArgumentException("Command type is required.", nameof(commandType));
            if (!commandVersion.IsValid) throw new ArgumentException("Command version is required.", nameof(commandVersion));
            if (!fingerprint.IsValid) throw new ArgumentException("Command fingerprint is required.", nameof(fingerprint));
            if (!correlationId.IsValid) throw new ArgumentException("Correlation id is required.", nameof(correlationId));
            if (rootCommandId.HasValue && !rootCommandId.Value.IsValid) throw new ArgumentException("Root command id must be valid.", nameof(rootCommandId));
            if (parentCommandId.HasValue && !parentCommandId.Value.IsValid) throw new ArgumentException("Parent command id must be valid.", nameof(parentCommandId));
            return new ApplicationCommand(commandId, commandType, commandVersion, fingerprint, correlationId, rootCommandId ?? commandId, parentCommandId);
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

        private CommandResult(CommandId commandId, CommandResultStatus status, TransactionId? transactionId, ReadOnlyCollection<DomainEvent> events, Error? error)
        {
            CommandId = commandId;
            Status = status;
            TransactionId = transactionId;
            _events = events;
            Error = error;
        }

        public CommandId CommandId { get; }
        public CommandResultStatus Status { get; }
        public TransactionId? TransactionId { get; }
        public IReadOnlyList<DomainEvent> Events => _events;
        public Error? Error { get; }

        public static CommandResult Accepted(CommandId commandId, DomainEventBatch batch)
        {
            if (!commandId.IsValid) throw new ArgumentException("Command id is required.", nameof(commandId));
            if (batch == null) throw new ArgumentNullException(nameof(batch));
            return new CommandResult(commandId, CommandResultStatus.Accepted, batch.TransactionId, CopyEvents(batch.Events), null);
        }

        public static CommandResult Pending(CommandId commandId, DomainEventBatch batch)
        {
            if (!commandId.IsValid) throw new ArgumentException("Command id is required.", nameof(commandId));
            if (batch == null) throw new ArgumentNullException(nameof(batch));
            return new CommandResult(commandId, CommandResultStatus.Pending, batch.TransactionId, CopyEvents(batch.Events), null);
        }

        public static CommandResult Rejected(CommandId commandId, Error error)
        {
            if (!commandId.IsValid) throw new ArgumentException("Command id is required.", nameof(commandId));
            if (error == null) throw new ArgumentNullException(nameof(error));
            return new CommandResult(commandId, CommandResultStatus.Rejected, null, EmptyEvents(), error);
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

    public interface ICommandHandler
    {
        Result<CommandResult> Execute(ApplicationCommand command);
    }

    public interface ICommandReceiptStore
    {
        bool TryGet(CommandId commandId, out CommandReceipt receipt);
        void Save(CommandReceipt receipt);
    }

    public sealed class CommandReceipt
    {
        public CommandReceipt(CommandId commandId, CommandFingerprint fingerprint, CommandResult result)
        {
            if (!commandId.IsValid) throw new ArgumentException("Command id is required.", nameof(commandId));
            if (!fingerprint.IsValid) throw new ArgumentException("Command fingerprint is required.", nameof(fingerprint));
            CommandId = commandId;
            Fingerprint = fingerprint;
            Result = result ?? throw new ArgumentNullException(nameof(result));
        }

        public CommandId CommandId { get; }
        public CommandFingerprint Fingerprint { get; }
        public CommandResult Result { get; }
    }

    public sealed class CommandExecutor
    {
        private readonly ICommandReceiptStore _receipts;
        private readonly ICommandHandler _handler;

        public CommandExecutor(ICommandReceiptStore receipts, ICommandHandler handler)
        {
            _receipts = receipts ?? throw new ArgumentNullException(nameof(receipts));
            _handler = handler ?? throw new ArgumentNullException(nameof(handler));
        }

        public Result<CommandResult> Submit(ApplicationCommand command)
        {
            if (command == null) throw new ArgumentNullException(nameof(command));
            if (_receipts.TryGet(command.CommandId, out CommandReceipt receipt))
            {
                if (receipt.Fingerprint == command.Fingerprint)
                {
                    return Result<CommandResult>.Success(receipt.Result);
                }

                return Result<CommandResult>.Failure(CreateIdentityMismatch(command));
            }

            Result<CommandResult> result = _handler.Execute(command);
            if (result.IsSuccess)
            {
                _receipts.Save(new CommandReceipt(command.CommandId, command.Fingerprint, result.Value));
            }

            return result;
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
