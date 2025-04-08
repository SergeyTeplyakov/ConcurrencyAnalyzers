using System.Diagnostics.CodeAnalysis;
using System.Diagnostics.ContractsLight;

namespace ConcurrencyAnalyzers.Utilities
{
    /// <summary>
    /// An interface that represents a result of an operation.
    /// </summary>
    /// <remarks>
    /// Required for implementing polymorphic operations like 'ThrowIfFailure'.
    /// </remarks>
    public interface IResult
    {
        /// <summary>
        /// Defines whether the result is successful or not.
        /// </summary>
        bool Success { get; }

        /// <summary>
        /// If the result is not successful, contains an error message.
        /// </summary>
        string? ErrorMessage { get; }
    }

    /// <summary>
    /// Represents either a successful result or an error.
    /// </summary>
    public readonly record struct Result<T> : IResult
    {
        private readonly T? _value;

        private readonly string? _error;

        public Result()
        {
            Contract.Requires(false, "The default constructor for 'Result' should not be used.");
            _value = default;
            _error = default;
        }

        internal Result(T result)
        {
            Contract.Requires(result is not null);

            _value = result;
            _error = null;
        }

        internal Result(string error)
        {
            Contract.Requires(!string.IsNullOrEmpty(error));

            _value = default;
            _error = error;
        }

        /// <inheritdoc />
        [MemberNotNullWhen(true, nameof(Value))]
        [MemberNotNullWhen(false, nameof(ErrorMessage))]
        public bool Success => _error is null;

        public T? Value
        {
            get
            {
                Contract.Requires(Success);
                return _value;
            }
        }

        /// <inheritdoc />
        public string? ErrorMessage
        {
            get
            {
                Contract.Requires(!Success);
                return _error;
            }
        }

        /// <inheritdoc />
        public override string ToString()
        {
            if (Success)
            {
                return "Success: " + Value.ToString();
            }

            return "Failure: " + _error;
        }

#pragma warning disable CA1000 // Do not declare static members on generic types
        public static Result<T> FromError<U>(Result<U> other)
#pragma warning restore CA1000 // Do not declare static members on generic types
        {
            Contract.Requires(!other.Success);
            return new Result<T>(other.ErrorMessage);
        }

        public void Deconstruct(out T? value, out string? errorMessage)
        {
            value = default;
            errorMessage = default;

            if (Success)
            {
                value = Value;
            }
            else
            {
                errorMessage = ErrorMessage;
            }
        }
    }

    public static class Result
    {
        public static Result<T> Success<T>(T result) => new Result<T>(result);
        public static Result<T> Error<T>(string errorMessage) => new Result<T>(errorMessage);
    }
}