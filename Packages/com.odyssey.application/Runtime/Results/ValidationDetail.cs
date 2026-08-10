using System;
using System.Collections.ObjectModel;
using System.Collections.Generic;

namespace Odyssey.Application.Results
{
    public readonly struct ValidationDetail : IEquatable<ValidationDetail>
    {
        public const int MaxDetailsPerError = 8;
        public const int MaxArguments = 4;
        public const int MaxFieldPathLength = 96;
        private readonly ReadOnlyCollection<SafeMessageArgument> _safeMessageArguments;

        private ValidationDetail(
            ErrorCode code,
            UserMessageKey userMessageKey,
            string? fieldPath,
            ReadOnlyCollection<SafeMessageArgument> safeMessageArguments,
            ValidationSeverity severity)
        {
            Code = code;
            UserMessageKey = userMessageKey;
            FieldPath = fieldPath;
            _safeMessageArguments = safeMessageArguments;
            Severity = severity;
        }

        public ErrorCode Code { get; }
        public UserMessageKey UserMessageKey { get; }
        public string? FieldPath { get; }
        public ValidationSeverity Severity { get; }
        public bool IsValid => Code.IsValid && UserMessageKey.IsValid && _safeMessageArguments != null;
        public IReadOnlyList<SafeMessageArgument> SafeMessageArguments => _safeMessageArguments ?? Array.AsReadOnly(Array.Empty<SafeMessageArgument>());

        public static ValidationDetail Create(
            ErrorCode code,
            UserMessageKey userMessageKey,
            string? fieldPath = null,
            IReadOnlyList<SafeMessageArgument>? safeMessageArguments = null,
            ValidationSeverity severity = ValidationSeverity.Error)
        {
            if (!code.IsValid) throw new ArgumentException("Validation detail code is required.", nameof(code));
            if (!userMessageKey.IsValid) throw new ArgumentException("Validation detail message key is required.", nameof(userMessageKey));
            if (!IsSafeFieldPath(fieldPath)) throw new ArgumentException("Field path is not allowlisted.", nameof(fieldPath));
            if (!Enum.IsDefined(typeof(ValidationSeverity), severity)) throw new ArgumentOutOfRangeException(nameof(severity));

            ReadOnlyCollection<SafeMessageArgument> arguments = CopyBounded(safeMessageArguments, MaxArguments, nameof(safeMessageArguments));
            return new ValidationDetail(code, userMessageKey, fieldPath, arguments, severity);
        }

        public bool Equals(ValidationDetail other)
        {
            return Code.Equals(other.Code) &&
                UserMessageKey.Equals(other.UserMessageKey) &&
                string.Equals(FieldPath, other.FieldPath, StringComparison.Ordinal) &&
                Severity == other.Severity &&
                SequenceEqual(SafeMessageArguments, other.SafeMessageArguments);
        }

        public override bool Equals(object? obj) => obj is ValidationDetail other && Equals(other);
        public override int GetHashCode()
        {
            int hash = HashCode.Combine(Code, UserMessageKey, FieldPath, Severity);
            foreach (SafeMessageArgument argument in SafeMessageArguments)
            {
                hash = HashCode.Combine(hash, argument);
            }

            return hash;
        }

        internal static ReadOnlyCollection<SafeMessageArgument> CopyBounded(IReadOnlyList<SafeMessageArgument>? source, int maxCount, string parameterName)
        {
            if (source == null || source.Count == 0)
            {
                return Array.AsReadOnly(Array.Empty<SafeMessageArgument>());
            }

            if (source.Count > maxCount)
            {
                throw new ArgumentOutOfRangeException(parameterName);
            }

            SafeMessageArgument[] copy = new SafeMessageArgument[source.Count];
            for (int index = 0; index < source.Count; index++)
            {
                if (!source[index].IsValid)
                {
                    throw new ArgumentException("Safe message argument is required.", parameterName);
                }

                copy[index] = source[index];
            }

            return Array.AsReadOnly(copy);
        }

        private static bool IsSafeFieldPath(string? fieldPath)
        {
            if (fieldPath == null)
            {
                return true;
            }

            if (fieldPath.Length == 0 || fieldPath.Length > MaxFieldPathLength || fieldPath.Trim() != fieldPath)
            {
                return false;
            }

            bool segmentHasCharacter = false;
            for (int index = 0; index < fieldPath.Length; index++)
            {
                char c = fieldPath[index];
                if (c == '.')
                {
                    if (!segmentHasCharacter)
                    {
                        return false;
                    }

                    segmentHasCharacter = false;
                    continue;
                }

                if (c == '[')
                {
                    if (!segmentHasCharacter)
                    {
                        return false;
                    }

                    index++;
                    int digitStart = index;
                    while (index < fieldPath.Length && fieldPath[index] >= '0' && fieldPath[index] <= '9')
                    {
                        index++;
                    }

                    if (index == digitStart || index >= fieldPath.Length || fieldPath[index] != ']')
                    {
                        return false;
                    }

                    continue;
                }

                if (!((c >= 'a' && c <= 'z') || (c >= '0' && c <= '9') || c == '_'))
                {
                    return false;
                }

                segmentHasCharacter = true;
            }

            return segmentHasCharacter;
        }

        private static bool SequenceEqual<T>(IReadOnlyList<T> left, IReadOnlyList<T> right)
        {
            if (left.Count != right.Count) return false;
            for (int index = 0; index < left.Count; index++)
            {
                if (!Equals(left[index], right[index])) return false;
            }

            return true;
        }
    }
}
