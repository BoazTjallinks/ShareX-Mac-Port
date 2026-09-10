using System;

namespace ShareX.Core.Errors
{
    /// <summary>
    /// A result type carrying either a successful value of <typeparamref name="T"/> or a
    /// <see cref="TaskError"/>. Every native-boundary and job-boundary call in this port
    /// returns one of these instead of throwing for expected failure modes (permission
    /// denied, user cancellation, remote rejection, etc.).
    /// </summary>
    public readonly struct OperationOutcome<T> : IEquatable<OperationOutcome<T>>
    {
        private readonly T? _value;
        private readonly TaskError? _error;

        private OperationOutcome(bool isSuccess, T? value, TaskError? error)
        {
            IsSuccess = isSuccess;
            _value = value;
            _error = error;
        }

        public bool IsSuccess { get; }

        public bool IsFailure => !IsSuccess;

        /// <summary>
        /// The success value. Throws <see cref="InvalidOperationException"/> if this outcome
        /// represents a failure -- check <see cref="IsSuccess"/> (or use <see cref="Match"/>)
        /// before accessing it.
        /// </summary>
        public T Value => IsSuccess
            ? _value!
            : throw new InvalidOperationException(
                $"OperationOutcome<{typeof(T).Name}> has no value; it failed with {_error!.Kind}: {_error.Message}");

        /// <summary>
        /// The failure detail, or <see langword="null"/> when <see cref="IsSuccess"/> is true.
        /// </summary>
        public TaskError? Error => _error;

        public static OperationOutcome<T> Success(T value) => new(true, value, null);

        public static OperationOutcome<T> Failure(TaskError error)
        {
            ArgumentNullException.ThrowIfNull(error);
            return new OperationOutcome<T>(false, default, error);
        }

        public TOut Match<TOut>(Func<T, TOut> onSuccess, Func<TaskError, TOut> onFailure)
        {
            ArgumentNullException.ThrowIfNull(onSuccess);
            ArgumentNullException.ThrowIfNull(onFailure);
            return IsSuccess ? onSuccess(_value!) : onFailure(_error!);
        }

        public bool Equals(OperationOutcome<T> other)
        {
            if (IsSuccess != other.IsSuccess)
            {
                return false;
            }

            return IsSuccess
                ? System.Collections.Generic.EqualityComparer<T>.Default.Equals(_value!, other._value!)
                : Equals(_error, other._error);
        }

        public override bool Equals(object? obj) => obj is OperationOutcome<T> other && Equals(other);

        public override int GetHashCode() => IsSuccess
            ? System.Collections.Generic.EqualityComparer<T>.Default.GetHashCode(_value!)
            : _error!.GetHashCode();

        public static bool operator ==(OperationOutcome<T> left, OperationOutcome<T> right) => left.Equals(right);

        public static bool operator !=(OperationOutcome<T> left, OperationOutcome<T> right) => !left.Equals(right);
    }
}
