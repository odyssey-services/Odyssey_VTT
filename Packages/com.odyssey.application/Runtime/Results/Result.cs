using System;

namespace Odyssey.Application.Results
{
    public readonly struct Result
    {
        private readonly Result<Unit> _inner;

        private Result(Result<Unit> inner)
        {
            _inner = inner;
        }

        public bool IsValid => _inner.IsValid;
        public bool IsSuccess => _inner.IsSuccess;
        public bool IsFailure => _inner.IsFailure;
        public Error Error => _inner.Error;

        public static Result Success()
        {
            return new Result(Result<Unit>.Success(Unit.Value));
        }

        public static Result Failure(Error error)
        {
            return new Result(Result<Unit>.Failure(error));
        }
    }

    public readonly struct Result<T>
    {
        private readonly T? _value;
        private readonly Error? _error;
        private readonly byte _state;

        private Result(T value)
        {
            _value = value;
            _error = null;
            _state = 1;
        }

        private Result(Error error)
        {
            _value = default;
            _error = error;
            _state = 2;
        }

        public bool IsValid => _state == 1 || _state == 2;
        public bool IsSuccess => _state == 1;
        public bool IsFailure => _state == 2;

        public T Value
        {
            get
            {
                if (!IsSuccess)
                {
                    throw new InvalidOperationException("Result does not contain a value.");
                }

                return _value!;
            }
        }

        public Error Error
        {
            get
            {
                if (!IsFailure)
                {
                    throw new InvalidOperationException("Result does not contain an error.");
                }

                return _error!;
            }
        }

        public static Result<T> Success(T value)
        {
            if (value == null)
            {
                throw new ArgumentNullException(nameof(value));
            }

            return new Result<T>(value);
        }

        public static Result<T> Failure(Error error)
        {
            if (error == null)
            {
                throw new ArgumentNullException(nameof(error));
            }

            return new Result<T>(error);
        }
    }
}
